using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Economy;
using Warlord.Domain.Stats;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Army;
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
        private readonly SyncVar<byte> _unitCap = new();
        private readonly SyncVar<byte> _capturedBases = new();
        private readonly SyncVar<bool> _eliminated = new();
        private readonly SyncVar<float> _incomePerSecond = new(new SyncTypeSettings(0.5f));
        private readonly SyncVar<float> _flagHoldSeconds = new(new SyncTypeSettings(0.5f));

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
        public int UnitCap => _unitCap.Value;
        public int CapturedBases => _capturedBases.Value;
        public bool IsEliminated => _eliminated.Value;
        public float IncomePerSecond => _incomePerSecond.Value;

        /// <summary>Накопленное время удержания центрального флага — основной критерий победы (ГДД §2).</summary>
        public float FlagHoldSeconds => _flagHoldSeconds.Value;

        /// <summary>Очередь постройки. Видна только владельцу — противнику она не нужна.</summary>
        public IReadOnlyList<SpawnTicket> SpawnQueue => _spawnQueue;

        // --- Серверные объекты. На клиенте всегда null. ---
        public PlayerWallet Wallet { get; private set; }
        public ArmyController Army { get; private set; }
        public ArmyStatsCache Stats { get; private set; }
        public UnitSpawnQueue BuildQueue { get; private set; }
        public HeroController Hero { get; internal set; }
        public bool HoldsCentralFlag => _context != null && _context.CentralFlagOwner == Slot;

        /// <summary>
        /// Состояние локального игрока. Ставится на клиенте при получении владения:
        /// UI и роутер команд иначе не смогли бы найти свой объект среди чужих.
        /// </summary>
        public static PlayerState Local { get; private set; }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
                Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (Local == this)
                Local = null;
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

            BuildQueue.BuildCompleted += OnBuildCompleted;
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
                BuildQueue.QueueChanged -= PublishQueue;
            }
        }

        /// <summary>Начисление дохода и публикация сводки для HUD.</summary>
        public void ServerTickEconomy(float deltaTime, in IncomeProfile income)
        {
            if (!IsServerInitialized || IsEliminated)
                return;

            Wallet.Accrue(in income, deltaTime);
            _incomePerSecond.Value = income.GoldPerSecond;
            _flagHoldSeconds.Value = _context.Scores.Get(Slot).FlagHoldSeconds;
            PublishWallet();
        }

        /// <summary>Продвижение очереди постройки и уборка погибших из состава армии.</summary>
        public void ServerTickBuildQueue(float deltaTime)
        {
            if (!IsServerInitialized || IsEliminated)
                return;

            BuildQueue.Tick(deltaTime);

            int before = Army.AliveCount;
            Army.PurgeDead();
            if (Army.AliveCount != before)
                _armyCount.Value = (byte)Mathf.Min(byte.MaxValue, Army.AliveCount);
        }

        /// <summary>Пересчёт статов после покупки перка — мгновенно для всех живых юнитов (ГДД §11).</summary>
        public void ServerApplyUpgrades(in UpgradeLevels levels)
        {
            _upgrades.Value = levels;
            Stats.ApplyLevels(in levels);

            UnitStatsResolver resolver = new(_context.Config.UpgradeTree, _context.Config.Command);
            _unitCap.Value = (byte)Mathf.Clamp(resolver.ResolveUnitCap(_context.Config.GameMode.maxUnits, levels), 0, byte.MaxValue);
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
            Army.CollectAndClear(_despawnBuffer);

            for (int i = 0; i < _despawnBuffer.Count; i++)
                _context.Units.Despawn(_despawnBuffer[i]);

            _despawnBuffer.Clear();
            _armyCount.Value = 0;

            if (Hero != null)
                Hero.ServerSetSpectator();

            _context.Events.RaisePlayerEliminated(Slot, reason);
        }

        private void OnBuildCompleted(int rosterIndex)
        {
            PlayerBaseAnchor anchor = _context.Players.GetBaseAnchor(Slot);

            UnitEntity unit = _context.Units.Spawn(
                Slot,
                rosterIndex,
                Stats,
                anchor.UnitSpawnPoint,
                Quaternion.Euler(0f, anchor.YawDegrees, 0f),
                Owner);

            if (unit == null)
                return;

            Army.Add(unit);
            _armyCount.Value = (byte)Mathf.Min(byte.MaxValue, Army.AliveCount);
        }

        internal void ServerNotifyUnitLost(UnitEntity unit)
        {
            Army.Remove(unit);
            _armyCount.Value = (byte)Mathf.Min(byte.MaxValue, Army.AliveCount);
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
