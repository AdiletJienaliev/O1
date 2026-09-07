using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// «Защита» (ГДД §6): армия встаёт стеной щитов. Юниты занимают слоты, разворачиваются
    /// по фронту построения и больше не сходят с места ни за кем — ни за целью, ни за обидчиком.
    /// Пока щит поднят, удар во фронтальный сектор гасится целиком (см. <see cref="ShieldBlock"/>).
    ///
    /// Приказ намеренно неподвижный. Вся его цена — потерянный темп: строй, вставший в защиту,
    /// не наступает и не преследует, и обойти его с фланга — обычное дело, а не эксплойт.
    /// Разворачиваться за атакующим значило бы сделать стену непробиваемой вообще.
    /// </summary>
    public sealed class DefendBehaviour : IUnitOrderBehaviour
    {
        public ArmyOrderType OrderType => ArmyOrderType.Defend;

        public void Tick(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            CommandConfig command = context.Config.Command;
            float tolerance = command != null ? command.slotArriveTolerance : 0.35f;

            // Щит поднимается только в строю. На марше к слоту юнит открыт — иначе армию
            // можно было бы водить по карте под непробиваемым фронтом.
            bool inSlot = unit.Locomotion == null || unit.Locomotion.HasArrived(unit.FormationSlot, tolerance);
            unit.ServerSetShield(inSlot);

            if (!inSlot)
            {
                UnitCombatRoutine.ReturnToSlot(unit, army, context, deltaTime);
                return;
            }

            unit.Locomotion?.Stop();

            if (unit.Locomotion != null)
                unit.Locomotion.FacesMovement = false;

            float defendRadius = command != null ? command.defendRadius : 6f;
            ICombatTarget target = UnitCombatRoutine.AcquireTarget(unit, context, defendRadius);

            // Щитоносец бьёт из-за щита, не разворачиваясь. Лучник и маг щита не держат,
            // и заставлять их стрелять затылком вперёд не за чем — они целятся как обычно.
            bool holdsLine = unit.Stats.HasShield;

            // Результат удара не важен: не достал — значит не достал, догонять всё равно нельзя.
            if (target != null)
                UnitCombatRoutine.TryEngage(unit, target, context, deltaTime, faceTarget: !holdsLine);

            if (holdsLine || target == null)
                FaceFront(unit, army, deltaTime);
        }

        /// <summary>Разворот по фронту построения: вся стена смотрит в одну сторону.</summary>
        private static void FaceFront(UnitEntity unit, ArmyController army, float deltaTime)
        {
            if (unit.Locomotion == null || army == null)
                return;

            army.ResolveAnchor(out _, out float yaw);

            Vector3 front = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            unit.Locomotion.FaceTowards(unit.Position + front, deltaTime);
        }
    }
}
