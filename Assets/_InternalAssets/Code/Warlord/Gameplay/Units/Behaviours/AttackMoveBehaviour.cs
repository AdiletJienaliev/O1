using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// «В атаку» (ГДД §6): юнит ищет ближайшую цель в своём радиусе агро и атакует.
    /// Нет цели — идёт к своему слоту у точки приказа и продолжает сканировать.
    /// </summary>
    public sealed class AttackMoveBehaviour : IUnitOrderBehaviour
    {
        public ArmyOrderType OrderType => ArmyOrderType.AttackMove;

        public void Tick(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            ICombatTarget target = UnitCombatRoutine.AcquireTarget(unit, context, unit.Stats.AggroRadius);

            if (target != null)
            {
                if (UnitCombatRoutine.TryEngage(unit, target, context, deltaTime))
                    return;

                UnitCombatRoutine.MoveTowards(unit, context, target.Position);
                return;
            }

            UnitCombatRoutine.ReturnToSlot(unit, army, context);
        }
    }
}
