using System;
using System.Runtime.InteropServices;
using FishNet.Transporting;
using Steamworks;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Упаковка и распаковка сообщений Steam. Существует по одной причине: Steam не передаёт
    /// вместе с сообщением канал FishNet, поэтому номер канала едет первым байтом полезной
    /// нагрузки. Здесь же лежат буферы — по одному на сокет, чтобы каждая отправка не
    /// выделяла массив заново.
    /// </summary>
    internal sealed class SteamPacket
    {
        /// <summary>Сколько сообщений забираем за один опрос сокета.</summary>
        internal const int MaxPollMessages = 256;

        private byte[] _sendBuffer = new byte[1024];
        private byte[] _receiveBuffer = new byte[1024];

        /// <summary>Флаги отправки Steam по каналу FishNet: 0 — надёжный, 1 — нет.</summary>
        internal static int SendFlags(byte channelId)
        {
            return channelId == (byte)Channel.Reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;
        }

        /// <summary>Отправить сегмент в соединение, дописав перед ним номер канала.</summary>
        internal EResult Send(HSteamNetConnection connection, byte channelId, ArraySegment<byte> segment)
        {
            int length = segment.Count + 1;
            EnsureCapacity(ref _sendBuffer, length);

            _sendBuffer[0] = channelId;

            if (segment.Count > 0)
                Buffer.BlockCopy(segment.Array, segment.Offset, _sendBuffer, 1, segment.Count);

            GCHandle pinned = GCHandle.Alloc(_sendBuffer, GCHandleType.Pinned);

            try
            {
                return SteamNetworkingSockets.SendMessageToConnection(
                    connection,
                    pinned.AddrOfPinnedObject(),
                    (uint)length,
                    SendFlags(channelId),
                    out long _);
            }
            finally
            {
                pinned.Free();
            }
        }

        /// <summary>
        /// Прочитать сообщение Steam по указателю и освободить его. Возвращает false для
        /// пустых сообщений: в них нет даже байта канала, и разбирать там нечего.
        /// </summary>
        internal bool Read(IntPtr messagePointer, out ArraySegment<byte> data, out Channel channel)
        {
            SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(messagePointer);
            int size = message.m_cbSize;

            if (size < 1)
            {
                SteamNetworkingMessage_t.Release(messagePointer);
                data = default;
                channel = Channel.Reliable;
                return false;
            }

            EnsureCapacity(ref _receiveBuffer, size);
            Marshal.Copy(message.m_pData, _receiveBuffer, 0, size);
            SteamNetworkingMessage_t.Release(messagePointer);

            channel = _receiveBuffer[0] == (byte)Channel.Unreliable ? Channel.Unreliable : Channel.Reliable;
            data = new ArraySegment<byte>(_receiveBuffer, 1, size - 1);
            return true;
        }

        private static void EnsureCapacity(ref byte[] buffer, int required)
        {
            if (buffer.Length >= required)
                return;

            int size = buffer.Length;

            while (size < required)
                size *= 2;

            buffer = new byte[size];
        }
    }
}
