using System;
using Warlord.Core;

namespace Warlord.Networking.Lobby
{
    /// <summary>
    /// Одна строка списка игроков в лобби (ГДД §14). Она же — стартовый состав матча:
    /// кто занимает слот, за какую команду играет и, если это бот, с каким характером
    /// и на какой сложности.
    ///
    /// Про бота едут три байта, а не имя строкой: имя одинаково выводится из набора ботов
    /// и на сервере, и на клиенте, а строка в SyncList стоила бы трафика на каждое
    /// изменение готовности.
    /// </summary>
    [Serializable]
    public struct LobbySlotInfo : IEquatable<LobbySlotInfo>
    {
        public int ClientId;
        public byte Slot;
        public byte ColorId;
        public bool Ready;

        /// <summary>Кто занимает слот: никто, человек или бот.</summary>
        public byte Kind;

        /// <summary>Команда или -1, если слот сам за себя (обычный FFA).</summary>
        public sbyte TeamId;

        /// <summary>Индекс характера в наборе ботов. Значим только для бота.</summary>
        public byte BotPersonality;

        /// <summary>Сложность бота как <see cref="Warlord.Core.BotDifficulty"/>.</summary>
        public byte BotDifficulty;

        /// <summary>Номер имени внутри пула характера: два одинаковых бота зовутся по-разному.</summary>
        public byte BotNameIndex;

        public SlotKind SlotKind => (SlotKind)Kind;

        public bool Occupied => SlotKind != Core.SlotKind.Empty;

        public bool IsBot => SlotKind == Core.SlotKind.Bot;

        public bool IsHuman => SlotKind == Core.SlotKind.Human;

        /// <summary>Пустая строка слота. Команда снята, бот сброшен.</summary>
        public static LobbySlotInfo Empty(int slot)
        {
            return new LobbySlotInfo
            {
                Slot = (byte)slot,
                ColorId = (byte)slot,
                ClientId = -1,
                Kind = (byte)Core.SlotKind.Empty,
                TeamId = -1,
                Ready = false
            };
        }

        public bool Equals(LobbySlotInfo other)
        {
            return ClientId == other.ClientId
                && Slot == other.Slot
                && ColorId == other.ColorId
                && Ready == other.Ready
                && Kind == other.Kind
                && TeamId == other.TeamId
                && BotPersonality == other.BotPersonality
                && BotDifficulty == other.BotDifficulty
                && BotNameIndex == other.BotNameIndex;
        }

        public override bool Equals(object obj) => obj is LobbySlotInfo other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ClientId;
            hash = hash * 397 ^ (Slot << 8);
            hash = hash * 397 ^ ColorId;
            hash = hash * 397 ^ Kind;
            hash = hash * 397 ^ TeamId;
            hash = hash * 397 ^ BotPersonality;
            hash = hash * 397 ^ BotDifficulty;
            return hash;
        }
    }
}
