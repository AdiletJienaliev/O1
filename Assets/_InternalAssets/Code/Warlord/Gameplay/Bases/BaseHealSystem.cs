using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Bases
{
    /// <summary>
    /// Регенерация юнитов у своей базы (ГДД §5.1): вне базы юниты не лечатся,
    /// в радиусе baseHealRadius восстанавливают baseHealPerSecond.
    /// </summary>
    public sealed class BaseHealSystem : IServerSystem
    {
        private readonly IMatchContext _context;

        public BaseHealSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Regeneration;

        public void Tick(float deltaTime)
        {
            GameModeConfig mode = _context.Config.GameMode;
            if (mode.baseHealPerSecond <= 0f || mode.baseHealRadius <= 0f)
                return;

            float sqrRadius = mode.baseHealRadius * mode.baseHealRadius;
            float healAmount = mode.baseHealPerSecond * deltaTime;

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int p = 0; p < players.Count; p++)
            {
                PlayerState player = players[p];
                if (player == null || player.IsEliminated || player.Army == null)
                    continue;

                Vector3 baseCenter = _context.Players.GetBaseAnchor(player.Slot).Center;
                IReadOnlyList<UnitEntity> units = player.Army.Units;

                for (int u = 0; u < units.Count; u++)
                {
                    UnitEntity unit = units[u];
                    if (unit == null || !unit.IsAlive)
                        continue;

                    Vector3 delta = unit.Position - baseCenter;
                    delta.y = 0f;

                    if (delta.sqrMagnitude <= sqrRadius)
                        unit.ServerHeal(healAmount);
                }
            }
        }
    }
}
