using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Formations;
using Warlord.Domain.Match;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.World;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Клиентские команды и их серверная валидация (ГДД §12).
    /// Правило простое: клиент только просит, решает сервер. Каждая проверка
    /// возвращает конкретную причину отказа, чтобы UI мог объяснить игроку, что не так.
    /// </summary>
    public sealed partial class PlayerState
    {
        #region Покупка юнита

        [ServerRpc]
        public void CmdPurchaseUnit(byte rosterIndex)
        {
            CommandRejection rejection = ValidatePurchase(rosterIndex, out UnitConfig unit, out int cost);

            if (rejection != CommandRejection.None)
            {
                RejectCommand(rejection);
                return;
            }

            Wallet.TrySpendGold(cost);
            BuildQueue.Enqueue(rosterIndex);
            PublishWallet();
        }

        private CommandRejection ValidatePurchase(byte rosterIndex, out UnitConfig unit, out int cost)
        {
            unit = null;
            cost = 0;

            if (_context.Phase != MatchPhase.Running)
                return CommandRejection.MatchNotRunning;

            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            UnitRosterConfig roster = _context.Config.Roster;
            if (!roster.IsValidIndex(rosterIndex))
                return CommandRejection.UnknownUnit;

            unit = roster.Get(rosterIndex);

            // Покупка возможна только когда полководец стоит в зоне покупки своей базы (ГДД §5.1).
            if (!IsHeroInsideBuyZone())
                return CommandRejection.NotInBuyZone;

            MatchSettings settings = _context.Settings;
            cost = settings.ResolveUnitCost(unit, _context.Config.GameMode);

            if (Wallet.Gold < cost)
                return CommandRejection.NotEnoughGold;

            // Лимит считаем вместе с очередью, иначе можно назаказывать сверх лимита в один клик.
            if (Army.AliveCount + BuildQueue.PendingCount >= UnitCap)
                return CommandRejection.ArmyLimitReached;

            return CommandRejection.None;
        }

        private bool IsHeroInsideBuyZone()
        {
            if (Hero == null || !Hero.IsAlive)
                return false;

            PlayerBaseAnchor anchor = _context.Players.GetBaseAnchor(Slot);
            float radius = anchor.BuyZoneRadius;

            Vector3 delta = Hero.Position - anchor.Center;
            delta.y = 0f;

            return delta.sqrMagnitude <= radius * radius;
        }

        #endregion

        #region Приказы армии

        [ServerRpc]
        public void CmdSetOrder(byte orderType, Vector3 anchorPosition, float anchorYaw)
        {
            if (_context.Phase != MatchPhase.Running)
            {
                RejectCommand(CommandRejection.MatchNotRunning);
                return;
            }

            if (IsEliminated)
            {
                RejectCommand(CommandRejection.PlayerEliminated);
                return;
            }

            if (!System.Enum.IsDefined(typeof(ArmyOrderType), orderType))
            {
                RejectCommand(CommandRejection.UnknownFormation);
                return;
            }

            // Точку приказа клиент шлёт сам, поэтому её обязательно проверяем по арене.
            if (!MatchArena.Contains(anchorPosition))
            {
                RejectCommand(CommandRejection.OutOfBounds);
                return;
            }

            ServerSetOrder(new ArmyOrder((ArmyOrderType)orderType, anchorPosition, anchorYaw));
        }

        /// <summary>
        /// Пользовательская расстановка армии. Приходит от владельца целиком и редко —
        /// один раз при настройке, поэтому шлём массивом, а не дельтами.
        /// </summary>
        [ServerRpc]
        public void CmdSetArmyPreset(byte[] packed)
        {
            if (IsEliminated)
            {
                RejectCommand(CommandRejection.PlayerEliminated);
                return;
            }

            // Данные пришли от клиента: и длина, и индексы типов проверяются целиком,
            // иначе подделанный пакет уронил бы решатель строя на сервере.
            ArmyPreset preset = new();

            if (!preset.Unpack(packed, _context.Config.Roster.Count))
            {
                RejectCommand(CommandRejection.UnknownFormation);
                return;
            }

            Army.ApplyPreset(preset);
        }

        [ServerRpc]
        public void CmdSetFormation(byte formationIndex)
        {
            if (IsEliminated)
            {
                RejectCommand(CommandRejection.PlayerEliminated);
                return;
            }

            if (!_context.Config.Formations.IsValidIndex(formationIndex))
            {
                RejectCommand(CommandRejection.UnknownFormation);
                return;
            }

            ServerSetFormation(formationIndex);
        }

        #endregion

        #region Древо прокачки

        [ServerRpc]
        public void CmdBuyUpgrade(byte branch)
        {
            if (IsEliminated)
            {
                RejectCommand(CommandRejection.PlayerEliminated);
                return;
            }

            if (!System.Enum.IsDefined(typeof(UpgradeBranch), branch))
            {
                RejectCommand(CommandRejection.UpgradeUnavailable);
                return;
            }

            UpgradeBranch upgradeBranch = (UpgradeBranch)branch;
            UpgradeLevels levels = Upgrades;

            CommandRejection rejection = UpgradePurchase.Validate(
                _context.Config.UpgradeTree,
                in levels,
                Wallet.XpAvailable,
                upgradeBranch,
                out UpgradeNodeConfig node);

            if (rejection != CommandRejection.None)
            {
                RejectCommand(rejection);
                return;
            }

            Wallet.TrySpendXp(node.xpCost);
            ServerApplyUpgrades(levels.With(upgradeBranch, levels.Get(upgradeBranch) + 1));
            PublishWallet();
        }

        #endregion

        private void RejectCommand(CommandRejection reason) => TargetCommandRejected(Owner, (byte)reason);

        /// <summary>
        /// Обратная связь владельцу: почему команда не прошла. Для тултипов и звука отказа.
        /// Событие поднимается на стороне клиента, поэтому берём общую шину, а не серверный контекст.
        /// </summary>
        [TargetRpc]
        private void TargetCommandRejected(NetworkConnection connection, byte reason)
        {
            MatchManager manager = MatchManager.Instance;
            if (manager != null)
                manager.Events.RaiseCommandRejected(Slot, (CommandRejection)reason);
        }
    }
}
