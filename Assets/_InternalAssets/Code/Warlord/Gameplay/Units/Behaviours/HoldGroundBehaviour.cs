using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// «Стоять» (ГДД §6): юниты замирают в слотах, отвечают врагу в defendRadius,
    /// но не отходят от слота дальше leashDistance — после чего возвращаются.
    /// </summary>
    public sealed class HoldGroundBehaviour : IUnitOrderBehaviour
    {
        public ArmyOrderType OrderType => ArmyOrderType.HoldGround;

        public void Tick(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            CommandConfig command = context.Config.Command;
            float defendRadius = command != null ? command.defendRadius : 6f;
            float leash = command != null ? command.leashDistance : 4f;

            ICombatTarget target = UnitCombatRoutine.AcquireTarget(unit, context, defendRadius);

            if (target != null)
            {
                // Поводок считаем от слота, а не от текущей позиции: иначе юнит уползал бы шагами.
                Vector3 slotToTarget = target.Position - unit.FormationSlot;
                slotToTarget.y = 0f;

                float allowed = leash + unit.Stats.AttackRange + target.Radius;
                bool withinLeash = slotToTarget.sqrMagnitude <= allowed * allowed;

                if (withinLeash)
                {
                    if (UnitCombatRoutine.TryEngage(unit, target, context, deltaTime))
                        return;

                    UnitCombatRoutine.MoveTowards(unit, context, target.Position);
                    return;
                }

                // Цель увела бы слишком далеко — забываем её и возвращаемся.
                unit.SetTarget(null, 0f);
            }

            UnitCombatRoutine.ReturnToSlot(unit, army, context);
        }
    }
}
