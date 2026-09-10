using FishNet.Connection;
using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Bots;
using Warlord.Domain.Combat;
using Warlord.Domain.Match;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Match
{
    /// <summary>
    /// Регистрация игроков, разбор смертей и завершение матча.
    /// Здесь сходятся события, которые затрагивают сразу несколько подсистем.
    /// </summary>
    public sealed partial class MatchManager
    {
        /// <summary>Регистрация игрока в матче. Вызывается спавнером после создания PlayerState.</summary>
        public void ServerRegisterPlayer(PlayerState player, HeroController hero)
        {
            if (!IsServerInitialized || ServerContext == null || player == null)
                return;

            ServerContext.Players.Add(player);

            if (hero != null)
            {
                player.Hero = hero;
                player.Army.Leader = hero;

                if (hero.TryGetComponent(out HeroCombat combat))
                    combat.ServerInitialize(ServerContext);
            }

            Events.RaisePlayerJoined(player.Slot);
        }

        /// <summary>
        /// Регистрация бота. Отдельный вход, потому что боту вдобавок к объектам игрока
        /// нужен собственный мозг и место в системе ботов — но всё остальное у него
        /// ровно такое же, как у живого: тот же PlayerState, тот же полководец, те же правила.
        /// </summary>
        public BotPlayer ServerRegisterBot(PlayerState player, HeroController hero, in BotProfile profile, BotHeroPilot pilot)
        {
            if (!IsServerInitialized || ServerContext == null || player == null || Bots == null)
                return null;

            ServerRegisterPlayer(player, hero);

            player.IncomeScale = profile.IncomeMultiplier;

            BotPlayer bot = new(ServerContext, player, hero, in profile, pilot);
            Bots.Add(bot);

            return bot;
        }

        public void ServerUnregisterPlayer(PlayerState player)
        {
            if (ServerContext == null || player == null)
                return;

            Bots?.Remove(player.Slot);

            // Отключение равносильно выбыванию: армия распускается, счёт остаётся в таблице.
            player.ServerEliminate(EliminationReason.Disconnected);
            ServerContext.Players.Remove(player);
        }

        private void OnTargetKilled(ICombatTarget victim, ICombatTarget killer)
        {
            if (victim is UnitEntity unit)
            {
                OnUnitKilled(unit, killer);
                return;
            }

            // Смерть полководца обрабатывает он сам: событие уже поднято в ReceiveDamage,
            // а респавн отсчитывает HeroLifecycleSystem.
        }

        private void OnUnitKilled(UnitEntity unit, ICombatTarget killer)
        {
            int ownerSlot = unit.OwnerSlot;
            int killerSlot = killer != null ? killer.OwnerSlot : PlayerSlots.None;

            PlayerState owner = ServerContext.Players.Get(ownerSlot);
            owner?.ServerNotifyUnitLost(unit);

            if (ServerContext.Teams.AreEnemies(killerSlot, ownerSlot))
            {
                ServerContext.Scores.AddUnitKill(killerSlot);

                int bounty = config.GameMode.goldPerUnitKill;
                if (bounty > 0)
                    ServerContext.Players.Get(killerSlot)?.Wallet.AddGold(bounty);
            }

            Events.RaiseUnitKilled(ownerSlot, killerSlot);

            // Смерть освобождает слот лимита сразу, золото не возвращается (ГДД §5.1).
            ServerContext.Units.Despawn(unit);
        }

        private void OnMatchResolved(MatchOutcome outcome)
        {
            SetPhase(MatchPhase.Finished);
            Events.RaiseMatchFinished(in outcome);
            ObserversMatchFinished((sbyte)outcome.WinnerSlot, (byte)outcome.Reason);
        }

        [FishNet.Object.ObserversRpc(BufferLast = true)]
        private void ObserversMatchFinished(sbyte winnerSlot, byte reason)
        {
            if (IsServerInitialized)
                return;

            Events.RaiseMatchFinished(new MatchOutcome(winnerSlot, (MatchEndReason)reason));
        }
    }
}
