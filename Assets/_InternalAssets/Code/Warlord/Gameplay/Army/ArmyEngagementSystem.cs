using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Army
{
    /// <summary>
    /// Кого и где бьёт армия под приказом «В атаку» (ГДД §6). Зона боя одна на всю армию
    /// и привязана к полководцу: цель ищется рядом с ним, а не рядом с каждым юнитом —
    /// иначе крайний лучник видел патруль на фланге и уводил за собой весь строй.
    ///
    /// Здесь же армия сама возвращается в строй: когда в зоне не осталось живых врагов,
    /// приказ через несколько секунд переключается на «За мной». Задержка нужна, чтобы
    /// приказ, отданный на подходе к противнику, не отменялся в тот же такт.
    /// </summary>
    public sealed class ArmyEngagementSystem : IServerSystem
    {
        private readonly IMatchContext _context;
        private readonly float[] _emptySince;

        public ArmyEngagementSystem(IMatchContext context)
        {
            _context = context;
            _emptySince = new float[PlayerSlots.MaxSupported];
        }

        public int Order => ServerSystemOrder.ArmyEngagement;

        public void Tick(float deltaTime)
        {
            CommandConfig command = _context.Config.Command;
            float radius = command != null ? command.attackAggroRadius : 15f;
            float regroupDelay = command != null ? command.attackRegroupDelay : 2f;

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                if (player == null || player.IsEliminated || player.Army == null)
                    continue;

                ArmyController army = player.Army;

                if (army.Order.Type != ArmyOrderType.AttackMove)
                {
                    army.SetEngagement(false, army.Order.AnchorPosition, radius);
                    ResetTimer(player.Slot);
                    continue;
                }

                Vector3 center = ResolveCenter(army);
                bool hasTargets = _context.Targeting.FindNearestEnemyInZone(center, center, radius, player.Slot) != null;

                army.SetEngagement(hasTargets, center, radius);

                if (hasTargets)
                {
                    ResetTimer(player.Slot);
                    continue;
                }

                if (!Accumulate(player.Slot, deltaTime, regroupDelay))
                    continue;

                // Бить больше некого — армия возвращается к полководцу сама (ГДД §6).
                ResetTimer(player.Slot);
                player.ServerSetOrder(new ArmyOrder(ArmyOrderType.FollowLeader, center, ResolveYaw(army)));
            }
        }

        /// <summary>Центр зоны: живой полководец, иначе точка, где приказ был отдан.</summary>
        private static Vector3 ResolveCenter(ArmyController army)
        {
            IArmyLeader leader = army.Leader;
            return leader != null && leader.IsAlive ? leader.Position : army.Order.AnchorPosition;
        }

        private static float ResolveYaw(ArmyController army)
        {
            IArmyLeader leader = army.Leader;
            return leader != null && leader.IsAlive ? leader.YawDegrees : army.Order.AnchorYaw;
        }

        private void ResetTimer(int slot)
        {
            if (PlayerSlots.IsValid(slot))
                _emptySince[slot] = 0f;
        }

        /// <summary>Копит время без целей. Возвращает true один раз, когда задержка вышла.</summary>
        private bool Accumulate(int slot, float deltaTime, float delay)
        {
            if (!PlayerSlots.IsValid(slot))
                return false;

            _emptySince[slot] += deltaTime;
            return _emptySince[slot] >= delay;
        }
    }
}
