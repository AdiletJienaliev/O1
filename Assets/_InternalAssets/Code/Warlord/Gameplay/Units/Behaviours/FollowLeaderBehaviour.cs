using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units.Behaviours
{
    /// <summary>
    /// «За мной» (ГДД §6): якорь построения привязан к полководцу, строй не ломается.
    /// Юниты бьют только тех, кто сам их ударил — цель не ищется активно.
    /// </summary>
    public sealed class FollowLeaderBehaviour : IUnitOrderBehaviour
    {
        public ArmyOrderType OrderType => ArmyOrderType.FollowLeader;

        public void Tick(UnitEntity unit, ArmyController army, IMatchContext context, float deltaTime)
        {
            CommandConfig command = context.Config.Command;
            bool retaliateOnly = command == null || command.retaliateOnFollow;

            ICombatTarget target = retaliateOnly
                ? unit.LastAttacker
                : UnitCombatRoutine.AcquireTarget(unit, context, unit.Stats.AggroRadius);

            if (target != null && target.IsAlive)
            {
                // Отвечаем, только если обидчик сам подошёл на дистанцию удара:
                // строй важнее размена, за противником никто не бежит.
                if (UnitCombatRoutine.TryEngage(unit, target, context, deltaTime))
                    return;
            }

            UnitCombatRoutine.ReturnToSlot(unit, army, context);
        }
    }
}
