using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Match;

namespace Warlord.Networking.Lobby
{
    /// <summary>
    /// Лобби комнаты (ГДД §14): список слотов, выбор цвета, готовность и настройки хоста.
    /// Настройки едут одной структурой и перекрывают дефолты режима при старте матча.
    /// </summary>
    public sealed class LobbyManager : NetworkBehaviour
    {
        private readonly SyncList<LobbySlotInfo> _slots = new();
        private readonly SyncVar<MatchSettings> _settings = new();

        [SerializeField] private MatchManager matchManager;

        public SyncList<LobbySlotInfo> Slots => _slots;
        public MatchSettings Settings => _settings.Value;

        /// <summary>Состав лобби изменился — UI перерисовывает список.</summary>
        public event Action LobbyChanged;

        private void Awake() => matchManager ??= MatchManager.Instance;

        public override void OnStartServer()
        {
            base.OnStartServer();

            matchManager ??= MatchManager.Instance;
            _settings.Value = MatchSettings.FromConfig(matchManager.Config.GameMode);

            int count = matchManager.Config.GameMode.maxPlayers;
            _slots.Clear();
            for (int i = 0; i < count; i++)
                _slots.Add(new LobbySlotInfo { Slot = (byte)i, ColorId = (byte)i, ClientId = -1 });

            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _slots.OnChange += OnSlotsChanged;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _slots.OnChange -= OnSlotsChanged;
        }

        private void OnSlotsChanged(SyncListOperation op, int index, LobbySlotInfo previous, LobbySlotInfo next, bool asServer)
        {
            if (!asServer)
                LobbyChanged?.Invoke();
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                TryOccupySlot(connection);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                ReleaseSlot(connection);
        }

        private void TryOccupySlot(NetworkConnection connection)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                LobbySlotInfo info = _slots[i];
                if (info.Occupied)
                    continue;

                info.Occupied = true;
                info.ClientId = connection.ClientId;
                info.Ready = false;
                _slots[i] = info;
                return;
            }

            Debug.LogWarning($"LobbyManager: свободных слотов нет для клиента {connection.ClientId}");
        }

        private void ReleaseSlot(NetworkConnection connection)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                LobbySlotInfo info = _slots[i];
                if (!info.Occupied || info.ClientId != connection.ClientId)
                    continue;

                info.Occupied = false;
                info.ClientId = -1;
                info.Ready = false;
                _slots[i] = info;
                return;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdSetReady(bool ready, NetworkConnection sender = null)
        {
            int index = FindSlotIndex(sender);
            if (index < 0)
                return;

            LobbySlotInfo info = _slots[index];
            info.Ready = ready;
            _slots[index] = info;
        }

        [ServerRpc(RequireOwnership = false)]
        public void CmdSelectColor(byte colorId, NetworkConnection sender = null)
        {
            int index = FindSlotIndex(sender);
            if (index < 0)
                return;

            // Цвет привязан к слоту в лобби, занятый цвет выбрать нельзя (ГДД §13).
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i != index && _slots[i].Occupied && _slots[i].ColorId == colorId)
                    return;
            }

            LobbySlotInfo info = _slots[index];
            info.ColorId = colorId;
            _slots[index] = info;
        }

        /// <summary>Хост меняет настройки комнаты. Валидация — Sanitized при старте матча.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdUpdateSettings(MatchSettings settings, NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsHost)
                return;

            _settings.Value = settings.Sanitized(matchManager.Config.GameMode);
        }

        /// <summary>Хост запускает матч. Только хост — выход хоста завершает матч (ГДД §12, §16).</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdStartMatch(NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsHost)
                return;

            if (!AllOccupiedReady())
                return;

            matchManager.ServerStartMatch(_settings.Value);
        }

        private bool AllOccupiedReady()
        {
            int occupied = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].Occupied)
                    continue;

                occupied++;
                if (!_slots[i].Ready)
                    return false;
            }

            return occupied >= 1;
        }

        private int FindSlotIndex(NetworkConnection connection)
        {
            if (connection == null)
                return -1;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Occupied && _slots[i].ClientId == connection.ClientId)
                    return i;
            }

            return -1;
        }
    }
}
