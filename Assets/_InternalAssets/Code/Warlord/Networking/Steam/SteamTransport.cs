using System;
using FishNet.Managing;
using FishNet.Transporting;
using HeathenEngineering.SteamworksIntegration;
using UnityEngine;
using API = HeathenEngineering.SteamworksIntegration.API;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Транспорт FishNet поверх Steam P2P (ISteamNetworkingSockets). Адрес клиента — SteamID64
    /// хоста, порт не используется: за обход NAT и подбор маршрута отвечает сеть Valve.
    /// </summary>
    /// <remarks>
    /// Транспорт ничего не знает про лобби и приглашения — он получает готовый SteamID хоста.
    /// Откуда тот взялся (приглашение, ссылка запуска, поиск комнат), решает
    /// <see cref="SteamSession"/>, и менять способ подбора игроков можно, не трогая сеть.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Transport/Warlord Steam")]
    public sealed class SteamTransport : Transport
    {
        [Header("Steam P2P")]
        [Tooltip("Виртуальный порт P2P. Менять нужно, только если в одном приложении живёт " +
                 "несколько независимых серверов.")]
        [SerializeField] private int virtualPort;

        [Tooltip("Максимум клиентов на сервере, не считая самого хоста.")]
        [Range(1, 32)]
        [SerializeField] private int maximumClients = 8;

        [Tooltip("SteamID64 хоста. Обычно проставляется кодом из лобби, руками — только для проверок.")]
        [SerializeField] private string clientAddress = string.Empty;

        /// <summary>
        /// Потолок полезной нагрузки. Steam разрешает сильно больше, но резать данные на куски
        /// размером с обычный UDP-пакет дешевле, чем полагаться на его сборку по частям.
        /// </summary>
        private const int Mtu = 1200;

        private SteamServerPeer _server;
        private SteamClientPeer _client;
        private SteamHostPeer _host;

        #region Инициализация

        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);

            _server = new SteamServerPeer(this);
            _client = new SteamClientPeer(this);
            _host = new SteamHostPeer(this);
        }

        private void OnDestroy() => Shutdown();

        #endregion

        #region События

        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

        public override void HandleClientConnectionState(ClientConnectionStateArgs args) => OnClientConnectionState?.Invoke(args);
        public override void HandleServerConnectionState(ServerConnectionStateArgs args) => OnServerConnectionState?.Invoke(args);
        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs args) => OnRemoteConnectionState?.Invoke(args);
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs args) => OnClientReceivedData?.Invoke(args);
        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs args) => OnServerReceivedData?.Invoke(args);

        #endregion

        #region Состояния

        public override LocalConnectionState GetConnectionState(bool server)
        {
            if (server)
                return _server?.State ?? LocalConnectionState.Stopped;

            if (_host != null && _host.State != LocalConnectionState.Stopped)
                return _host.State;

            return _client?.State ?? LocalConnectionState.Stopped;
        }

        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
            if (connectionId == SteamHostPeer.ClientId)
                return _host?.GetConnectionState(connectionId) ?? RemoteConnectionState.Stopped;

            return _server?.GetConnectionState(connectionId) ?? RemoteConnectionState.Stopped;
        }

        public override string GetConnectionAddress(int connectionId)
        {
            if (connectionId == SteamHostPeer.ClientId)
                return UserData.Me.SteamId.ToString();

            return _server != null ? _server.GetAddress(connectionId) : string.Empty;
        }

        /// <summary>Клиент хоста живёт в этом же процессе — проверки чужого трафика ему не нужны.</summary>
        public override bool IsLocalTransport(int connectionId) => connectionId == SteamHostPeer.ClientId;

        #endregion

        #region Старт и остановка

        public override bool StartConnection(bool server) => server ? StartServer() : StartClient();

        public override bool StopConnection(bool server)
        {
            if (server)
            {
                // Клиент хоста висит на этом сервере: оставить его после остановки нельзя,
                // иначе FishNet будет считать себя подключённым к тому, чего уже нет.
                _host?.Stop();
                _server?.Stop();
                return true;
            }

            if (_host != null && _host.State != LocalConnectionState.Stopped)
                _host.Stop();
            else
                _client?.Stop();

            return true;
        }

        public override bool StopConnection(int connectionId, bool immediately)
        {
            if (connectionId == SteamHostPeer.ClientId)
            {
                _host?.Stop();
                return true;
            }

            return _server != null && _server.Stop(connectionId, immediately);
        }

        public override void Shutdown()
        {
            StopConnection(server: false);
            StopConnection(server: true);
        }

        private bool StartServer()
        {
            if (!IsSteamReady("сервер"))
                return false;

            return _server.Start(virtualPort, maximumClients);
        }

        private bool StartClient()
        {
            // Сервер уже поднят в этом процессе — значит, мы хост и подключаемся к себе.
            if (_server != null && _server.State == LocalConnectionState.Started)
            {
                _host.Start();
                return true;
            }

            if (!IsSteamReady("клиент"))
                return false;

            return _client.Start(clientAddress, virtualPort);
        }

        private bool IsSteamReady(string role)
        {
            if (API.App.Initialized)
                return true;

            Debug.LogError($"SteamTransport: Steam не инициализирован — {role} не поднять. " +
                           "Проверьте SteamSettings и запущен ли клиент Steam.", this);
            return false;
        }

        #endregion

        #region Обмен данными

        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (_host != null && _host.State != LocalConnectionState.Stopped)
                _host.SendToServer(channelId, segment);
            else
                _client?.Send(channelId, segment);
        }

        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (connectionId == SteamHostPeer.ClientId)
                _host?.SendToClient(channelId, segment);
            else
                _server?.Send(channelId, segment, connectionId);
        }

        public override void IterateIncoming(bool asServer)
        {
            if (asServer)
                _server?.IterateIncoming();
            else
                _client?.IterateIncoming();

            _host?.IterateIncoming(asServer);
        }

        public override void IterateOutgoing(bool asServer)
        {
            if (asServer)
                _server?.IterateOutgoing();
            else
                _client?.IterateOutgoing();
        }

        #endregion

        #region Настройки

        public override int GetMTU(byte channel) => Mtu;

        public override int GetMaximumClients() => _server != null ? _server.GetMaximumClients() : maximumClients;

        public override void SetMaximumClients(int value)
        {
            maximumClients = value;
            _server?.SetMaximumClients(value);
        }

        public override string GetClientAddress() => clientAddress;

        public override void SetClientAddress(string address) => clientAddress = address;

        /// <summary>Порт в P2P не используется: маршрут строит сеть Steam по SteamID.</summary>
        public override ushort GetPort() => 0;

        public override void SetPort(ushort port) { }

        #endregion
    }
}
