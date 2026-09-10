using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Gameplay.Capture;
using Warlord.Domain.Formations;
using Warlord.Domain.Match;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.World;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Команды игрока и их серверная валидация (ГДД §12).
    /// Правило простое: клиент только просит, решает сервер. Каждая проверка
    /// возвращает конкретную причину отказа, чтобы UI мог объяснить игроку, что не так.
    ///
    /// Каждая команда разбита на две части: тонкий <c>Cmd*</c> — ServerRpc, который умеет
    /// только доставить просьбу и показать отказ, — и <c>ServerTry*</c>, где живёт вся
    /// проверка и само действие. Так сделано ради ботов: бот живёт на сервере и RPC себе
    /// послать не может, но и обходить правила не должен. Он зовёт те же <c>ServerTry*</c>,
    /// поэтому «бот не стоит в зоне покупки» или «у бота не хватает золота» — это ровно
    /// тот же код, что отказывает живому игроку. Добавленное правило начинает
    /// действовать на ботов само, без единой правки в их коде.
    /// </summary>
    public sealed partial class PlayerState
    {
        #region Покупка юнита

        [ServerRpc]
        public void CmdPurchaseUnit(byte rosterIndex) => Reject(ServerTryPurchaseUnit(rosterIndex));

        /// <summary>Покупка полевого юнита на сервере. Возвращает причину отказа или None.</summary>
        public CommandRejection ServerTryPurchaseUnit(int rosterIndex)
        {
            CommandRejection rejection = ValidatePurchase(rosterIndex, out UnitConfig _, out int cost);

            if (rejection != CommandRejection.None)
                return rejection;

            Wallet.TrySpendGold(cost);
            BuildQueue.Enqueue(rosterIndex);
            PublishWallet();

            return CommandRejection.None;
        }

        /// <summary>Пройдёт ли покупка прямо сейчас. Для подсветки кнопок и для планов бота.</summary>
        public CommandRejection CanPurchaseUnit(int rosterIndex) => ValidatePurchase(rosterIndex, out _, out _);

        private CommandRejection ValidatePurchase(int rosterIndex, out UnitConfig unit, out int cost)
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

            // Охранник покупается только вместе с точкой — через CmdPurchaseGuard (ГДД §1.6).
            // Без этой проверки его можно было бы купить как полевого юнита, и он встал бы
            // на базе в слот, которого у него нет, навсегда.
            if (unit != null && unit.isGarrison)
                return CommandRejection.UnknownUnit;

            // Покупка возможна только когда полководец стоит в зоне покупки своей базы (ГДД §5.1).
            if (!HeroInBuyZone)
                return CommandRejection.NotInBuyZone;

            MatchSettings settings = _context.Settings;
            cost = settings.ResolveUnitCost(unit, _context.Config.GameMode);

            if (Wallet.Gold < cost)
                return CommandRejection.NotEnoughGold;

            // Лимит считаем вместе с очередью и гарнизоном: охранники занимают те же слоты,
            // и в этом вся механика против снежного кома (ГДД §1.1).
            if (OccupiedUnitSlots >= UnitCap)
                return CommandRejection.ArmyLimitReached;

            return CommandRejection.None;
        }

        #endregion

        #region Покупка охранника

        /// <summary>
        /// Покупка охранника на конкретную точку (ГДД §1.6). Точка едет ссылкой на сетевой объект,
        /// а не индексом: индексы точек живут только на сервере, и подделанное число попало бы
        /// в чужой гарнизон, а несуществующая ссылка приходит просто как null.
        /// </summary>
        [ServerRpc]
        public void CmdPurchaseGuard(byte rosterIndex, CapturePointBehaviour point)
        {
            Reject(ServerTryPurchaseGuard(rosterIndex, point));
        }

        /// <summary>Покупка охранника на сервере. Возвращает причину отказа или None.</summary>
        public CommandRejection ServerTryPurchaseGuard(int rosterIndex, CapturePointBehaviour point)
        {
            CommandRejection rejection = ValidateGuardPurchase(rosterIndex, point, out int cost);

            if (rejection != CommandRejection.None)
                return rejection;

            Wallet.TrySpendGold(cost);
            BuildQueue.Enqueue(rosterIndex, point, cost);
            PublishWallet();

            return CommandRejection.None;
        }

        /// <summary>Пройдёт ли покупка охранника прямо сейчас.</summary>
        public CommandRejection CanPurchaseGuard(int rosterIndex, CapturePointBehaviour point)
        {
            return ValidateGuardPurchase(rosterIndex, point, out _);
        }

        private CommandRejection ValidateGuardPurchase(int rosterIndex, CapturePointBehaviour point, out int cost)
        {
            cost = 0;

            if (_context.Phase != MatchPhase.Running)
                return CommandRejection.MatchNotRunning;

            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            UnitRosterConfig roster = _context.Config.Roster;
            if (!roster.IsValidIndex(rosterIndex))
                return CommandRejection.UnknownUnit;

            UnitConfig unit = roster.Get(rosterIndex);
            if (unit == null || !unit.isGarrison)
                return CommandRejection.UnknownUnit;

            if (point == null || point.MaxGuards <= 0)
                return CommandRejection.PointNotOwned;

            // Гарнизон ставится только на свою точку: чужую сперва надо захватить.
            if (point.OwnerSlot != Slot)
                return CommandRejection.PointNotOwned;

            if (!HeroInBuyZone)
                return CommandRejection.NotInBuyZone;

            int limit = Mathf.Max(1, Mathf.Min(point.MaxGuards, unit.maxPerPoint));

            // Считаем и стоящих, и едущих: очередь без этой проверки переполняет кольцо,
            // а лишние охранники потом отменяются с возвратом — выглядит как сбой покупки.
            if (Garrison.CountAt(point) + CountQueuedGuardsAt(point) >= limit)
                return CommandRejection.GarrisonFull;

            if (OccupiedUnitSlots >= UnitCap)
                return CommandRejection.ArmyLimitReached;

            // «Наёмники» удешевляют охранников именно на своей точке (ГДД §2.5).
            int baseCost = _context.Settings.ResolveUnitCost(unit, _context.Config.GameMode);
            cost = Warlord.Domain.Upgrades.OutpostUpgradeStack.ResolveGuardCost(baseCost, point.Upgrade);

            if (Wallet.Gold < cost)
                return CommandRejection.NotEnoughGold;

            return CommandRejection.None;
        }

        /// <summary>Сколько охранников уже едет на эту точку из очереди постройки.</summary>
        private int CountQueuedGuardsAt(CapturePointBehaviour point)
        {
            int count = 0;
            IReadOnlyList<CapturePointBehaviour> queued = BuildQueue.QueuedGuardPoints;

            for (int i = 0; i < queued.Count; i++)
            {
                if (queued[i] == point)
                    count++;
            }

            return count;
        }

        #endregion

        #region Аванпосты

        /// <summary>Выбор улучшения аванпоста (ГДД §2.5). Меняется, пока точка у игрока.</summary>
        [ServerRpc]
        public void CmdSelectOutpostUpgrade(CapturePointBehaviour point, byte upgradeIndex)
        {
            Reject(ServerTrySelectOutpostUpgrade(point, upgradeIndex));
        }

        /// <summary>Выбор улучшения аванпоста на сервере.</summary>
        public CommandRejection ServerTrySelectOutpostUpgrade(CapturePointBehaviour point, int upgradeIndex)
        {
            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            if (_context.Phase != MatchPhase.Running)
                return CommandRejection.MatchNotRunning;

            if (point == null || point.OwnerSlot != Slot)
                return CommandRejection.PointNotOwned;

            OutpostUpgradeSetConfig set = _context.Config.OutpostUpgrades;

            if (!point.HasUpgradeSlot || set == null || !set.IsValidIndex(upgradeIndex))
                return CommandRejection.UpgradeSlotUnavailable;

            point.ServerSetUpgrade(upgradeIndex);
            _context.RebuildOutpostUpgrades(Slot);

            return CommandRejection.None;
        }

        /// <summary>
        /// Переключение точки сбора (ГДД §2.6). Null — новые юниты снова идут на базу.
        /// Переключение бесплатно и доступно только в зоне покупки своей базы.
        /// </summary>
        [ServerRpc]
        public void CmdSetRallyPoint(CapturePointBehaviour point) => Reject(ServerTrySetRallyPoint(point));

        /// <summary>Переключение точки сбора на сервере.</summary>
        public CommandRejection ServerTrySetRallyPoint(CapturePointBehaviour point)
        {
            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            if (!HeroInBuyZone)
                return CommandRejection.NotInBuyZone;

            if (point == null)
            {
                ServerSetRallyPoint(null);
                return CommandRejection.None;
            }

            if (point.OwnerSlot != Slot || !point.AllowsRallyPoint)
                return CommandRejection.PointNotOwned;

            ServerSetRallyPoint(point);
            return CommandRejection.None;
        }

        /// <summary>
        /// Стоит ли полководец в зоне покупки своей базы (ГДД §5.1). Публичное:
        /// на него смотрят и валидации команд, и бот, решающий, пора ли возвращаться домой.
        /// </summary>
        public bool HeroInBuyZone
        {
            get
            {
                if (Hero == null || !Hero.IsAlive || _context == null)
                    return false;

                PlayerBaseAnchor anchor = _context.Players.GetBaseAnchor(Slot);
                float radius = anchor.BuyZoneRadius;

                Vector3 delta = Hero.Position - anchor.Center;
                delta.y = 0f;

                return delta.sqrMagnitude <= radius * radius;
            }
        }

        #endregion

        #region Приказы армии

        [ServerRpc]
        public void CmdSetOrder(byte orderType, Vector3 anchorPosition, float anchorYaw)
        {
            if (!System.Enum.IsDefined(typeof(ArmyOrderType), orderType))
            {
                Reject(CommandRejection.UnknownFormation);
                return;
            }

            Reject(ServerTrySetOrder((ArmyOrderType)orderType, anchorPosition, anchorYaw));
        }

        /// <summary>Смена приказа армии на сервере (ГДД §6).</summary>
        public CommandRejection ServerTrySetOrder(ArmyOrderType orderType, Vector3 anchorPosition, float anchorYaw)
        {
            if (_context.Phase != MatchPhase.Running)
                return CommandRejection.MatchNotRunning;

            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            // Точку приказа клиент шлёт сам, поэтому её обязательно проверяем по арене.
            if (!MatchArena.Contains(anchorPosition))
                return CommandRejection.OutOfBounds;

            ServerSetOrder(new ArmyOrder(orderType, anchorPosition, anchorYaw));
            return CommandRejection.None;
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
                Reject(CommandRejection.PlayerEliminated);
                return;
            }

            // Данные пришли от клиента: и длина, и индексы типов проверяются целиком,
            // иначе подделанный пакет уронил бы решатель строя на сервере.
            ArmyPreset preset = new();

            if (!preset.Unpack(packed, _context.Config.Roster.Count))
            {
                Reject(CommandRejection.UnknownFormation);
                return;
            }

            Army.ApplyPreset(preset);
        }

        [ServerRpc]
        public void CmdSetFormation(byte formationIndex) => Reject(ServerTrySetFormation(formationIndex));

        /// <summary>Смена построения на сервере (ГДД §7).</summary>
        public CommandRejection ServerTrySetFormation(int formationIndex)
        {
            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            if (!_context.Config.Formations.IsValidIndex(formationIndex))
                return CommandRejection.UnknownFormation;

            ServerSetFormation(formationIndex);
            return CommandRejection.None;
        }

        #endregion

        #region Древо прокачки

        [ServerRpc]
        public void CmdBuyUpgrade(byte branch)
        {
            if (!System.Enum.IsDefined(typeof(UpgradeBranch), branch))
            {
                Reject(CommandRejection.UpgradeUnavailable);
                return;
            }

            Reject(ServerTryBuyUpgrade((UpgradeBranch)branch));
        }

        /// <summary>Покупка перка на сервере (ГДД §11).</summary>
        public CommandRejection ServerTryBuyUpgrade(UpgradeBranch branch)
        {
            if (IsEliminated)
                return CommandRejection.PlayerEliminated;

            UpgradeLevels levels = Upgrades;

            CommandRejection rejection = UpgradePurchase.Validate(
                _context.Config.UpgradeTree,
                in levels,
                Wallet.XpAvailable,
                branch,
                out UpgradeNodeConfig node);

            if (rejection != CommandRejection.None)
                return rejection;

            Wallet.TrySpendXp(node.xpCost);
            ServerApplyUpgrades(levels.With(branch, levels.Get(branch) + 1));
            PublishWallet();

            return CommandRejection.None;
        }

        #endregion

        /// <summary>
        /// Сообщить владельцу, почему команда не прошла. У бота владельца нет,
        /// и рассказывать об отказе некому — он читает возвращённую причину сам.
        /// </summary>
        private void Reject(CommandRejection reason)
        {
            if (reason == CommandRejection.None || Owner == null || !Owner.IsActive)
                return;

            TargetCommandRejected(Owner, (byte)reason);
        }

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
