using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
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

        private readonly List<ICombatTarget> _occupants = new(16);

        private CaptureLogic _logic;
        private CaptureOccupancy _occupancy;
        private CapturePointConfig _config;
        private IMatchContext _context;

        public CapturePointKind Kind => kind;

        /// <summary>Слот зоны: у базы берётся с неё самой, иначе — из поля.</summary>
        public int ZoneOwnerSlot => owningBase != null ? owningBase.Slot : zoneOwnerSlot;

        public CapturePointConfig Config => _config;

        public int OwnerSlot => _ownerSlot.Value;
        public int ChallengerSlot => _challengerSlot.Value;
        public float OwnerProgress => _ownerProgress.Value / 255f;
        public float ChallengerProgress => _challengerProgress.Value / 255f;
        public CaptureStatus Status => (CaptureStatus)_status.Value;

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

            _logic = new CaptureLogic(_config, initialOwner);
            _logic.Captured += OnLogicCaptured;
            _logic.OwnershipLost += OnLogicOwnershipLost;

            _occupancy = new CaptureOccupancy(context.Players.SlotCount);

            PublishState();
        }

        private CapturePointConfig ResolveConfig(IMatchContext context)
        {
            if (configOverride != null)
                return configOverride;

            return kind == CapturePointKind.CentralFlag
                ? context.Config.CentralFlag
                : context.Config.BaseFlag;
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
            _logic.Tick(deltaTime, _occupancy);
            PublishState();
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

            Gizmos.color = kind == CapturePointKind.CentralFlag
                ? new Color(1f, 0.85f, 0.3f, 0.9f)
                : new Color(0.5f, 0.8f, 1f, 0.9f);

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
        }
    }
}
