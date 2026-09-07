using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Capture;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;
using Warlord.Gameplay.Units.Behaviours;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Боевой такт охранников (ГДД §1). Отдельная система рядом с <see cref="Units.UnitAiSystem"/>:
    /// та прогоняет армию по приказу игрока, эта — гарнизоны по их точкам.
    ///
    /// Регенерация гарнизона считается здесь же, а не в общей системе восстановления, потому что
    /// зависит от того же состояния, которое эта система только что вычислила: стоит ли охранник
    /// в своём слоте и давно ли он дрался. Разносить это по двум системам значило бы считать
    /// одно и то же дважды и рисковать расхождением.
    /// </summary>
    public sealed class GarrisonSystem : IServerSystem
    {
        private readonly IMatchContext _context;
        private readonly List<GarrisonRoster.Post> _buffer = new(64);

        public GarrisonSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Garrison;

        public void Tick(float deltaTime)
        {
            if (_context.Phase != MatchPhase.Running)
                return;

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int p = 0; p < players.Count; p++)
            {
                PlayerState player = players[p];
                GarrisonRoster roster = player != null ? player.Garrison : null;

                if (roster == null || roster.Count == 0)
                    continue;

                // Копия списка: охранник может погибнуть прямо в такте, а список — тот же,
                // из которого его уберёт PurgeDead.
                _buffer.Clear();
                _buffer.AddRange(roster.Posts);

                for (int i = 0; i < _buffer.Count; i++)
                    TickGuard(_buffer[i], deltaTime);
            }

            _buffer.Clear();
        }

        private void TickGuard(in GarrisonRoster.Post post, float deltaTime)
        {
            UnitEntity unit = post.Unit;
            CapturePointBehaviour point = post.Point;

            if (unit == null || !unit.IsAlive || point == null)
                return;

            unit.ServerTickTimers(deltaTime);

            Vector3 center = point.transform.position;
            Vector3 home = GarrisonLayout.SlotPosition(center, point.GarrisonRingRadius, post.SlotIndex, point.MaxGuards);

            // Слот пересчитывается каждый такт, а не запоминается при спавне: точка — обычный
            // объект сцены, её можно двигать во время отладки, и гарнизон должен ехать за ней.
            unit.AssignFormationSlot(post.SlotIndex, home);

            GuardRoutine.Tick(unit, home, center, _context, deltaTime);
        }
    }
}
