using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Capture;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// Всё поведение охранника (ГДД §1.4) одним проходом. Это не <see cref="IUnitOrderBehaviour"/>
    /// намеренно: тот интерфейс описывает, что юнит делает по приказу, а охранник приказов
    /// не получает вообще — у него нет ни армии, ни построения, ни точки приказа.
    ///
    /// Таблица из ГДД §1.4 читается здесь сверху вниз:
    /// нет врагов — стоит в слоте лицом наружу; враг в агро — бьёт, но не дальше поводка;
    /// цель ушла — забывает её и возвращается ускоренно; вне боя лечится.
    /// </summary>
    public static class GuardRoutine
    {
        /// <param name="home">Слот кольца гарнизона — единственная точка, к которой охранник привязан.</param>
        /// <param name="center">Центр точки захвата: по нему считается разворот наружу.</param>
        public static void Tick(
            UnitEntity unit,
            Vector3 home,
            Vector3 center,
            IMatchContext context,
            float deltaTime)
        {
            UnitConfig config = unit.Config;

            // Щит охранник держит опущенным: приказа «Защита» ему никто не отдаёт, а поднятый
            // навсегда щит превратил бы точку в неберущуюся спереди стену.
            unit.ServerSetShield(false);

            float aggro = unit.Stats.AggroRadius;
            ICombatTarget target = UnitCombatRoutine.AcquireTarget(unit, context, aggro);

            if (target != null && WithinLeash(unit, target, home, config))
            {
                if (!UnitCombatRoutine.TryEngage(unit, target, context, deltaTime))
                    MoveToTarget(unit, context, target.Position, deltaTime);

                return;
            }

            // Цель увела бы за поводок — забываем её сразу, а не после того, как охранник
            // отойдёт. Иначе гарнизон растаскивают по одному, подставляя приманку.
            if (target != null)
                unit.SetTarget(null, 0f);

            ReturnHome(unit, home, center, context, deltaTime);
            Regenerate(unit, home, config, context, deltaTime);
        }

        /// <summary>
        /// Поводок считается от слота, а не от текущей позиции: иначе охранник уползал бы
        /// за целью по чуть-чуть, каждый раз оставаясь «в поводке» относительно себя самого.
        /// </summary>
        private static bool WithinLeash(UnitEntity unit, ICombatTarget target, Vector3 home, UnitConfig config)
        {
            Vector3 delta = target.Position - home;
            delta.y = 0f;

            float leash = config != null ? config.garrisonLeash : 8f;
            float allowed = leash + unit.Stats.AttackRange + target.Radius;

            return delta.sqrMagnitude <= allowed * allowed;
        }

        private static void MoveToTarget(UnitEntity unit, IMatchContext context, Vector3 destination, float deltaTime)
        {
            IUnitLocomotion locomotion = unit.Locomotion;
            if (locomotion == null)
                return;

            CommandConfig command = context.Config.Command;
            float tolerance = command != null ? command.slotArriveTolerance : 0.35f;

            if (locomotion.HasArrived(destination, tolerance))
            {
                locomotion.Stop();
                return;
            }

            Vector3 delta = destination - unit.Position;
            delta.y = 0f;

            bool running = delta.sqrMagnitude > unit.Stats.RunDistance * unit.Stats.RunDistance;

            locomotion.Gait = running ? Warlord.Core.Gait.Run : Warlord.Core.Gait.Walk;
            locomotion.FacesMovement = running;

            if (!running)
                locomotion.FaceTowards(destination, deltaTime);

            float interval = command != null ? command.repathInterval : 0.4f;
            if (unit.TryConsumeRepath(interval))
                locomotion.MoveTo(destination, 1f);
        }

        /// <summary>Возврат в слот с бонусом скорости и разворотом наружу по прибытии.</summary>
        private static void ReturnHome(
            UnitEntity unit,
            Vector3 home,
            Vector3 center,
            IMatchContext context,
            float deltaTime)
        {
            IUnitLocomotion locomotion = unit.Locomotion;
            if (locomotion == null)
                return;

            CommandConfig command = context.Config.Command;
            float tolerance = command != null ? command.slotArriveTolerance : 0.35f;

            if (locomotion.HasArrived(home, tolerance))
            {
                locomotion.Stop();
                locomotion.FacesMovement = false;

                // Лицом наружу от центра точки: охранник встречает того, кто подходит,
                // а не разглядывает флаг, который и так никуда не денется.
                Vector3 outward = GarrisonLayout.SlotFacing(center, home);
                locomotion.FaceTowards(unit.Position + outward, deltaTime);
                return;
            }

            Vector3 delta = home - unit.Position;
            delta.y = 0f;

            bool running = delta.sqrMagnitude > unit.Stats.RunDistance * unit.Stats.RunDistance;

            locomotion.Gait = running ? Warlord.Core.Gait.Run : Warlord.Core.Gait.Walk;
            locomotion.FacesMovement = running;

            if (!running)
            {
                Vector3 outward = GarrisonLayout.SlotFacing(center, home);
                locomotion.FaceTowards(unit.Position + outward, deltaTime);
            }

            float interval = command != null ? command.repathInterval : 0.4f;
            float speed = unit.Config != null ? unit.Config.garrisonReturnSpeed : 1.3f;

            if (unit.TryConsumeRepath(interval))
                locomotion.MoveTo(home, speed);
        }

        /// <summary>
        /// Регенерация в своём слоте вне боя (ГДД §1.2). Два условия сразу: охранник должен
        /// и стоять на месте, и достаточно давно не участвовать в драке — иначе отступивший
        /// на шаг за угол гарнизон лечился бы прямо в бою.
        /// </summary>
        private static void Regenerate(
            UnitEntity unit,
            Vector3 home,
            UnitConfig config,
            IMatchContext context,
            float deltaTime)
        {
            if (config == null || config.garrisonRegenPerSecond <= 0f)
                return;

            if (unit.TimeSinceCombat < config.garrisonRegenCombatDelay)
                return;

            CommandConfig command = context.Config.Command;
            float tolerance = command != null ? command.slotArriveTolerance : 0.35f;

            Vector3 delta = unit.Position - home;
            delta.y = 0f;

            if (delta.sqrMagnitude > tolerance * tolerance)
                return;

            unit.ServerHeal(config.garrisonRegenPerSecond * deltaTime);
        }
    }
}
