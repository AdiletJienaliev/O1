using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Combat;
using Warlord.Domain.Stats;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// Общие кирпичики для всех поведений: выбор цели, атака, движение к слоту.
    /// Вынесены отдельно, чтобы правила «одна цель за удар» и «урон в конце такта»
    /// не расползались по трём реализациям.
    /// </summary>
    public static class UnitCombatRoutine
    {
        /// <summary>Ищет цель с учётом кулдауна переключения (ГДД §6, targetSwitchCooldown).</summary>
        public static ICombatTarget AcquireTarget(UnitEntity unit, IMatchContext context, float searchRadius)
        {
            unit.ClearTargetIfDead();

            ICombatTarget current = unit.CurrentTarget;
            if (current != null && current.IsAlive && !unit.CanSwitchTarget)
                return current;

            CommandConfig command = context.Config.Command;
            ICombatTarget found = context.Targeting.FindNearestEnemy(unit.Position, unit.OwnerSlot, searchRadius);

            if (found != current)
                unit.SetTarget(found, command != null ? command.targetSwitchCooldown : 1f);

            return found;
        }

        /// <summary>
        /// Пытается ударить цель. Возвращает true, если юнит в дистанции и занят боем —
        /// в этом случае двигаться ему уже не нужно.
        /// </summary>
        public static bool TryEngage(UnitEntity unit, ICombatTarget target, IMatchContext context, float deltaTime)
        {
            if (target == null || !target.IsAlive)
                return false;

            UnitStats stats = unit.Stats;
            float reach = stats.AttackRange + target.Radius;

            Vector3 delta = target.Position - unit.Position;
            delta.y = 0f;

            if (delta.sqrMagnitude > reach * reach)
                return false;

            context.Targeting.ReportAttackerOn(target);

            unit.Locomotion?.Stop();
            unit.Locomotion?.FaceTowards(target.Position, deltaTime);

            if (!unit.CanAttack)
                return true;

            unit.ConsumeAttackCooldown();

            // Юнит наносит урон ровно одной цели за удар — никакого AoE (ГДД §5.1).
            if (stats.IsRanged)
                context.Projectiles.Launch(unit, target, stats.DamagePerHit, stats.ProjectileSpeed);
            else
                context.Damage.Enqueue(unit, target, stats.DamagePerHit);

            return true;
        }

        /// <summary>Движение к точке с дросселем перепрокладки пути.</summary>
        public static void MoveTowards(UnitEntity unit, IMatchContext context, Vector3 destination, float speedMultiplier = 1f)
        {
            CommandConfig command = context.Config.Command;
            float tolerance = command != null ? command.slotArriveTolerance : 0.35f;

            if (unit.Locomotion == null)
                return;

            if (unit.Locomotion.HasArrived(destination, tolerance))
            {
                unit.Locomotion.Stop();
                return;
            }

            float interval = command != null ? command.repathInterval : 0.4f;
            if (unit.TryConsumeRepath(interval))
                unit.Locomotion.MoveTo(destination, speedMultiplier);
        }

        /// <summary>Возврат в свой слот построения с бонусом скорости для отставших.</summary>
        public static void ReturnToSlot(UnitEntity unit, ArmyController army, IMatchContext context)
        {
            CommandConfig command = context.Config.Command;
            float rebuildSpeed = command != null ? command.formationRebuildSpeed : 1f;
            MoveTowards(unit, context, unit.FormationSlot, rebuildSpeed);
        }
    }
}
