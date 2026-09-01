using Warlord.Core;

namespace Warlord.Domain.Match
{
    /// <summary>
    /// Таблица счёта матча. Хранится только на сервере, наружу уходит снимком.
    /// Счёт выбывшего игрока сохраняется (ГДД §10.4) — он просто перестаёт расти.
    /// </summary>
    public sealed class ScoreBoard
    {
        private readonly PlayerScore[] _scores;

        public ScoreBoard(int slotCount)
        {
            _scores = new PlayerScore[slotCount > 0 ? slotCount : PlayerSlots.MaxSupported];
        }

        public int SlotCount => _scores.Length;

        public PlayerScore Get(int slot) => InRange(slot) ? _scores[slot] : default;

        public void AddFlagHoldTime(int slot, float seconds)
        {
            if (InRange(slot))
                _scores[slot].FlagHoldSeconds += seconds;
        }

        public void AddFlagCapture(int slot)
        {
            if (InRange(slot))
                _scores[slot].FlagCaptures++;
        }

        public void AddBaseCapture(int slot)
        {
            if (InRange(slot))
                _scores[slot].BasesCaptured++;
        }

        public void AddUnitKill(int slot)
        {
            if (InRange(slot))
                _scores[slot].UnitKills++;
        }

        public void CopyTo(PlayerScore[] destination)
        {
            int count = destination.Length < _scores.Length ? destination.Length : _scores.Length;
            for (int i = 0; i < count; i++)
                destination[i] = _scores[i];
        }

        public void Reset()
        {
            for (int i = 0; i < _scores.Length; i++)
                _scores[i] = default;
        }

        private bool InRange(int slot) => slot >= 0 && slot < _scores.Length;
    }
}
