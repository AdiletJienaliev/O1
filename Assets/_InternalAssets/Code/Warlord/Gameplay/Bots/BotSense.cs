using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Bots;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Глаза бота: превращает живое состояние матча в <see cref="BotSituation"/>.
    /// Здесь и только здесь решается, что бот имеет право знать.
    ///
    /// Это принципиальное место. Бот живёт на сервере и физически видит всё — позиции,
    /// здоровье, состав чужих армий. Играть на этом знании значит сделать противника,
    /// которого невозможно обмануть ничем, кроме прямой силы: обход, отвлечение и блеф
    /// перестают работать вообще. Поэтому по умолчанию бот видит ровно то, что видят его
    /// тела на карте, остальное берёт из памяти, а неизвестное оценивает по прикидке
    /// «сколько у противника обычно бывает» — как это делает живой игрок, который не
    /// смотрел в тот угол две минуты.
    ///
    /// Всеведение включается только явным <see cref="BotVisionMode.Omniscient"/>
    /// в настройках сложности — честной поблажкой, видимой в ассете.
    /// </summary>
    public sealed class BotSense
    {
        private readonly IMatchContext _context;
        private readonly PlayerState _player;
        private readonly BotProfile _profile;
        private readonly BotMemory _memory;
        private readonly BotSituation _situation;
        private readonly BotUnitCatalog _catalog;

        private readonly List<ICombatTarget> _buffer = new(64);

        private float _visionRadius = 20f;
        private float _mapScale = 60f;
        private int _cheapestCost = 1;

        public BotSense(
            IMatchContext context,
            PlayerState player,
            in BotProfile profile,
            BotMemory memory,
            BotUnitCatalog catalog)
        {
            _context = context;
            _player = player;
            _profile = profile;
            _memory = memory;
            _catalog = catalog;

            int points = context.CapturePoints != null ? context.CapturePoints.Count : 0;
            _situation = new BotSituation(Mathf.Max(1, points), PlayerSlots.MaxSupported);

            MeasureMap();
        }

        public BotSituation Situation => _situation;

        /// <summary>Радиус, в котором тела бота считаются «видящими». Публичен для отладочных врезок.</summary>
        public float VisionRadius => _visionRadius;

        /// <summary>
        /// Габариты карты и самая дешёвая покупка. Считаются один раз: карта за матч
        /// не меняется, а цены зависят от настроек комнаты, которые тоже зафиксированы.
        /// Именно здесь бот перестаёт зависеть от конкретной арены — дальше он оперирует
        /// долями от этого масштаба, а не метрами.
        /// </summary>
        private void MeasureMap()
        {
            Vector3 home = _context.Players.GetBaseAnchor(_player.Slot).Center;
            float farthest = 40f;

            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                    continue;

                float distance = Vector3.Distance(home, points[i].transform.position);
                if (distance > farthest)
                    farthest = distance;
            }

            _mapScale = farthest;

            CommandConfig command = _context.Config.Command;
            float aggro = command != null ? command.attackAggroRadius : 15f;
            float bonus = _profile.Difficulty != null ? _profile.Difficulty.visionBonus : 0f;

            _visionRadius = Mathf.Max(18f, aggro) + bonus;

            UnitRosterConfig roster = _context.Config.Roster;
            _cheapestCost = int.MaxValue;

            for (int i = 0; i < roster.Count; i++)
            {
                UnitConfig unit = roster.Get(i);
                if (unit == null || unit.isGarrison)
                    continue;

                int cost = _context.Settings.ResolveUnitCost(unit, _context.Config.GameMode);
                if (cost < _cheapestCost)
                    _cheapestCost = cost;
            }

            if (_cheapestCost == int.MaxValue)
                _cheapestCost = 1;
        }

        /// <summary>Собрать снимок мира. Зовётся раз в интервал решения, а не каждый боевой такт.</summary>
        public void Refresh(float now, float matchProgress)
        {
            BotSituation s = _situation;

            s.Slot = _player.Slot;
            s.MapScale = _mapScale;
            s.CheapestUnitCost = _cheapestCost;
            s.MatchProgress = Mathf.Clamp01(matchProgress);
            s.HasAllies = HasAllies();

            HeroController hero = _player.Hero;
            s.HeroAlive = hero != null && hero.IsAlive;
            s.BasePosition = _context.Players.GetBaseAnchor(_player.Slot).Center;
            s.BuyZoneRadius = _context.Players.GetBaseAnchor(_player.Slot).BuyZoneRadius;
            s.HeroPosition = s.HeroAlive ? hero.Position : s.BasePosition;
            s.HeroHealthFraction = hero != null && hero.MaxHealth > 0
                ? Mathf.Clamp01(hero.Health / (float)hero.MaxHealth)
                : 0f;

            s.AtBase = _player.HeroInBuyZone;

            s.ArmyCount = _player.Army != null ? _player.Army.AliveCount : 0;
            s.GarrisonCount = _player.Garrison != null ? _player.Garrison.Count : 0;
            s.QueuedCount = _player.BuildQueue != null ? _player.BuildQueue.PendingCount : 0;
            s.QueuedGuardCount = _player.BuildQueue != null ? _player.BuildQueue.QueuedGuardPoints.Count : 0;
            s.UnitCap = _player.UnitCap;
            s.Gold = _player.Gold;
            s.Xp = _player.Xp;
            s.HoldsCenter = _context.CentralFlagOwner == _player.Slot;
            s.ArmyPower = MyArmyPower();

            RefreshPoints(now, s);
            RefreshRivals(now, s);
        }

        private bool HasAllies()
        {
            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && _context.Teams.AreAllies(players[i].Slot, _player.Slot))
                    return true;
            }

            return false;
        }

        private float MyArmyPower()
        {
            if (_player.Army == null)
                return 0f;

            IReadOnlyList<UnitEntity> units = _player.Army.Units;
            float total = 0f;

            for (int i = 0; i < units.Count; i++)
                total += BotForce.PowerOf(units[i]);

            return total + BotForce.PowerOf(_player.Hero, _context.Config.Hero);
        }

        #region Точки

        private void RefreshPoints(float now, BotSituation s)
        {
            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            s.EnsurePointCapacity(points.Count);
            s.PointCount = 0;
            s.HomeUnderAttack = false;

            for (int i = 0; i < points.Count; i++)
            {
                CapturePointBehaviour point = points[i];
                if (point == null)
                    continue;

                BotPointView view = default;
                view.Index = i;
                view.Kind = point.Kind;
                view.Position = point.transform.position;
                view.ZoneOwnerSlot = point.Kind == CapturePointKind.BaseFlag ? point.ZoneOwnerSlot : PlayerSlots.None;
                view.HasUpgradeSlot = point.HasUpgradeSlot;
                view.UpgradeChosen = point.UpgradeIndex != CapturePointBehaviour.NoUpgrade;
                view.AllowsRally = point.AllowsRallyPoint;
                view.IncomeValue = IncomeOf(point);

                view.DistanceFromHero = Vector3.Distance(s.HeroPosition, view.Position);
                view.DistanceFromMyBase = Vector3.Distance(s.BasePosition, view.Position);

                bool visible = CanSee(view.Position, point.CaptureRadius);
                view.Scouted = visible;

                if (visible)
                {
                    ObservePoint(point, s.Slot, ref view);
                    _memory.RememberPoint(i, view.OwnerSlot, view.EnemyForce, view.FriendlyForce, view.GuardCount, now);
                    view.StaleSeconds = 0f;
                }
                else
                {
                    RecallPoint(i, now, ref view);
                }

                view.Mine = view.OwnerSlot == s.Slot;
                view.Allied = !view.Mine && _context.Teams.AreAllies(view.OwnerSlot, s.Slot);
                view.Neutral = !PlayerSlots.IsValid(view.OwnerSlot);
                view.Enemy = !view.Mine && !view.Allied && !view.Neutral;

                // Свой дом под ударом — единственный факт, который бот знает всегда:
                // о собственной крепости ему докладывают, а не он высматривает её в бинокль.
                if (view.Kind == CapturePointKind.BaseFlag && point.ZoneOwnerSlot == s.Slot)
                {
                    bool losing = point.Status == CaptureStatus.Losing || point.Status == CaptureStatus.Contested;
                    view.UnderAttack = losing || point.OwnerSlot != s.Slot;
                    view.OwnerProgress = point.OwnerProgress;

                    if (view.UnderAttack)
                        s.HomeUnderAttack = true;
                }

                s.Points[s.PointCount++] = view;
            }
        }

        /// <summary>Что видно на точке прямо сейчас: владелец, шкала, кто вокруг стоит.</summary>
        private void ObservePoint(CapturePointBehaviour point, int mySlot, ref BotPointView view)
        {
            view.OwnerSlot = point.OwnerSlot;
            view.OwnerProgress = point.OwnerProgress;
            view.GuardCount = point.GuardCount;
            view.UnderAttack = point.Status == CaptureStatus.Losing || point.Status == CaptureStatus.Contested;

            float radius = point.CaptureRadius + 12f;
            _context.Targeting.CollectInRadius(view.Position, radius, null, _buffer);

            HeroConfig heroConfig = _context.Config.Hero;
            float enemy = 0f;
            float friendly = 0f;

            for (int i = 0; i < _buffer.Count; i++)
            {
                ICombatTarget target = _buffer[i];
                float power = BotForce.PowerOf(target, heroConfig);

                if (_context.Teams.SameSide(target.OwnerSlot, mySlot))
                    friendly += power;
                else
                    enemy += power;
            }

            view.EnemyForce = enemy;
            view.FriendlyForce = friendly;

            _buffer.Clear();
        }

        /// <summary>
        /// Чего бот не видит — то помнит. Никогда не виденная точка остаётся неизвестной:
        /// «ничего не знаю» и «там пусто» — разные вещи, и путать их значит подарить
        /// противнику бесплатный обход.
        /// </summary>
        private void RecallPoint(int index, float now, ref BotPointView view)
        {
            if (_memory.TryRecallPoint(
                    index,
                    now,
                    _profile.MemorySeconds,
                    out int owner,
                    out float enemy,
                    out float friendly,
                    out int guards,
                    out float stale))
            {
                view.OwnerSlot = owner;
                view.EnemyForce = enemy;
                view.FriendlyForce = friendly;
                view.GuardCount = guards;
                view.StaleSeconds = stale;
                return;
            }

            view.OwnerSlot = PlayerSlots.None;
            view.StaleSeconds = float.MaxValue;
        }

        private float IncomeOf(CapturePointBehaviour point)
        {
            GameModeConfig mode = _context.Config.GameMode;

            switch (point.Kind)
            {
                case CapturePointKind.CentralFlag:
                    return mode != null ? mode.goldPerSecondPerFlag : 0f;

                case CapturePointKind.BaseFlag:
                    return mode != null ? mode.goldPerSecondPerCapturedBase : 0f;

                default:
                    // Аванпост платит через выбранное улучшение (ГДД §2.5); пока выбора нет,
                    // считаем по «Снабжению» как по типовому варианту.
                    return point.Upgrade != null ? point.Upgrade.goldPerSecond : 2f;
            }
        }

        #endregion

        #region Соперники

        private void RefreshRivals(float now, BotSituation s)
        {
            IReadOnlyList<PlayerState> players = _context.Players.Active;

            s.EnsureRivalCapacity(PlayerSlots.MaxSupported);
            s.RivalCount = 0;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState other = players[i];
                if (other == null || other.Slot == s.Slot)
                    continue;

                BotRivalView view = default;
                view.Slot = other.Slot;
                view.Active = true;
                view.Eliminated = other.IsEliminated;
                view.Allied = _context.Teams.AreAllies(other.Slot, s.Slot);
                view.BasePosition = _context.Players.GetBaseAnchor(other.Slot).Center;
                view.BaseFlagOwner = FindBaseFlagOwner(other.Slot);
                view.FlagHoldSeconds = _context.Scores.Get(other.Slot).FlagHoldSeconds;
                view.RecentAggression = _memory.Aggression(other.Slot);

                ObserveHero(other, now, ref view);
                ObserveArmy(other, now, s, ref view);

                view.BaseDefense = EstimateBaseDefense(other.Slot, s.Slot, view.BasePosition);

                s.Rivals[s.RivalCount++] = view;
            }
        }

        private void ObserveHero(PlayerState other, float now, ref BotRivalView view)
        {
            HeroController hero = other.Hero;

            if (hero == null)
                return;

            bool visible = hero.IsAlive && CanSee(hero.Position, 0f);

            if (visible)
            {
                float health = hero.MaxHealth > 0 ? Mathf.Clamp01(hero.Health / (float)hero.MaxHealth) : 1f;

                view.HeroKnown = true;
                view.HeroAlive = true;
                view.HeroPosition = hero.Position;
                view.HeroHealthFraction = health;

                _memory.RememberHero(other.Slot, hero.Position, health, now);
                return;
            }

            if (_memory.TryRecallHero(other.Slot, now, _profile.MemorySeconds, out Vector3 position, out float remembered, out _))
            {
                view.HeroKnown = true;
                view.HeroAlive = hero.IsAlive;
                view.HeroPosition = position;
                view.HeroHealthFraction = remembered;
            }
        }

        /// <summary>
        /// Оценка чужой армии. Видимые тела считаются честно; невидимое — не ноль,
        /// а прикидка «сколько у него обычно бывает к этой минуте матча». Иначе бот,
        /// потерявший противника из виду, немедленно решал бы, что тот безоружен,
        /// и обмануть его можно было бы одним отходом за холм.
        /// </summary>
        private void ObserveArmy(PlayerState other, float now, BotSituation s, ref BotRivalView view)
        {
            IReadOnlyList<UnitEntity> units = other.Army != null ? other.Army.Units : null;

            float seen = 0f;
            int seenCount = 0;

            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    UnitEntity unit = units[i];
                    if (unit == null || !unit.IsAlive || !CanSee(unit.Position, 0f))
                        continue;

                    seen += BotForce.PowerOf(unit);
                    seenCount++;
                }
            }

            if (_profile.Vision == BotVisionMode.Omniscient)
            {
                view.ArmyPower = other.Army != null ? TotalPower(other.Army.Units) : 0f;
                view.ArmyCount = other.ArmyCount;
                return;
            }

            if (seenCount > 0)
            {
                _memory.RememberArmy(other.Slot, seen, seenCount, now);
                view.ArmyPower = seen;
                view.ArmyCount = seenCount;
                return;
            }

            _memory.TryRecallArmy(other.Slot, now, _profile.MemorySeconds, out float remembered, out int rememberedCount);

            // Прикидка по времени матча: к середине у любого живого игрока обычно есть
            // половина лимита. Это не подглядывание — это обычный опыт игрока.
            float expectedBodies = Mathf.Max(1, _player.UnitCap) * Mathf.Lerp(0.2f, 0.75f, s.MatchProgress);
            float prior = _catalog != null ? _catalog.AveragePower * expectedBodies : 0f;

            view.ArmyPower = Mathf.Max(remembered, prior);
            view.ArmyCount = Mathf.Max(rememberedCount, Mathf.RoundToInt(expectedBodies));
        }

        private static float TotalPower(IReadOnlyList<UnitEntity> units)
        {
            float total = 0f;

            for (int i = 0; i < units.Count; i++)
                total += BotForce.PowerOf(units[i]);

            return total;
        }

        private int FindBaseFlagOwner(int slot)
        {
            IReadOnlyList<CapturePointBehaviour> points = _context.CapturePoints;

            for (int i = 0; i < points.Count; i++)
            {
                CapturePointBehaviour point = points[i];

                if (point != null && point.Kind == CapturePointKind.BaseFlag && point.ZoneOwnerSlot == slot)
                    return point.OwnerSlot;
            }

            return slot;
        }

        /// <summary>Сколько силы стоит у чужой базы. По ней считается, стоит ли рейд свеч.</summary>
        private float EstimateBaseDefense(int rivalSlot, int mySlot, Vector3 basePosition)
        {
            if (!CanSee(basePosition, _context.Config.GameMode != null ? _context.Config.GameMode.baseHealRadius : 10f))
                return 0f;

            _context.Targeting.CollectInRadius(basePosition, 18f, null, _buffer);

            HeroConfig heroConfig = _context.Config.Hero;
            float defense = 0f;

            for (int i = 0; i < _buffer.Count; i++)
            {
                if (!_context.Teams.SameSide(_buffer[i].OwnerSlot, mySlot))
                    defense += BotForce.PowerOf(_buffer[i], heroConfig);
            }

            _buffer.Clear();
            return defense;
        }

        #endregion

        #region Обзор

        /// <summary>
        /// Видит ли бот точку в мире. Видят его тела: юниты, охранники и полководец.
        /// В командном режиме на сложности с общей разведкой — ещё и тела союзников.
        /// </summary>
        public bool CanSee(Vector3 position, float extraRadius)
        {
            if (_profile.Vision == BotVisionMode.Omniscient)
                return true;

            float radius = _visionRadius + extraRadius;
            float sqrRadius = radius * radius;

            if (SeenBy(_player, position, sqrRadius))
                return true;

            if (_profile.Vision != BotVisionMode.Shared)
                return false;

            IReadOnlyList<PlayerState> players = _context.Players.Active;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState ally = players[i];

                if (ally != null && _context.Teams.AreAllies(ally.Slot, _player.Slot) && SeenBy(ally, position, sqrRadius))
                    return true;
            }

            return false;
        }

        private static bool SeenBy(PlayerState player, Vector3 position, float sqrRadius)
        {
            HeroController hero = player.Hero;

            if (hero != null && hero.IsAlive && (hero.Position - position).sqrMagnitude <= sqrRadius)
                return true;

            if (player.Army != null)
            {
                IReadOnlyList<UnitEntity> units = player.Army.Units;

                for (int i = 0; i < units.Count; i++)
                {
                    UnitEntity unit = units[i];
                    if (unit != null && unit.IsAlive && (unit.Position - position).sqrMagnitude <= sqrRadius)
                        return true;
                }
            }

            if (player.Garrison == null)
                return false;

            IReadOnlyList<GarrisonRoster.Post> posts = player.Garrison.Posts;

            for (int i = 0; i < posts.Count; i++)
            {
                UnitEntity guard = posts[i].Unit;
                if (guard != null && guard.IsAlive && (guard.Position - position).sqrMagnitude <= sqrRadius)
                    return true;
            }

            return false;
        }

        #endregion
    }
}
