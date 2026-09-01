using System;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Запись очереди спавна, видимая владельцу. Прогресс не синкается покадрово:
    /// уходит только тик завершения, а полоску клиент рисует сам от текущего тика.
    /// </summary>
    [Serializable]
    public struct SpawnTicket : IEquatable<SpawnTicket>
    {
        /// <summary>Индекс типа в ростере.</summary>
        public byte RosterIndex;

        /// <summary>Тик, на котором юнит появится. 0 — ещё не начал строиться.</summary>
        public uint FinishTick;

        /// <summary>Длительность постройки в тиках. Нужна клиенту для знаменателя прогресса.</summary>
        public ushort DurationTicks;

        public bool InProgress => FinishTick > 0u;

        public bool Equals(SpawnTicket other)
        {
            return RosterIndex == other.RosterIndex
                && FinishTick == other.FinishTick
                && DurationTicks == other.DurationTicks;
        }

        public override bool Equals(object obj) => obj is SpawnTicket other && Equals(other);

        public override int GetHashCode() => (int)(FinishTick * 397u) ^ RosterIndex;
    }
}
