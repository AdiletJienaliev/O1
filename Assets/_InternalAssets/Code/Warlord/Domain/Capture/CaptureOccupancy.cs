using Warlord.Core;

namespace Warlord.Domain.Capture
{
    /// <summary>
    /// Кто сейчас стоит в зоне захвата, посчитанный по слотам.
    /// Переиспользуемый буфер: на 20 Гц нельзя аллоцировать список каждый такт.
    /// </summary>
    public sealed class CaptureOccupancy
    {
        private readonly int[] _countsBySlot;

        public CaptureOccupancy(int slotCount)
        {
            _countsBySlot = new int[slotCount > 0 ? slotCount : PlayerSlots.MaxSupported];
            Clear();
        }

        /// <summary>Сколько разных игроков представлено в зоне.</summary>
        public int DistinctSlots { get; private set; }

        /// <summary>Слот единственного присутствующего игрока или <see cref="PlayerSlots.None"/>.</summary>
        public int SoleSlot { get; private set; }

        public void Clear()
        {
            for (int i = 0; i < _countsBySlot.Length; i++)
                _countsBySlot[i] = 0;

            DistinctSlots = 0;
            SoleSlot = PlayerSlots.None;
        }

        public void Add(int slot)
        {
            if (slot < 0 || slot >= _countsBySlot.Length)
                return;

            if (_countsBySlot[slot] == 0)
            {
                DistinctSlots++;
                SoleSlot = DistinctSlots == 1 ? slot : PlayerSlots.None;
            }

            _countsBySlot[slot]++;
        }

        public int CountFor(int slot)
        {
            return slot >= 0 && slot < _countsBySlot.Length ? _countsBySlot[slot] : 0;
        }
    }
}
