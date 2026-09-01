using System;

namespace Warlord.Networking.Lobby
{
    /// <summary>Одна строка списка игроков в лобби (ГДД §14).</summary>
    [Serializable]
    public struct LobbySlotInfo : IEquatable<LobbySlotInfo>
    {
        public int ClientId;
        public byte Slot;
        public byte ColorId;
        public bool Ready;
        public bool Occupied;

        public bool Equals(LobbySlotInfo other)
        {
            return ClientId == other.ClientId
                && Slot == other.Slot
                && ColorId == other.ColorId
                && Ready == other.Ready
                && Occupied == other.Occupied;
        }

        public override bool Equals(object obj) => obj is LobbySlotInfo other && Equals(other);

        public override int GetHashCode() => (ClientId * 397) ^ (Slot << 8) ^ ColorId;
    }
}
