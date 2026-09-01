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

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Точка захвата в сцене (ГДД §9, §10). Логика двух шкал живёт в <see cref="CaptureLogic"/>,
    /// здесь только связь со сценой и репликация состояния для UI.
    /// </summary>
    public sealed class CapturePointBehaviour : NetworkBehaviour
    {
        [Header("Роль")]
        [SerializeField] private CapturePointKind kind = CapturePointKind.CentralFlag;

        [Tooltip("Для флага базы — слот игрока, которому база принадлежит. Для центра не используется.")]
        [SerializeField] private int zoneOwnerSlot = PlayerSlots.None;

        private readonly SyncVar<sbyte> _ownerSlot = new(new SyncTypeSettings(0.1f));
        private readonly SyncVar<sbyte> _challengerSlot = new(new SyncTypeSettings(0.1f));
        private readonly SyncVar<byte> _ownerProgress = new(new SyncTypeSettings(0.1f));
        private readonly SyncVar<byte> _challengerProgress = new(new SyncTypeSettings(0.1f));
        private readonly SyncVar<byte> _status = new(new SyncTypeSettings(0.1f));

        private readonly List<ICombatTarget> _occupants = new(16);

        private CaptureLogic _logic;
        private CaptureOccupancy _occupancy;
        private CapturePointConfig _config;
        private IMatchContext _context;

        public CapturePointKind Kind => kind;
        public int ZoneOwnerSlot => zoneOwnerSlot;
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

        /// <summary>Инициализация сервером при старте матча. Клиент только читает SyncVar.</summary>
        public void ServerInitialize(IMatchContext context)
        {
            _context = context;
            _config = kind == CapturePointKind.CentralFlag
                ? context.Config.CentralFlag
                : context.Config.BaseFlag;

            int initialOwner = _config.startsOwnedByZoneOwner ? zoneOwnerSlot : PlayerSlots.None;

            _logic = new CaptureLogic(_config, initialOwner);
            _logic.Captured += OnLogicCaptured;
            _logic.OwnershipLost += OnLogicOwnershipLost;

            _occupancy = new CaptureOccupancy(context.Players.SlotCount);

            PublishState();
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
    }
}
