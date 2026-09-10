using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Лечащая аура улучшения «Кузница» (ГДД §2.5). Отдельно от <see cref="Bases.BaseHealSystem"/>,
    /// потому что источник другой: там лечит своя база, здесь — конкретная точка с конкретным
    /// улучшением, и включается аура только пока точка удерживается.
    ///
    /// Обход идёт по точкам, а не по игрокам: кузниц на карте единицы, и перебирать по ним
    /// дешевле, чем каждому юниту каждый такт спрашивать, нет ли рядом лечащего аванпоста.
    /// </summary>
    public sealed class OutpostHealSystem : IServerSystem
    {
        private readonly IMatchContext _context;
        private readonly List<ICombatTarget> _buffer = new(64);

        public OutpostHealSystem(IMatchContext context) => _context = context;

        public int Order => ServerSystemOrder.Regeneration;

        public void Tick(float deltaTime)
        {
            if (_context.Phase != MatchPhase.Running)
                return;

            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            for (int i = 0; i < points.Count; i++)
                TickPoint(points[i], deltaTime);
        }

        private void TickPoint(CapturePointBehaviour point, float deltaTime)
        {
            if (point == null || !PlayerSlots.IsValid(point.OwnerSlot))
                return;

            OutpostUpgradeConfig upgrade = point.Upgrade;

            if (upgrade == null || upgrade.healPerSecond <= 0f || upgrade.healRadius <= 0f)
                return;

            _context.Targeting.CollectInRadius(
                point.transform.position,
                upgrade.healRadius,
                CombatantKind.Unit,
                _buffer);

            float amount = upgrade.healPerSecond * deltaTime;

            for (int i = 0; i < _buffer.Count; i++)
            {
                // Лечим только свою сторону: кузница держит фронт владельца и его союзников,
                // а не чинит тех, кто пришёл её отбивать.
                if (!_context.Teams.SameSide(_buffer[i].OwnerSlot, point.OwnerSlot))
                    continue;

                if (_buffer[i] is UnitEntity unit)
                    unit.ServerHeal(amount);
            }

            _buffer.Clear();
        }
    }
}
