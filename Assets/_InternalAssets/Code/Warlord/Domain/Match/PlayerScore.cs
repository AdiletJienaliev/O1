using System;

namespace Warlord.Domain.Match
{
    /// <summary>
    /// Счёт одного игрока. Время удержания центрального флага — основной критерий победы,
    /// остальные поля работают как тай-брейки (ГДД §2).
    /// </summary>
    [Serializable]
    public struct PlayerScore
    {
        public float FlagHoldSeconds;
        public ushort FlagCaptures;
        public ushort BasesCaptured;
        public ushort UnitKills;
    }
}
