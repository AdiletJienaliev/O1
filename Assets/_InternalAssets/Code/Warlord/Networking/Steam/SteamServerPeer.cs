using System;
using System.Collections.Generic;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Серверная сторона Steam P2P: слушающий сокет, группа опроса и раздача id клиентам.
    /// </summary>
    /// <remarks>
    /// События FishNet не поднимаются прямо из колбэка Steam: колбэки приходят из
    /// <c>SteamAPI.RunCallbacks</c> в произвольный момент кадра, а FishNet ждёт их на своём
    /// шаге итерации. Поэтому смена состояний копится в очереди и разбирается в
    /// <see cref="IterateIncoming"/> — ровно как это делают штатные транспорты.
    /// </remarks>
    internal sealed class SteamServerPeer
    {
        private readonly SteamTransport _transport;
        private readonly SteamPacket _packet = new();
        private readonly IntPtr[] _messages = new IntPtr[SteamPacket.MaxPollMessages];

        private readonly Dictionary<int, HSteamNetConnection> _connectionById = new();
        private readonly Dictionary<HSteamNetConnection, int> _idByConnection = new();
        private readonly Queue<int> _freeIds = new();
        private readonly Queue<RemoteConnectionStateArgs> _remoteStates = new();
        private readonly Queue<LocalConnectionState> _localStates = new();

        private HSteamListenSocket _listenSocket;
        private HSteamNetPollGroup _pollGroup;
        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusChanged;
        private int _nextId;
        private int _maximumClients;

        internal SteamServerPeer(SteamTransport transport) => _transport = transport;

        /// <summary>Текущее состояние сокета. Обновляется сразу, а не по разбору очереди событий.</summary>
        internal LocalConnectionState State { get; private set; } = LocalConnectionState.Stopped;

        internal int ConnectionCount => _idByConnection.Count;

        internal bool Start(int virtualPort, int maximumClients)
        {
            if (State != LocalConnectionState.Stopped)
                return false;

            _maximumClients = Mathf.Max(1, maximumClients);

            SetState(LocalConnectionState.Starting);

            // Реле Valve поднимается не мгновенно: чем раньше попросим, тем быстрее
            // подключится первый друг из-за NAT.
            SteamNetworkingUtils.InitRelayNetworkAccess();

            _statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);

            if (_listenSocket == HSteamListenSocket.Invalid)
            {
                Debug.LogError("SteamTransport: не удалось открыть слушающий сокет P2P");
                Stop();
                return false;
            }

            SetState(LocalConnectionState.Started);
            return true;
        }

        internal void Stop()
        {
            if (State == LocalConnectionState.Stopped)
                return;

            SetState(LocalConnectionState.Stopping);

            foreach (HSteamNetConnection connection in _connectionById.Values)
                SteamNetworkingSockets.CloseConnection(connection, 0, "server shutdown", false);

            _connectionById.Clear();
            _idByConnection.Clear();
            _freeIds.Clear();
            _nextId = 0;

            if (_pollGroup != HSteamNetPollGroup.Invalid)
            {
                SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
                _pollGroup = HSteamNetPollGroup.Invalid;
            }

            if (_listenSocket != HSteamListenSocket.Invalid)
            {
                SteamNetworkingSockets.CloseListenSocket(_listenSocket);
                _listenSocket = HSteamListenSocket.Invalid;
            }

            _statusChanged?.Dispose();
            _statusChanged = null;

            SetState(LocalConnectionState.Stopped);
        }

        /// <summary>Отключить клиента. immediately — не дожидаться доставки уже отправленного.</summary>
        internal bool Stop(int connectionId, bool immediately)
        {
            if (!_connectionById.TryGetValue(connectionId, out HSteamNetConnection connection))
                return false;

            SteamNetworkingSockets.CloseConnection(connection, 0, "kicked", !immediately);
            Release(connection, connectionId);
            return true;
        }

        internal void Send(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (State != LocalConnectionState.Started)
                return;

            if (_connectionById.TryGetValue(connectionId, out HSteamNetConnection connection))
                _packet.Send(connection, channelId, segment);
        }

        internal void IterateIncoming()
        {
            FlushStates();

            if (State != LocalConnectionState.Started)
                return;

            int count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _messages, SteamPacket.MaxPollMessages);

            for (int i = 0; i < count; i++)
            {
                SteamNetworkingMessage_t header = SteamNetworkingMessage_t.FromIntPtr(_messages[i]);

                if (!_idByConnection.TryGetValue(header.m_conn, out int connectionId))
                {
                    SteamNetworkingMessage_t.Release(_messages[i]);
                    continue;
                }

                if (_packet.Read(_messages[i], out ArraySegment<byte> data, out Channel channel))
                    _transport.HandleServerReceivedDataArgs(new ServerReceivedDataArgs(data, channel, connectionId, _transport.Index));
            }
        }

        internal void IterateOutgoing()
        {
            if (State != LocalConnectionState.Started)
                return;

            foreach (HSteamNetConnection connection in _connectionById.Values)
                SteamNetworkingSockets.FlushMessagesOnConnection(connection);
        }

        internal RemoteConnectionState GetConnectionState(int connectionId)
        {
            return _connectionById.ContainsKey(connectionId)
                ? RemoteConnectionState.Started
                : RemoteConnectionState.Stopped;
        }

        /// <summary>SteamID64 клиента — им подписываются логи и баны.</summary>
        internal string GetAddress(int connectionId)
        {
            if (!_connectionById.TryGetValue(connectionId, out HSteamNetConnection connection))
                return string.Empty;

            if (!SteamNetworkingSockets.GetConnectionInfo(connection, out SteamNetConnectionInfo_t info))
                return string.Empty;

            return info.m_identityRemote.GetSteamID64().ToString();
        }

        internal void SetMaximumClients(int value) => _maximumClients = Mathf.Max(1, value);

        internal int GetMaximumClients() => _maximumClients;

        private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t args)
        {
            // Колбэк общий на процесс: у хоста здесь же появляются собственные исходящие
            // соединения. Наши — только те, что пришли на наш слушающий сокет.
            if (args.m_info.m_hListenSocket != _listenSocket || _listenSocket == HSteamListenSocket.Invalid)
                return;

            switch (args.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    Accept(args.m_hConn);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    Register(args.m_hConn);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    SteamNetworkingSockets.CloseConnection(args.m_hConn, 0, "peer closed", false);

                    if (_idByConnection.TryGetValue(args.m_hConn, out int closedId))
                        Release(args.m_hConn, closedId);

                    break;
            }
        }

        private void Accept(HSteamNetConnection connection)
        {
            if (_idByConnection.Count >= _maximumClients)
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, "server full", false);
                return;
            }

            if (SteamNetworkingSockets.AcceptConnection(connection) != EResult.k_EResultOK)
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, "accept failed", false);
                return;
            }

            SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);
        }

        private void Register(HSteamNetConnection connection)
        {
            if (_idByConnection.ContainsKey(connection))
                return;

            int id = _freeIds.Count > 0 ? _freeIds.Dequeue() : _nextId++;

            _idByConnection[connection] = id;
            _connectionById[id] = connection;

            // Группу назначаем и здесь: соединение могло дойти до Connected, минуя наш Accept
            // (так бывает, когда Steam принял его сам по уже открытой сессии).
            SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);

            _remoteStates.Enqueue(new RemoteConnectionStateArgs(RemoteConnectionState.Started, id, _transport.Index));
        }

        private void Release(HSteamNetConnection connection, int connectionId)
        {
            _idByConnection.Remove(connection);
            _connectionById.Remove(connectionId);
            _freeIds.Enqueue(connectionId);

            _remoteStates.Enqueue(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, connectionId, _transport.Index));
        }

        private void SetState(LocalConnectionState state)
        {
            State = state;
            _localStates.Enqueue(state);
        }

        private void FlushStates()
        {
            while (_localStates.Count > 0)
                _transport.HandleServerConnectionState(new ServerConnectionStateArgs(_localStates.Dequeue(), _transport.Index));

            while (_remoteStates.Count > 0)
                _transport.HandleRemoteConnectionState(_remoteStates.Dequeue());
        }
    }
}
