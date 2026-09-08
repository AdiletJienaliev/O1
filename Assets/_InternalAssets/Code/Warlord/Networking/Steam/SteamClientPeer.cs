using System;
using System.Collections.Generic;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Клиентская сторона Steam P2P: одно соединение к SteamID хоста. Адрес приходит из лобби,
    /// поэтому вводить его руками не нужно — и не получится.
    /// </summary>
    internal sealed class SteamClientPeer
    {
        private readonly SteamTransport _transport;
        private readonly SteamPacket _packet = new();
        private readonly IntPtr[] _messages = new IntPtr[SteamPacket.MaxPollMessages];
        private readonly Queue<LocalConnectionState> _states = new();

        private HSteamNetConnection _connection = HSteamNetConnection.Invalid;
        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusChanged;
        private bool _closeRequested;

        internal SteamClientPeer(SteamTransport transport) => _transport = transport;

        internal LocalConnectionState State { get; private set; } = LocalConnectionState.Stopped;

        internal bool Start(string address, int virtualPort)
        {
            if (State != LocalConnectionState.Stopped)
                return false;

            if (!TryParseIdentity(address, out SteamNetworkingIdentity identity))
            {
                Debug.LogError($"SteamTransport: «{address}» не похож на SteamID хоста");
                return false;
            }

            SetState(LocalConnectionState.Starting);

            SteamNetworkingUtils.InitRelayNetworkAccess();

            _statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            _connection = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null);

            if (_connection == HSteamNetConnection.Invalid)
            {
                Debug.LogError("SteamTransport: не удалось начать P2P-соединение с хостом");
                Stop();
                return false;
            }

            return true;
        }

        internal void Stop()
        {
            _closeRequested = false;

            if (State == LocalConnectionState.Stopped)
                return;

            SetState(LocalConnectionState.Stopping);

            if (_connection != HSteamNetConnection.Invalid)
            {
                SteamNetworkingSockets.CloseConnection(_connection, 0, "client shutdown", false);
                _connection = HSteamNetConnection.Invalid;
            }

            _statusChanged?.Dispose();
            _statusChanged = null;

            SetState(LocalConnectionState.Stopped);
        }

        internal void Send(byte channelId, ArraySegment<byte> segment)
        {
            if (State != LocalConnectionState.Started)
                return;

            _packet.Send(_connection, channelId, segment);
        }

        internal void IterateIncoming()
        {
            // Разрыв приходит из колбэка Steam, а закрывать сокет и снимать сам колбэк изнутри
            // его же вызова нельзя — поэтому обрыв только помечается, а разбирается здесь.
            if (_closeRequested)
                Stop();

            FlushStates();

            if (State != LocalConnectionState.Started)
                return;

            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(_connection, _messages, SteamPacket.MaxPollMessages);

            for (int i = 0; i < count; i++)
            {
                if (_packet.Read(_messages[i], out ArraySegment<byte> data, out Channel channel))
                    _transport.HandleClientReceivedDataArgs(new ClientReceivedDataArgs(data, channel, _transport.Index));
            }
        }

        internal void IterateOutgoing()
        {
            if (State == LocalConnectionState.Started)
                SteamNetworkingSockets.FlushMessagesOnConnection(_connection);
        }

        private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t args)
        {
            if (args.m_hConn != _connection || _connection == HSteamNetConnection.Invalid)
                return;

            switch (args.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    SetState(LocalConnectionState.Started);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    Debug.LogWarning($"SteamTransport: соединение с хостом закрыто ({args.m_info.m_szEndDebug})");
                    _closeRequested = true;
                    break;
            }
        }

        /// <summary>
        /// Адрес хоста — это SteamID64 строкой. Отдельный разбор нужен потому, что тот же
        /// метод транспорта используется и для «127.0.0.1»: перепутанный режим должен
        /// заканчиваться внятной ошибкой, а не молчаливым отсутствием соединения.
        /// </summary>
        private static bool TryParseIdentity(string address, out SteamNetworkingIdentity identity)
        {
            identity = default;
            identity.Clear();

            if (!ulong.TryParse(address, out ulong steamId) || steamId == 0)
                return false;

            identity.SetSteamID64(steamId);
            return true;
        }

        private void SetState(LocalConnectionState state)
        {
            State = state;
            _states.Enqueue(state);
        }

        private void FlushStates()
        {
            while (_states.Count > 0)
                _transport.HandleClientConnectionState(new ClientConnectionStateArgs(_states.Dequeue(), _transport.Index));
        }
    }
}
