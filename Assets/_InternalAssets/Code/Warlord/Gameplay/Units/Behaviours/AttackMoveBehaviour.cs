using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// «В атаку» (ГДД §6): армия дерётся там, где стоит полководец. Зону боя и её центр
    /// считает <see cref="ArmyEngagementSystem"/> один раз на всю армию, а юнит уже внутри
    /// этой зоны берёт ближайшего к себе противника — так строй не растаскивает по карте
    /// каждый, кто заметил врага у своего фланга.
    ///
    /// Целей в зоне не осталось — юнит возвращается в слот; сам приказ через пару секунд
    /// переключит на «За мной» та же система.
    /// </summary>
    public sealed class AttackMoveBehaviour : IUnitOrderBehaviour
    {
        public ArmyOrderType OrderType => ArmyOrderType.AttackMove;

        public void Tick(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            if (army.IsEngaged)
            {
                ICombatTarget target = UnitCombatRoutine.AcquireTargetInZone(
                    unit,
                    context,
                    army.EngagementCenter,
                    army.EngagementRadius);

                if (target != null)
                {
                    if (UnitCombatRoutine.TryEngage(unit, target, context, deltaTime))
                        return;

                    UnitCombatRoutine.MoveTowards(unit, army, context, target.Position, deltaTime);
                    return;
                }
            }

            // Драться не с кем — держим строй у точки приказа и ждём решения системы боя.
            unit.SetTarget(null, 0f);
            UnitCombatRoutine.ReturnToSlot(unit, army, context, deltaTime);
        }
    }
}
