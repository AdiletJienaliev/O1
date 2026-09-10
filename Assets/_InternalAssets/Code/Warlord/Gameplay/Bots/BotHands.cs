using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Bots;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Bots;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Руки бота: покупки, гарнизон, прокачка, точка сбора и приказы армии.
    ///
    /// Ни одно действие здесь не делается напрямую. Всё идёт через серверные команды
    /// <see cref="PlayerState"/> — те же самые, что обслуживают живого игрока, с теми же
    /// проверками зоны покупки, лимита армии, цены и владения точкой. Поэтому бот не может
    /// купить юнита, стоя посреди карты, не может переполнить лимит и не получает скидок,
    /// а любое новое правило, добавленное в валидацию, начинает действовать на него само.
    ///
    /// Второе ограничение — темп. Действия выдаются не пачкой, а по одному, с паузой
    /// из настроек сложности: бот, закупающий десять юнитов в один такт, читается как
    /// машина мгновенно, даже если каждая покупка законна.
    /// </summary>
    public sealed class BotHands
    {
        private readonly IMatchContext _context;
        private readonly PlayerState _player;
        private readonly BotProfile _profile;
        private readonly BotUnitCatalog _catalog;
        private readonly BotArmyPlanner _planner;
        private readonly BotSense _sense;
        private readonly System.Random _random;

        private float _actionCooldown;
        private ArmyOrderType _lastOrder = ArmyOrderType.HoldGround;
        private Vector3 _lastOrderAnchor;
        private float _orderCooldown;

        public BotHands(
            IMatchContext context,
            PlayerState player,
            in BotProfile profile,
            BotUnitCatalog catalog,
            BotArmyPlanner planner,
            BotSense sense,
            System.Random random)
        {
            _context = context;
            _player = player;
            _profile = profile;
            _catalog = catalog;
            _planner = planner;
            _sense = sense;
            _random = random;
        }

        /// <summary>Одно действие за такт, не чаще паузы из сложности.</summary>
        public void Tick(float deltaTime, BotSituation situation)
        {
            _actionCooldown -= deltaTime;
            _orderCooldown -= deltaTime;

            RefreshCatalog();

            if (_actionCooldown > 0f)
                return;

            if (TryChooseOutpostUpgrade())
            {
                Spend();
                return;
            }

            if (TryBuyUpgrade(situation))
            {
                Spend();
                return;
            }

            // Всё остальное требует полководца в зоне покупки — ровно как у игрока (ГДД §5.1).
            if (!_player.HeroInBuyZone)
                return;

            if (TryUpdateRallyPoint())
            {
                Spend();
                return;
            }

            if (TryBuyGuard(situation))
            {
                Spend();
                return;
            }

            if (TryBuyUnit())
                Spend();
        }

        private void Spend() => _actionCooldown = _profile.ActionCooldown;

        /// <summary>Роли типов пересчитываются только после покупки перка — версия кэша статов это ловит.</summary>
        private void RefreshCatalog()
        {
            _catalog.Refresh(_player.Stats, ResolveCost);
        }

        private int ResolveCost(UnitConfig unit)
        {
            int baseCost = _context.Settings.ResolveUnitCost(unit, _context.Config.GameMode);
            return Mathf.Max(1, Mathf.RoundToInt(baseCost * _profile.CostMultiplier));
        }

        #region Армия

        private bool TryBuyUnit()
        {
            RecountComposition();

            int choice = _planner.ChooseFieldUnit(
                _catalog,
                _profile.Personality,
                _profile.Difficulty,
                CanBuyField,
                _random);

            if (choice < 0)
                return false;

            return _player.ServerTryPurchaseUnit(choice) == CommandRejection.None;
        }

        private bool CanBuyField(int rosterIndex) => _player.CanPurchaseUnit(rosterIndex) == CommandRejection.None;

        /// <summary>
        /// Пересчёт состава: свой — целиком, чужой — только то, что бот действительно видит.
        /// Контрпик по невидимой армии был бы подглядыванием, а не мышлением.
        /// </summary>
        private void RecountComposition()
        {
            _planner.ClearComposition();

            if (_player.Army != null)
            {
                IReadOnlyList<UnitEntity> mine = _player.Army.Units;

                for (int i = 0; i < mine.Count; i++)
                {
                    if (mine[i] != null && mine[i].IsAlive)
                        _planner.CountMine(mine[i].UnitTypeIndex);
                }
            }

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int p = 0; p < players.Count; p++)
            {
                PlayerState other = players[p];

                if (other == null || other.Army == null || _context.Teams.SameSide(other.Slot, _player.Slot))
                    continue;

                IReadOnlyList<UnitEntity> units = other.Army.Units;

                for (int i = 0; i < units.Count; i++)
                {
                    UnitEntity unit = units[i];

                    if (unit != null && unit.IsAlive && _sense.CanSee(unit.Position, 0f))
                        _planner.CountEnemy(unit.UnitTypeIndex);
                }
            }
        }

        #endregion

        #region Гарнизон

        /// <summary>
        /// Охранники (ГДД §1). Бот держит их ровно настолько, насколько велит характер:
        /// они едят тот же лимит, что и полевая армия, и жадный до гарнизона бот
        /// сознательно платит за это слабой атакой.
        /// </summary>
        private bool TryBuyGuard(BotSituation situation)
        {
            BotPersonalityConfig personality = _profile.Personality;

            if (personality == null || personality.garrisonShare <= 0.01f)
                return false;

            // Сперва войско, потом сторожа. Обратный порядок выглядит особенно глупо на старте:
            // бот тратит первые деньги на охрану пустых точек, а в поле выходить нечем.
            if (situation.FieldFill < personality.massBeforePush * 0.6f)
                return false;

            int cap = Mathf.Max(1, _player.UnitCap);

            // Считаем и стоящих, и заказанных: заказ доедет через несколько секунд,
            // и без него бот успевает набрать вдвое больше гарнизона, чем собирался.
            int queued = _player.BuildQueue != null ? _player.BuildQueue.QueuedGuardPoints.Count : 0;
            int guards = (_player.Garrison != null ? _player.Garrison.Count : 0) + queued;

            if (guards / (float)cap >= personality.garrisonShare)
                return false;

            CapturePointBehaviour target = PickGarrisonPoint(situation);
            if (target == null)
                return false;

            int choice = _planner.ChooseGuard(
                _catalog,
                personality,
                index => _player.CanPurchaseGuard(index, target) == CommandRejection.None,
                _random);

            if (choice < 0)
                return false;

            return _player.ServerTryPurchaseGuard(choice, target) == CommandRejection.None;
        }

        /// <summary>
        /// Куда ставить охранника. Ценнее всего точка, которая сама откатывается без гарнизона
        /// (ГДД §2.4) и до которой врагу ближе всего: именно она отвалится первой.
        /// </summary>
        private CapturePointBehaviour PickGarrisonPoint(BotSituation situation)
        {
            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            CapturePointBehaviour best = null;
            float bestScore = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                CapturePointBehaviour point = points[i];

                if (point == null || point.OwnerSlot != _player.Slot || point.MaxGuards <= 0)
                    continue;

                float score = 1f;

                // Точка с откатом без гарнизона — первая в очереди: без охранника она просто утечёт.
                if (point.Config != null && point.Config.garrisonDecay)
                    score *= 2f;

                if (point.Kind == CapturePointKind.CentralFlag)
                    score *= 1.5f;

                // Уже прикрытая точка нужна меньше, чем голая.
                score /= 1f + point.GuardCount;

                // Дальняя от дома опаснее: подкрепление туда не успеет.
                score *= 1f + Vector3.Distance(point.transform.position, situation.BasePosition) / Mathf.Max(1f, situation.MapScale);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = point;
            }

            return best;
        }

        #endregion

        #region Прокачка и улучшения

        private bool TryBuyUpgrade(BotSituation situation)
        {
            UpgradeLevels levels = _player.Upgrades;

            if (!BotUpgradePlanner.TryChoose(
                    _context.Config.UpgradeTree,
                    in levels,
                    _player.Xp,
                    situation,
                    _profile.Personality,
                    _profile.Difficulty,
                    RangedShare(),
                    _random,
                    out UpgradeBranch branch))
            {
                return false;
            }

            return _player.ServerTryBuyUpgrade(branch) == CommandRejection.None;
        }

        private float RangedShare()
        {
            if (_player.Army == null)
                return 0f;

            IReadOnlyList<UnitEntity> units = _player.Army.Units;
            int ranged = 0;

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].IsAlive && units[i].Stats.IsRanged)
                    ranged++;
            }

            return units.Count > 0 ? ranged / (float)units.Count : 0f;
        }

        /// <summary>
        /// Выбор улучшения аванпоста (ГДД §2.5). Улучшения сравниваются по их собственным
        /// эффектам, а не по названию типа: добавленное завтра четвёртое улучшение
        /// попадёт в сравнение само.
        /// </summary>
        private bool TryChooseOutpostUpgrade()
        {
            OutpostUpgradeSetConfig set = _context.Config.OutpostUpgrades;

            if (set == null || set.Count == 0 || _profile.Personality == null)
                return false;

            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            for (int i = 0; i < points.Count; i++)
            {
                CapturePointBehaviour point = points[i];

                if (point == null || point.OwnerSlot != _player.Slot || !point.HasUpgradeSlot)
                    continue;

                if (point.UpgradeIndex != CapturePointBehaviour.NoUpgrade)
                    continue;

                int choice = PickUpgrade(set);

                if (choice >= 0 && _player.ServerTrySelectOutpostUpgrade(point, choice) == CommandRejection.None)
                    return true;
            }

            return false;
        }

        private int PickUpgrade(OutpostUpgradeSetConfig set)
        {
            BotPersonalityConfig p = _profile.Personality;

            int best = -1;
            float bestScore = 0f;

            for (int i = 0; i < set.Count; i++)
            {
                OutpostUpgradeConfig upgrade = set.Get(i);
                if (upgrade == null)
                    continue;

                float economy = upgrade.goldPerSecond * 0.12f + upgrade.unitCapBonus * 0.2f;
                float tempo = upgrade.spawnTimeReduction * 2f;
                float defence = upgrade.healPerSecond * 0.04f
                    + upgrade.guardCostReduction * 1.2f
                    + upgrade.guardHealthBonus * 0.6f;

                float variety = upgrade.unlockedUnit != null ? 0.35f : 0f;

                float score = economy * p.economyWeight
                    + tempo * (2f - p.caution)
                    + defence * (p.defenseWeight + p.garrisonShare)
                    + variety;

                if (_random != null)
                    score *= 1f + ((float)_random.NextDouble() * 2f - 1f) * 0.2f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = i;
            }

            return best;
        }

        #endregion

        #region Точка сбора и приказы

        /// <summary>
        /// Точка сбора (ГДД §2.6). Бот ставит её на свою точку ближе всего к фронту:
        /// без неё каждое подкрепление тратит минуту на дорогу от базы, и потерянная
        /// армия уже не восстанавливается вовремя.
        /// </summary>
        private bool TryUpdateRallyPoint()
        {
            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            CapturePointBehaviour best = null;
            float bestDistance = -1f;
            Vector3 home = _context.Players.GetBaseAnchor(_player.Slot).Center;

            for (int i = 0; i < points.Count; i++)
            {
                CapturePointBehaviour point = points[i];

                if (point == null || point.OwnerSlot != _player.Slot || !point.AllowsRallyPoint)
                    continue;

                float distance = Vector3.Distance(point.transform.position, home);

                // Дальняя от дома точка и есть фронт.
                if (distance <= bestDistance)
                    continue;

                bestDistance = distance;
                best = point;
            }

            if (_player.RallyPoint == best)
                return false;

            return _player.ServerTrySetRallyPoint(best) == CommandRejection.None;
        }

        /// <summary>
        /// Приказ армии под текущую цель. Меняется редко и только по делу: приказ,
        /// переотдаваемый каждые полсекунды, сбивает строй и выглядит как припадок.
        /// </summary>
        public void ApplyArmyOrder(ArmyOrderType order, Vector3 anchor, float yaw)
        {
            if (_orderCooldown > 0f && order == _lastOrder && (anchor - _lastOrderAnchor).sqrMagnitude < 36f)
                return;

            if (_player.ServerTrySetOrder(order, anchor, yaw) != CommandRejection.None)
                return;

            _lastOrder = order;
            _lastOrderAnchor = anchor;
            _orderCooldown = 1.5f;
        }

        #endregion
    }
}
