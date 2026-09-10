using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Capture;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.World;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Флаг в сцене (ГДД §9, §10). Ставится как обычный объект: перетащить, выбрать роль,
    /// для флага базы — указать базу. Логика двух шкал живёт в <see cref="CaptureLogic"/>,
    /// здесь только связь со сценой и репликация состояния для UI.
    ///
    /// Собирает флаги <see cref="Warlord.Gameplay.Match.MatchManager"/> один раз при старте матча,
    /// поэтому порядок инициализации сценных объектов значения не имеет.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CapturePointBehaviour : NetworkBehaviour
    {
        [Header("Роль")]
        [SerializeField] private CapturePointKind kind = CapturePointKind.CentralFlag;

        [Tooltip("Для флага базы — база, к которой он относится. Слот берётся у неё, вручную дублировать не нужно.")]
        [SerializeField] private PlayerBase owningBase;

        [Tooltip("Запасной слот владельца зоны, если база не указана. -1 — ничей.")]
        [SerializeField] private int zoneOwnerSlot = PlayerSlots.None;

        [Header("Настройки")]
        [Tooltip("Свои параметры захвата вместо общих из GameConfig. Пусто — берутся общие для роли.")]
        [SerializeField] private CapturePointConfig configOverride;

        // Стартовое значение — «ничей», а не 0: слот 0 это живой игрок, и с дефолтным нулём
        // до старта матча флаг на всех клиентах выглядит как захваченный первым игроком.
        private readonly SyncVar<sbyte> _ownerSlot = new((sbyte)PlayerSlots.None, new SyncTypeSettings(0.1f));
        private readonly SyncVar<sbyte> _challengerSlot = new((sbyte)PlayerSlots.None, new SyncTypeSettings(0.1f));
        private readonly SyncVar<byte> _ownerProgress = new(new SyncTypeSettings(0.1f));
        private readonly SyncVar<byte> _challengerProgress = new(new SyncTypeSettings(0.1f));
        private readonly SyncVar<byte> _status = new(new SyncTypeSettings(0.1f));

        // Точка откатывается из-за пустого гарнизона. Владелец при этом прежний, поэтому
        // по _status этого не видно, а кольцо в мире обязано пульсировать (ГДД §2.7).
        private readonly SyncVar<bool> _decaying = new(new SyncTypeSettings(0.2f));

        // Число живых охранников владельца: значок щита над точкой и на миникарте (ГДД §1.9).
        private readonly SyncVar<byte> _guardCount = new(new SyncTypeSettings(0.2f));

        // Индекс выбранного улучшения в наборе или NoUpgrade, если выбора нет (ГДД §2.5).
        private readonly SyncVar<byte> _upgradeIndex = new(NoUpgrade, new SyncTypeSettings(0.2f));

        private readonly List<ICombatTarget> _occupants = new(16);

        private CaptureLogic _logic;
        private CaptureOccupancy _occupancy;
        private CapturePointConfig _config;
        private IMatchContext _context;

        public CapturePointKind Kind => kind;

        /// <summary>Слот зоны: у базы берётся с неё самой, иначе — из поля.</summary>
        public int ZoneOwnerSlot => owningBase != null ? owningBase.Slot : zoneOwnerSlot;

        /// <summary>
        /// Параметры точки. На сервере ставятся при старте матча, на клиенте достаются
        /// из общего конфига по роли: клиенту они нужны для UI — радиус зоны, лимит
        /// гарнизона, наличие слота улучшения, — а <see cref="ServerInitialize"/>
        /// на нём не выполняется никогда.
        /// </summary>
        public CapturePointConfig Config => _config ??= ResolveConfig(_context);

        public int OwnerSlot => _ownerSlot.Value;
        public int ChallengerSlot => _challengerSlot.Value;
        public float OwnerProgress => _ownerProgress.Value / 255f;
        public float ChallengerProgress => _challengerProgress.Value / 255f;
        public CaptureStatus Status => (CaptureStatus)_status.Value;

        /// <summary>Значение индекса улучшения, означающее «улучшение не выбрано».</summary>
        public const byte NoUpgrade = 255;

        /// <summary>Шкала точки убывает, потому что у владельца рядом нет ни одного живого юнита.</summary>
        public bool IsDecaying => _decaying.Value;

        /// <summary>Живые охранники владельца на этой точке.</summary>
        public int GuardCount => _guardCount.Value;

        /// <summary>Индекс улучшения в наборе или <see cref="NoUpgrade"/>.</summary>
        public int UpgradeIndex => _upgradeIndex.Value;

        /// <summary>Выбранное улучшение или null. На клиенте читается из того же SyncVar.</summary>
        public OutpostUpgradeConfig Upgrade
        {
            get
            {
                OutpostUpgradeSetConfig set = ResolveUpgradeSet();
                return set != null ? set.Get(_upgradeIndex.Value) : null;
            }
        }

        /// <summary>Сколько охранников помещается на кольце. Ноль — гарнизон на этой точке не ставится.</summary>
        public int MaxGuards => Config != null ? Config.maxGuards : 0;

        public float GarrisonRingRadius => Config != null ? Config.garrisonRingRadius : 4.5f;

        /// <summary>Радиус зоны захвата. Нужен UI и подсказкам в сцене.</summary>
        public float CaptureRadius => Config != null ? Config.captureRadius : 6f;

        /// <summary>Можно ли назначить точку точкой сбора новых юнитов (ГДД §2.6).</summary>
        public bool AllowsRallyPoint => Config != null && Config.allowsRallyPoint;

        /// <summary>Есть ли у точки слот улучшения.</summary>
        public bool HasUpgradeSlot => Config != null && Config.hasUpgradeSlot;

        /// <summary>Точка перешла к новому владельцу. Аргумент — слот.</summary>
        public event Action<CapturePointBehaviour, int> Captured;

        /// <summary>Точка стала нейтральной. Аргумент — слот бывшего владельца.</summary>
        public event Action<CapturePointBehaviour, int> OwnershipLost;

        /// <summary>
        /// Подписка на смену владельца ставится в Awake, а не в OnStartClient: FishNet
        /// откладывает OnChange от спавн-пакета до стартовых колбэков, но подписаться
        /// нужно до того, как значения прочитаны.
        /// </summary>
        private void Awake()
        {
            _ownerSlot.OnChange += OnOwnerSyncChanged;
        }

        private void OnDestroy()
        {
            _ownerSlot.OnChange -= OnOwnerSyncChanged;
        }

        /// <summary>
        /// Прокидывает смену владельца центра в шину матча на чистом клиенте. Шина локальна
        /// для процесса: на сервере то же событие поднимает <see cref="CentralFlagRewardHandler"/>
        /// из логики захвата, и до клиента оно само по себе не доезжает. Владелец уже реплицирован
        /// SyncVar-ом, поэтому отдельный RPC не нужен — хватает его OnChange.
        /// </summary>
        private void OnOwnerSyncChanged(sbyte previous, sbyte next, bool asServer)
        {
            // На хосте событие уже поднято серверной стороной — иначе лента показала бы захват дважды.
            if (asServer || IsServerInitialized || kind != CapturePointKind.CentralFlag)
                return;

            MatchManager match = MatchManager.Instance;
            if (match != null)
                match.Events.RaiseCentralFlagOwnerChanged(previous, next);
        }

        /// <summary>Инициализация сервером при старте матча. Клиент только читает SyncVar.</summary>
        public void ServerInitialize(IMatchContext context)
        {
            _context = context;
            _config = ResolveConfig(context);

            if (_config == null)
            {
                Debug.LogError($"CapturePointBehaviour: у флага {name} нет параметров захвата", this);
                return;
            }

            int initialOwner = _config.startsOwnedByZoneOwner ? ZoneOwnerSlot : PlayerSlots.None;

            _logic = new CaptureLogic(_config, initialOwner, context.Teams);
            _logic.Captured += OnLogicCaptured;
            _logic.OwnershipLost += OnLogicOwnershipLost;

            _occupancy = new CaptureOccupancy(context.Players.SlotCount, context.Teams);

            PublishState();
        }

        private CapturePointConfig ResolveConfig(IMatchContext context)
        {
            if (configOverride != null)
                return configOverride;

            GameConfig game = context != null ? context.Config : null;

            if (game == null)
            {
                MatchManager match = MatchManager.Instance;
                game = match != null ? match.Config : null;
            }

            if (game == null)
                return null;

            switch (kind)
            {
                case CapturePointKind.CentralFlag: return game.CentralFlag;
                case CapturePointKind.Outpost: return game.Outpost;
                default: return game.BaseFlag;
            }
        }

        /// <summary>Набор улучшений. На клиенте контекста нет, поэтому берётся из общего конфига.</summary>
        private OutpostUpgradeSetConfig ResolveUpgradeSet()
        {
            if (_context != null)
                return _context.Config.OutpostUpgrades;

            MatchManager match = MatchManager.Instance;
            return match != null && match.Config != null ? match.Config.OutpostUpgrades : null;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (_logic == null)
                return;

            _logic.Captured -= OnLogicCaptured;
            _logic.OwnershipLost -= OnLogicOwnershipLost;
        }

        /// <summary>Один серверный такт точки. Вызывается из <see cref="CaptureSystem"/>.</summary>
        public void ServerTick(float deltaTime)
        {
            if (_logic == null || _config == null)
                return;

            CollectOccupants();
            _logic.Tick(deltaTime, _occupancy, HasOwnerGarrison());
            PublishState();
        }

        /// <summary>
        /// Есть ли у владельца рядом хоть один живой юнит (ГДД §2.4). Считаются и охранники,
        /// и полевые юниты: правило «удержание стоит силы» не должно превращаться
        /// в «обязан купить именно охранника» — армия, стоящая на точке, тоже её держит.
        ///
        /// Полководец сюда не входит намеренно: он один, а точек пять, и разреши ему держать
        /// точку собой — вся механика лимита обнулилась бы одним бегом по кругу.
        /// </summary>
        private bool HasOwnerGarrison()
        {
            if (!_config.garrisonDecay)
                return true;

            int owner = _logic.OwnerSlot;
            if (!PlayerSlots.IsValid(owner))
                return true;

            _context.Targeting.CollectInRadius(
                transform.position,
                _config.garrisonCheckRadius,
                CombatantKind.Unit,
                _occupants);

            for (int i = 0; i < _occupants.Count; i++)
            {
                // Союзный юнит держит точку наравне со своим: правило «удержание стоит силы»
                // говорит о силе на точке, а не о том, чьё именно золото на неё потрачено.
                if (_context.Teams.SameSide(_occupants[i].OwnerSlot, owner))
                    return true;
            }

            return false;
        }

        /// <summary>Сбросить точку в нейтраль — например, когда её владелец выбыл из матча.</summary>
        public void ServerForceNeutral()
        {
            _logic?.ForceNeutral();
            PublishState();
        }

        private void CollectOccupants()
        {
            _occupancy.Clear();

            // По ГДД §9 захвату мешают только вражеские полководцы: юниты на точке нужны
            // не для блокировки, а чтобы убить того, кто на ней стоит.
            if (_config.contestedByHeroes)
                AddOccupants(CombatantKind.Hero);

            if (_config.contestedByUnits)
                AddOccupants(CombatantKind.Unit);
        }

        private void AddOccupants(CombatantKind kindFilter)
        {
            _context.Targeting.CollectInRadius(transform.position, _config.captureRadius, kindFilter, _occupants);

            for (int i = 0; i < _occupants.Count; i++)
                _occupancy.Add(_occupants[i].OwnerSlot);
        }

        private void PublishState()
        {
            _ownerSlot.Value = (sbyte)_logic.OwnerSlot;
            _challengerSlot.Value = (sbyte)_logic.ChallengerSlot;
            _ownerProgress.Value = (byte)Mathf.RoundToInt(Mathf.Clamp01(_logic.OwnerProgress) * 255f);
            _challengerProgress.Value = (byte)Mathf.RoundToInt(Mathf.Clamp01(_logic.ChallengerProgress) * 255f);
            _status.Value = (byte)_logic.Status;
            _decaying.Value = _logic.Decaying;
        }

        /// <summary>Число живых охранников владельца. Обновляется раз в такт из <see cref="CaptureSystem"/>.</summary>
        public void ServerSetGuardCount(int count)
        {
            if (!IsServerInitialized)
                return;

            byte value = (byte)Mathf.Clamp(count, 0, byte.MaxValue);

            if (_guardCount.Value != value)
                _guardCount.Value = value;
        }

        /// <summary>
        /// Выбор улучшения владельцем (ГДД §2.5). Валидацию делает вызывающий:
        /// сюда индекс доходит уже проверенным по набору.
        /// </summary>
        public void ServerSetUpgrade(int upgradeIndex)
        {
            if (IsServerInitialized)
                _upgradeIndex.Value = (byte)Mathf.Clamp(upgradeIndex, 0, NoUpgrade);
        }

        /// <summary>Снять улучшение — точка потеряна, эффект прекращается немедленно (ГДД §2.5).</summary>
        public void ServerClearUpgrade()
        {
            if (IsServerInitialized)
                _upgradeIndex.Value = NoUpgrade;
        }

        private void OnLogicCaptured(int newOwnerSlot) => Captured?.Invoke(this, newOwnerSlot);

        private void OnLogicOwnershipLost(int previousOwnerSlot) => OwnershipLost?.Invoke(this, previousOwnerSlot);

        /// <summary>
        /// Радиус зоны в сцене. Без него флаг ставится вслепую: захват идёт по радиусу
        /// из конфига, а не по видимой модели, и промах в пару метров ничем себя не выдаёт.
        /// </summary>
        private void OnDrawGizmos()
        {
            CapturePointConfig preview = configOverride;
            float radius = preview != null ? preview.captureRadius : 6f;

            Gizmos.color = kind switch
            {
                CapturePointKind.CentralFlag => new Color(1f, 0.85f, 0.3f, 0.9f),
                CapturePointKind.Outpost => new Color(0.6f, 1f, 0.5f, 0.9f),
                _ => new Color(0.5f, 0.8f, 1f, 0.9f)
            };

            const int Segments = 40;
            Vector3 center = transform.position;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.DrawLine(center, center + Vector3.up * 3f);

            if (preview == null || preview.maxGuards <= 0)
                return;

            // Кольцо гарнизона. Без него радиус слотов виден только в игре, а он обязан
            // помещаться внутри площадки аванпоста, а не торчать в скалы.
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);

            for (int i = 0; i < preview.maxGuards; i++)
            {
                Vector3 slot = GarrisonLayout.SlotPosition(
                    center, preview.garrisonRingRadius, i, preview.maxGuards);

                Gizmos.DrawWireSphere(slot, 0.5f);
                Gizmos.DrawLine(slot, slot + GarrisonLayout.SlotFacing(center, slot) * 1.2f);
            }
        }
    }
}
