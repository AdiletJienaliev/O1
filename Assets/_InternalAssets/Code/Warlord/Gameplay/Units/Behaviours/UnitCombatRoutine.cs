using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Domain.Stats;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Combat;
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
            if (current.IsAliveTarget() && !unit.CanSwitchTarget)
                return current;

            CommandConfig command = context.Config.Command;
            ICombatTarget found = context.Targeting.FindNearestEnemy(unit.Position, unit.OwnerSlot, searchRadius);

            if (found != current)
                unit.SetTarget(found, command != null ? command.targetSwitchCooldown : 1f);

            return found;
        }

        /// <summary>
        /// Ищет цель внутри чужой зоны боя: ближайшую к самому юниту, но только среди тех,
        /// кто стоит рядом с полководцем. Так приказ «В атаку» бьёт по тому противнику,
        /// которого видит игрок, а не по случайному встречному у фланга строя (ГДД §6).
        /// </summary>
        public static ICombatTarget AcquireTargetInZone(
            UnitEntity unit,
            IMatchContext context,
            Vector3 zoneCenter,
            float zoneRadius)
        {
            unit.ClearTargetIfDead();

            ICombatTarget current = unit.CurrentTarget;
            if (current.IsAliveTarget() && !unit.CanSwitchTarget)
                return current;

            CommandConfig command = context.Config.Command;
            ICombatTarget found = context.Targeting.FindNearestEnemyInZone(
                unit.Position,
                zoneCenter,
                zoneRadius,
                unit.OwnerSlot);

            if (found != current)
                unit.SetTarget(found, command != null ? command.targetSwitchCooldown : 1f);

            return found;
        }

        /// <summary>
        /// Пытается ударить цель. Возвращает true, если юнит в дистанции и занят боем —
        /// в этом случае двигаться ему уже не нужно.
        /// </summary>
        /// <param name="faceTarget">
        /// Доворачивать ли корпус к цели. Стена щитов держит фронт и не поворачивается
        /// за каждым, кто её ковыряет: иначе обойти строй было бы нельзя в принципе —
        /// щит всегда оказывался бы там, откуда бьют.
        /// </param>
        public static bool TryEngage(
            UnitEntity unit,
            ICombatTarget target,
            IMatchContext context,
            float deltaTime,
            bool faceTarget = true)
        {
            if (!target.IsAliveTarget())
                return false;

            UnitStats stats = unit.Stats;
            float reach = stats.AttackRange + target.Radius;

            Vector3 delta = target.Position - unit.Position;
            delta.y = 0f;

            if (delta.sqrMagnitude > reach * reach)
                return false;

            context.Targeting.ReportAttackerOn(target);

            unit.Locomotion?.Stop();

            // В бою корпусом распоряжаемся сами: агент довернул бы юнита по остаточной
            // скорости и тот бил бы мимо, глядя туда, откуда только что прибежал.
            if (unit.Locomotion != null)
                unit.Locomotion.FacesMovement = false;

            if (faceTarget)
                unit.Locomotion?.FaceTowards(target.Position, deltaTime);

            if (!unit.CanAttack)
                return true;

            unit.ConsumeAttackCooldown();
            unit.ServerNotifyAttack();

            // Обычный юнит бьёт ровно одну цель (ГДД §5.1); у мага ненулевой splashRadius,
            // и тогда тот же удар накрывает всех врагов вокруг цели.
            if (stats.IsRanged)
            {
                float flightTime = context.Projectiles.Launch(
                    unit,
                    target,
                    stats.DamagePerHit,
                    stats.ProjectileSpeed,
                    stats.SplashRadius,
                    stats.SplashDamageFactor);

                // Стрелу клиентам заводит сам юнит: ей нужны и цель, и то же время полёта,
                // по которому сервер начислит урон, иначе попадание разойдётся с уроном.
                if (flightTime > 0f)
                    unit.ServerNotifyProjectile(target, flightTime);
            }
            else
            {
                SplashDamage.Apply(
                    context.Damage,
                    context.Targeting,
                    unit,
                    target,
                    stats.DamagePerHit,
                    stats.SplashRadius,
                    stats.SplashDamageFactor);
            }

            return true;
        }

        /// <summary>
        /// Движение к точке с дросселем перепрокладки пути и выбором походки.
        ///
        /// Идти далеко — юнит бежит и разворачивается по движению. Осталось близко —
        /// переходит на шаг и корпус больше не крутит: он смотрит на противника, а если
        /// драться не с кем, то в сторону фронта построения, и доходит приставным шагом.
        /// Именно поэтому строй встаёт лицом в одну сторону, а не разворачивается спиной
        /// к врагу, дошагивая последние полметра до своего слота.
        /// </summary>
        public static void MoveTowards(
            UnitEntity unit,
            ArmyController army,
            IMatchContext context,
            Vector3 destination,
            float deltaTime,
            float speedMultiplier = 1f)
        {
            IUnitLocomotion locomotion = unit.Locomotion;
            if (locomotion == null)
                return;

            CommandConfig command = context.Config.Command;
            float tolerance = command != null ? command.slotArriveTolerance : 0.35f;

            if (locomotion.HasArrived(destination, tolerance))
            {
                locomotion.Stop();
                locomotion.FacesMovement = false;
                HoldFacing(unit, army, context, deltaTime);
                return;
            }

            Vector3 delta = destination - unit.Position;
            delta.y = 0f;

            Gait gait = delta.sqrMagnitude > unit.Stats.RunDistance * unit.Stats.RunDistance
                ? Gait.Run
                : Gait.Walk;

            locomotion.Gait = gait;
            locomotion.FacesMovement = gait == Gait.Run;

            if (gait == Gait.Walk)
                HoldFacing(unit, army, context, deltaTime);

            float interval = command != null ? command.repathInterval : 0.4f;
            if (unit.TryConsumeRepath(interval))
                locomotion.MoveTo(destination, speedMultiplier);
        }

        /// <summary>Возврат в свой слот построения с бонусом скорости для отставших.</summary>
        public static void ReturnToSlot(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            CommandConfig command = context.Config.Command;
            float rebuildSpeed = command != null ? command.formationRebuildSpeed : 1f;
            MoveTowards(unit, army, context, unit.FormationSlot, deltaTime, rebuildSpeed);
        }

        /// <summary>
        /// Куда юнит смотрит, когда корпусом не распоряжается навигация: на свою цель,
        /// а без цели — вдоль фронта построения, туда же, куда развёрнут весь строй.
        /// </summary>
        public static void HoldFacing(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            IUnitLocomotion locomotion = unit.Locomotion;
            if (locomotion == null)
                return;

            ICombatTarget target = unit.CurrentTarget;

            if (target.IsAliveTarget())
            {
                locomotion.FaceTowards(target.Position, deltaTime);
                return;
            }

            if (army == null)
                return;

            army.ResolveAnchor(out _, out float yaw);

            Vector3 front = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            locomotion.FaceTowards(unit.Position + front, deltaTime);
        }
    }
}
