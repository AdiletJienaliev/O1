using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Economy;
using Warlord.Domain.Stats;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Состояние игрока: кошелёк, прокачка, армия, очередь постройки.
    /// Объект принадлежит своему клиенту, поэтому команды приходят сюда через ServerRpc,
    /// а наружу отдаётся только то, что нужно HUD. Вся авторитетная логика — на сервере.
    /// </summary>
    public sealed partial class PlayerState : NetworkBehaviour
    {
        private readonly SyncVar<byte> _slot = new();
        private readonly SyncVar<int> _gold = new(new SyncTypeSettings(0.2f));
        private readonly SyncVar<int> _xp = new(new SyncTypeSettings(0.2f));
        private readonly SyncVar<UpgradeLevels> _upgrades = new();
        private readonly SyncVar<byte> _orderType = new();
        private readonly SyncVar<byte> _formationIndex = new();
        private readonly SyncVar<byte> _armyCount = new(new SyncTypeSettings(0.2f));

        // Гарнизон считается отдельно от армии: в лимит он входит наравне с полевыми юнитами,
        // но игроку в HUD нужны оба числа порознь — «Армия 8 / 20 · Гарнизон 7» (ГДД §1.9).
        private readonly SyncVar<byte> _garrisonCount = new(new SyncTypeSettings(0.2f));
        private readonly SyncVar<byte> _unitCap = new();
        private readonly SyncVar<byte> _capturedBases = new();
        private readonly SyncVar<bool> _eliminated = new();
        private readonly SyncVar<float> _incomePerSecond = new(new SyncTypeSettings(0.5f));
        private readonly SyncVar<float> _flagHoldSeconds = new(new SyncTypeSettings(0.5f));

        // Кто сидит в слоте. Индекс характера служит и признаком бота: отдельный bool рядом
        // с ним рано или поздно разъехался бы с ним по значению, а так состояние одно.
        private readonly SyncVar<byte> _botPersonality = new(NoBotPersonality);
        private readonly SyncVar<byte> _botDifficulty = new();
        private readonly SyncVar<byte> _botNameIndex = new();

        private readonly SyncList<SpawnTicket> _spawnQueue = new(new SyncTypeSettings(ReadPermission.OwnerOnly));

        private readonly List<SpawnTicket> _ticketBuffer = new(16);
        private readonly List<UnitEntity> _despawnBuffer = new(32);

        private IMatchContext _context;

        public int Slot => _slot.Value;
        public int Gold => _gold.Value;
        public int Xp => _xp.Value;
        public UpgradeLevels Upgrades => _upgrades.Value;
        public ArmyOrderType OrderType => (ArmyOrderType)_orderType.Value;
        public int FormationIndex => _formationIndex.Value;
        public int ArmyCount => _armyCount.Value;

        /// <summary>Живые охранники на всех точках. Занимают те же слоты лимита, что и армия (ГДД §1.1).</summary>
        public int GarrisonCount => _garrisonCount.Value;

        public int UnitCap => _unitCap.Value;
        public int CapturedBases => _capturedBases.Value;
        public bool IsEliminated => _eliminated.Value;
        public float IncomePerSecond => _incomePerSecond.Value;

        /// <summary>Накопленное время удержания центрального флага — основной критерий победы (ГДД §2).</summary>
        public float FlagHoldSeconds => _flagHoldSeconds.Value;

        /// <summary>Очередь постройки. Видна только владельцу — противнику она не нужна.</summary>
        public IReadOnlyList<SpawnTicket> SpawnQueue => _spawnQueue;

        /// <summary>Значение индекса характера, означающее «слот занят живым игроком».</summary>
        public const byte NoBotPersonality = 255;

        /// <summary>Слотом управляет бот. Нужно и HUD, и таблице итогов, и самой системе ботов.</summary>
        public bool IsBot => _botPersonality.Value != NoBotPersonality;

        /// <summary>Индекс характера в наборе ботов или <see cref="NoBotPersonality"/> у живого игрока.</summary>
        public int BotPersonalityIndex => _botPersonality.Value;

        /// <summary>Сложность бота. У живого игрока значения не имеет.</summary>
        public BotDifficulty BotDifficulty => (BotDifficulty)_botDifficulty.Value;

        /// <summary>Индекс имени внутри пула характера: два одинаковых бота обязаны зваться по-разному.</summary>
        public int BotNameIndex => _botNameIndex.Value;

        /// <summary>
        /// Команда игрока или -1, если он сам за себя. Читается из настроек комнаты,
        /// поэтому одинаково работает и на сервере, и на клиенте.
        /// </summary>
        public int TeamId
        {
            get
            {
                if (_context != null)
                    return _context.Teams.AssignedTeam(Slot);

                MatchManager manager = MatchManager.Instance;
                return manager != null ? manager.Settings.Teams.AssignedTeam(Slot) : -1;
            }
        }

        // --- Серверные объекты. На клиенте всегда null. ---
        public PlayerWallet Wallet { get; private set; }
        public ArmyController Army { get; private set; }
        public ArmyStatsCache Stats { get; private set; }
        public UnitSpawnQueue BuildQueue { get; private set; }

        /// <summary>Охранники игрока и их привязка к точкам (ГДД §1).</summary>
        public GarrisonRoster Garrison { get; private set; }

        /// <summary>Суммарный эффект улучшений с удерживаемых аванпостов (ГДД §2.5).</summary>
        public OutpostUpgradeStack OutpostUpgrades { get; private set; }

        /// <summary>
        /// Куда идут новые полевые юниты (ГДД §2.6). Null — на базу. Точка сбора живёт только
        /// на сервере: клиенту достаточно видеть её подсветку в панели покупки.
        /// </summary>
        public CapturePointBehaviour RallyPoint { get; private set; }

        public HeroController Hero { get; internal set; }
        public bool HoldsCentralFlag => _context != null && _context.CentralFlagOwner == Slot;

        /// <summary>
        /// Состояние локального игрока. Ставится на клиенте при получении владения:
        /// UI и роутер команд иначе не смогли бы найти свой объект среди чужих.
        /// </summary>
        public static PlayerState Local { get; private set; }

        /// <summary>
        /// Все состояния игроков по слотам, как их видит эта машина. Нужны интерфейсу:
        /// чтобы подписать строку таблицы, надо знать, бот в слоте или человек, а искать
        /// это перебором сцены каждый кадр для каждой строки дороже, чем один массив.
        /// Живёт рядом с <see cref="Local"/> и по тем же правилам.
        /// </summary>
        private static readonly PlayerState[] BySlot = new PlayerState[PlayerSlots.MaxSupported];

        /// <summary>Игрок в слоте или null. Только для презентации — серверный код спрашивает реестр.</summary>
        public static PlayerState Find(int slot) => PlayerSlots.IsValid(slot) ? BySlot[slot] : null;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
                Local = this;

            if (PlayerSlots.IsValid(Slot))
                BySlot[Slot] = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (Local == this)
                Local = null;

            if (PlayerSlots.IsValid(Slot) && BySlot[Slot] == this)
                BySlot[Slot] = null;
        }

        /// <summary>
        /// Пометить слот ботом. Вызывается спавнером до <see cref="NetworkBehaviour.IsSpawned"/>,
        /// чтобы клиент получил объект уже с именем и сложностью и не показал безымянного игрока
        /// на один кадр.
        /// </summary>
        public void ServerMarkAsBot(int personalityIndex, BotDifficulty difficulty, int nameIndex)
        {
            _botPersonality.Value = (byte)Mathf.Clamp(personalityIndex, 0, NoBotPersonality - 1);
            _botDifficulty.Value = (byte)difficulty;
            _botNameIndex.Value = (byte)Mathf.Clamp(nameIndex, 0, byte.MaxValue);
        }

        /// <summary>Инициализация на сервере сразу после спавна объекта игрока.</summary>
        public void ServerInitialize(IMatchContext context, int slot)
        {
            _context = context;
            _slot.Value = (byte)slot;

            GameConfig config = context.Config;
            GameModeConfig mode = config.GameMode;

            UnitStatsResolver resolver = new(config.UpgradeTree, config.Command);
            Stats = new ArmyStatsCache(config.Roster, resolver);

            Wallet = new PlayerWallet(context.Settings.StartingGold);
            Army = new ArmyController(slot, Stats, config.Command, config.Formations);
            BuildQueue = new UnitSpawnQueue(mode, config.Roster, context.Settings);
            Garrison = new GarrisonRoster();
            OutpostUpgrades = new OutpostUpgradeStack();

            BuildQueue.BuildCompleted += OnBuildCompleted;
            BuildQueue.BuildCancelled += OnBuildCancelled;
            BuildQueue.QueueChanged += PublishQueue;

            _formationIndex.Value = (byte)config.Formations.DefaultIndex;
            _orderType.Value = (byte)ArmyOrderType.HoldGround;

            // Стартовый приказ якорится на своей базе, иначе строй ушёл бы к началу координат.
            PlayerBaseAnchor anchor = context.Players.GetBaseAnchor(slot);
            Army.SetOrder(ArmyOrder.HoldAt(anchor.UnitSpawnPoint, anchor.YawDegrees));

            _unitCap.Value = (byte)resolver.ResolveUnitCap(mode.maxUnits, _upgrades.Value);

            PublishWallet();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (BuildQueue != null)
            {
                BuildQueue.BuildCompleted -= OnBuildCompleted;
                BuildQueue.BuildCancelled -= OnBuildCancelled;
                BuildQueue.QueueChanged -= PublishQueue;
            }
        }

        /// <summary>
        /// Множитель дохода этого игрока. Существует ради гандикапа сложности ботов
        /// (<see cref="Warlord.Configs.Bots.BotDifficultyConfig.incomeMultiplier"/>) и по умолчанию
        /// равен единице: у живых игроков и у ботов экономика одна и та же, пока
        /// кто-то явно не решит иначе в ассете сложности.
        /// </summary>
        public float IncomeScale { get; set; } = 1f;

        /// <summary>Начисление дохода и публикация сводки для HUD.</summary>
        public void ServerTickEconomy(float deltaTime, in IncomeProfile income)
        {
            if (!IsServerInitialized || IsEliminated)
                return;

            float scale = Mathf.Max(0f, IncomeScale);

            Wallet.Accrue(in income, deltaTime * scale);
            _incomePerSecond.Value = income.GoldPerSecond * scale;
            _flagHoldSeconds.Value = _context.Scores.Get(Slot).FlagHoldSeconds;
            PublishWallet();
        }

        /// <summary>Продвижение очереди постройки и уборка погибших из состава армии.</summary>
        public void ServerTickBuildQueue(float deltaTime)
        {
            if (!IsServerInitialized || IsEliminated)
                return;

            // Точку могли отбить, пока охранник строился: покупка отменяется, золото
            // возвращается (ГДД §1.6). Проверяем до тика, чтобы отменённый не успел родиться.
            BuildQueue.CancelInvalidGuards(IsOwnedByMe);
            BuildQueue.Tick(deltaTime);

            int before = Army.AliveCount;
            Army.PurgeDead();
            if (Army.AliveCount != before)
                _armyCount.Value = (byte)Mathf.Min(byte.MaxValue, Army.AliveCount);

            if (Garrison.PurgeDead())
                _garrisonCount.Value = (byte)Mathf.Min(byte.MaxValue, Garrison.Count);
        }

        /// <summary>Точка всё ещё моя. Единственное условие, при котором охранник на неё поедет.</summary>
        private bool IsOwnedByMe(CapturePointBehaviour point) => point != null && point.OwnerSlot == Slot;

        /// <summary>
        /// Сколько слотов лимита занято прямо сейчас: армия, гарнизон и всё, что в очереди.
        /// Считать очередь обязательно — иначе за один клик заказывается втрое больше лимита.
        /// </summary>
        public int OccupiedUnitSlots => Army.AliveCount + Garrison.Count + BuildQueue.PendingCount;

        /// <summary>Пересчёт статов после покупки перка — мгновенно для всех живых юнитов (ГДД §11).</summary>
        public void ServerApplyUpgrades(in UpgradeLevels levels)
        {
            _upgrades.Value = levels;
            Stats.ApplyLevels(in levels);
            RefreshUnitCap();
        }

        /// <summary>
        /// Пересобрать эффекты аванпостов (ГДД §2.5). Лимит армии и скорость постройки
        /// применяются здесь же: это единственные эффекты улучшений, живущие на игроке,
        /// а не на самой точке.
        /// </summary>
        public void ServerApplyOutpostUpgrades(IReadOnlyList<OutpostUpgradeConfig> upgrades)
        {
            if (!IsServerInitialized)
                return;

            OutpostUpgrades.Rebuild(upgrades);
            BuildQueue.SpawnTimeMultiplier = OutpostUpgrades.SpawnTimeMultiplier;
            RefreshUnitCap();
        }

        /// <summary>Назначить точку сбора. Null — новые юниты снова идут на базу (ГДД §2.6).</summary>
        public void ServerSetRallyPoint(CapturePointBehaviour point) => RallyPoint = point;

        /// <summary>Точка потеряна: если сбор стоял на ней, он возвращается на базу.</summary>
        public void ServerClearRallyPointIfAt(CapturePointBehaviour point)
        {
            if (RallyPoint == point)
                RallyPoint = null;
        }

        /// <summary>Лимит армии: база режима, плоский бонус древа и прибавка от «Снабжения».</summary>
        private void RefreshUnitCap()
        {
            UnitStatsResolver resolver = new(_context.Config.UpgradeTree, _context.Config.Command);
            UpgradeLevels levels = _upgrades.Value;

            int cap = resolver.ResolveUnitCap(_context.Config.GameMode.maxUnits, levels)
                + (OutpostUpgrades != null ? OutpostUpgrades.UnitCapBonus : 0);

            _unitCap.Value = (byte)Mathf.Clamp(cap, 0, byte.MaxValue);
        }

        public void ServerSetOrder(in ArmyOrder order)
        {
            Army.SetOrder(in order);
            _orderType.Value = (byte)order.Type;
        }

        public void ServerSetFormation(int formationIndex)
        {
            Army.SetFormation(formationIndex);
            _formationIndex.Value = (byte)Army.FormationIndex;
        }

        /// <summary>Захват чужой базы: постоянная прибавка к доходу (ГДД §10.2).</summary>
        public void ServerAddCapturedBase()
        {
            _capturedBases.Value = (byte)Mathf.Min(byte.MaxValue, _capturedBases.Value + 1);
        }

        /// <summary>
        /// Выбывание игрока (ГДД §10.3): армия уничтожается, полководец не респавнится,
        /// накопленный счёт остаётся в таблице.
        /// </summary>
        public void ServerEliminate(EliminationReason reason)
        {
            if (IsEliminated)
                return;

            _eliminated.Value = true;

            BuildQueue.Clear();
            RallyPoint = null;

            // Гарнизон уничтожается вместе с остальной армией (ГДД §1.4): точки выбывшего
            // всё равно уходят в нейтраль, и оставленные охранники били бы уже ни за кого.
            Army.CollectAndClear(_despawnBuffer);
            Garrison.CollectAndClear(_despawnBuffer);

            for (int i = 0; i < _despawnBuffer.Count; i++)
                _context.Units.Despawn(_despawnBuffer[i]);

            _despawnBuffer.Clear();
            _armyCount.Value = 0;
            _garrisonCount.Value = 0;

            if (Hero != null)
                Hero.ServerSetSpectator();

            _context.Events.RaisePlayerEliminated(Slot, reason);
        }

        private void OnBuildCompleted(int rosterIndex, CapturePointBehaviour point, int paidGold)
        {
            if (point != null)
            {
                SpawnGuard(rosterIndex, point, paidGold);
                return;
            }

            SpawnFieldUnit(rosterIndex);
        }

        /// <summary>
        /// Полевой юнит. Появляется на базе или на точке сбора (ГДД §2.6): от базы до центра
        /// около сотни метров, и без точки сбора каждая потеря армии стоила бы минуты бега.
        /// </summary>
        private void SpawnFieldUnit(int rosterIndex)
        {
            PlayerBaseAnchor anchor = _context.Players.GetBaseAnchor(Slot);

            Vector3 position = anchor.UnitSpawnPoint;
            float yaw = anchor.YawDegrees;

            if (RallyPoint != null && RallyPoint.OwnerSlot == Slot)
            {
                position = RallyPoint.transform.position;
                yaw = RallyPoint.transform.eulerAngles.y;
            }

            UnitEntity unit = _context.Units.Spawn(
                Slot,
                rosterIndex,
                Stats,
                position,
                Quaternion.Euler(0f, yaw, 0f),
                Owner);

            if (unit == null)
                return;

            Army.Add(unit);
            _armyCount.Value = (byte)Mathf.Min(byte.MaxValue, Army.AliveCount);
        }

        /// <summary>
        /// Охранник. Появляется прямо в своём слоте на точке, а не идёт от базы (ГДД §1.6):
        /// одинокий медленный юнит, ковыляющий через полкарты, был бы перехвачен всегда,
        /// и покупка ощущалась бы как обман, а не как подкрепление гарнизона.
        /// </summary>
        private void SpawnGuard(int rosterIndex, CapturePointBehaviour point, int paidGold)
        {
            int slotCount = ResolveGuardSlots(rosterIndex, point);

            if (!Garrison.TryTakeSlot(point, slotCount, out int slotIndex))
            {
                // Слот заняли, пока охранник строился. Деньги возвращаем — покупка не состоялась.
                Refund(paidGold);
                return;
            }

            Vector3 center = point.transform.position;
            Vector3 home = Domain.Capture.GarrisonLayout.SlotPosition(center, point.GarrisonRingRadius, slotIndex, slotCount);
            float yaw = Domain.Capture.GarrisonLayout.SlotYaw(center, home);

            UnitEntity unit = _context.Units.Spawn(
                Slot,
                rosterIndex,
                Stats,
                home,
                Quaternion.Euler(0f, yaw, 0f),
                Owner);

            if (unit == null)
                return;

            // «Наёмники» дают охранникам этой точки прибавку к здоровью (ГДД §2.5).
            OutpostUpgradeConfig upgrade = point.Upgrade;
            if (upgrade != null && upgrade.guardHealthBonus > 0f)
                unit.ServerSetHealthMultiplier(1f + upgrade.guardHealthBonus);

            unit.AssignFormationSlot(slotIndex, home);

            Garrison.Add(unit, point, slotIndex);
            _garrisonCount.Value = (byte)Mathf.Min(byte.MaxValue, Garrison.Count);
        }

        /// <summary>Сколько слотов на кольце: меньшее из лимита точки и лимита самого типа охранника.</summary>
        private int ResolveGuardSlots(int rosterIndex, CapturePointBehaviour point)
        {
            UnitConfig config = _context.Config.Roster.Get(rosterIndex);
            int byType = config != null ? Mathf.Max(1, config.maxPerPoint) : 1;

            return Mathf.Max(1, Mathf.Min(point.MaxGuards, byType));
        }

        /// <summary>Возврат денег за несостоявшуюся покупку охранника (ГДД §1.6).</summary>
        private void OnBuildCancelled(int gold) => Refund(gold);

        private void Refund(int gold)
        {
            if (gold <= 0)
                return;

            Wallet.AddGold(gold);
            PublishWallet();
        }

        internal void ServerNotifyUnitLost(UnitEntity unit)
        {
            Army.Remove(unit);
            _armyCount.Value = (byte)Mathf.Min(byte.MaxValue, Army.AliveCount);

            Garrison.Remove(unit);
            _garrisonCount.Value = (byte)Mathf.Min(byte.MaxValue, Garrison.Count);
        }

        private void PublishWallet()
        {
            _gold.Value = Wallet.Gold;
            _xp.Value = Wallet.XpAvailable;
        }

        private void PublishQueue()
        {
            BuildQueue.CopyTo(_ticketBuffer, TimeManager.Tick, (float)TimeManager.TickDelta);

            _spawnQueue.Clear();
            for (int i = 0; i < _ticketBuffer.Count; i++)
                _spawnQueue.Add(_ticketBuffer[i]);
        }
    }
}
