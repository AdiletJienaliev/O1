using System;
using System.Collections.Generic;
using FishNet.Transporting;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Клиент хоста: сервер и клиент в одном процессе обмениваются данными через две очереди,
    /// минуя Steam.
    /// </summary>
    /// <remarks>
    /// Гонять пакеты хоста самому себе через P2P-реле Valve — лишний круг: лишняя задержка,
    /// лишний трафик и зависимость собственного матча от связи с сетью Steam. Поэтому, когда
    /// сервер поднят в этом же процессе, локальный клиент всегда идёт по этой трубе, а Steam
    /// остаётся только для чужих.
    /// </remarks>
    internal sealed class SteamHostPeer
    {
        /// <summary>
        /// Id клиента хоста на сервере. Взят с потолка диапазона, чтобы не пересечься
        /// с id, которые сервер раздаёт подключившимся по Steam с нуля вверх.
        /// </summary>
        internal const int ClientId = short.MaxValue;

        private readonly SteamTransport _transport;
        private readonly Queue<Packet> _toServer = new();
        private readonly Queue<Packet> _toClient = new();
        private readonly Queue<LocalConnectionState> _clientStates = new();
        private readonly Queue<RemoteConnectionStateArgs> _serverStates = new();
        private readonly Stack<byte[]> _pool = new();

        internal SteamHostPeer(SteamTransport transport) => _transport = transport;

        internal LocalConnectionState State { get; private set; } = LocalConnectionState.Stopped;

        internal void Start()
        {
            if (State != LocalConnectionState.Stopped)
                return;

            SetClientState(LocalConnectionState.Starting);
            _serverStates.Enqueue(new RemoteConnectionStateArgs(RemoteConnectionState.Started, ClientId, _transport.Index));
            SetClientState(LocalConnectionState.Started);
        }

        internal void Stop()
        {
            if (State == LocalConnectionState.Stopped)
                return;

            SetClientState(LocalConnectionState.Stopping);
            _serverStates.Enqueue(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, ClientId, _transport.Index));

            Discard(_toServer);
            Discard(_toClient);

            SetClientState(LocalConnectionState.Stopped);
        }

        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (State == LocalConnectionState.Started)
                _toServer.Enqueue(Copy(channelId, segment));
        }

        internal void SendToClient(byte channelId, ArraySegment<byte> segment)
        {
            if (State == LocalConnectionState.Started)
                _toClient.Enqueue(Copy(channelId, segment));
        }

        internal void IterateIncoming(bool asServer)
        {
            if (asServer)
            {
                while (_serverStates.Count > 0)
                    _transport.HandleRemoteConnectionState(_serverStates.Dequeue());

                Dispatch(_toServer, asServer: true);
            }
            else
            {
                while (_clientStates.Count > 0)
                    _transport.HandleClientConnectionState(new ClientConnectionStateArgs(_clientStates.Dequeue(), _transport.Index));

                Dispatch(_toClient, asServer: false);
            }
        }

        internal RemoteConnectionState GetConnectionState(int connectionId)
        {
            return connectionId == ClientId && State == LocalConnectionState.Started
                ? RemoteConnectionState.Started
                : RemoteConnectionState.Stopped;
        }

        /// <summary>
        /// Разбирает очередь по счётчику, снятому до начала обхода: обработка пакета может
        /// положить в ту же очередь ответ, и цикл «пока не пусто» крутился бы вечно.
        /// </summary>
        private void Dispatch(Queue<Packet> queue, bool asServer)
        {
            int count = queue.Count;

            for (int i = 0; i < count; i++)
            {
                Packet packet = queue.Dequeue();
                ArraySegment<byte> data = new(packet.Buffer, 0, packet.Length);

                if (asServer)
                    _transport.HandleServerReceivedDataArgs(new ServerReceivedDataArgs(data, packet.Channel, ClientId, _transport.Index));
                else
                    _transport.HandleClientReceivedDataArgs(new ClientReceivedDataArgs(data, packet.Channel, _transport.Index));

                _pool.Push(packet.Buffer);
            }
        }

        private void Discard(Queue<Packet> queue)
        {
            while (queue.Count > 0)
                _pool.Push(queue.Dequeue().Buffer);
        }

        /// <summary>
        /// Копия обязательна: FishNet отдаёт сегмент своего переиспользуемого буфера,
        /// а прочитан он будет только на следующей итерации.
        /// </summary>
        private Packet Copy(byte channelId, ArraySegment<byte> segment)
        {
            byte[] buffer = Rent(segment.Count);

            if (segment.Count > 0)
                Buffer.BlockCopy(segment.Array, segment.Offset, buffer, 0, segment.Count);

            Channel channel = channelId == (byte)Channel.Unreliable ? Channel.Unreliable : Channel.Reliable;
            return new Packet(buffer, segment.Count, channel);
        }

        private byte[] Rent(int length)
        {
            while (_pool.Count > 0)
            {
                byte[] candidate = _pool.Pop();

                if (candidate.Length >= length)
                    return candidate;
            }

            // Нижняя граница взята с запасом над MTU: так в пуле не заводятся буферы,
            // которые не подойдут следующему же пакету и будут выброшены.
            return new byte[Math.Max(length, 2048)];
        }

        private void SetClientState(LocalConnectionState state)
        {
            State = state;
            _clientStates.Enqueue(state);
        }

        private readonly struct Packet
        {
            internal readonly byte[] Buffer;
            internal readonly int Length;
            internal readonly Channel Channel;

            internal Packet(byte[] buffer, int length, Channel channel)
            {
                Buffer = buffer;
                Length = length;
                Channel = channel;
            }
        }
    }
}
