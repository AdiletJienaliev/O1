using Warlord.Core;

namespace Warlord.Domain.Capture
{
    /// <summary>
    /// Кто сейчас стоит в зоне захвата. Считается по слотам, а сравнивается по сторонам:
    /// два союзника на точке — это одна сторона, которая её берёт вдвое быстрее,
    /// а не «двое разных», которые заморозили бы захват друг другу (ГДД §9.2).
    ///
    /// Переиспользуемый буфер: на 20 Гц нельзя аллоцировать список каждый такт.
    /// </summary>
    public sealed class CaptureOccupancy
    {
        private readonly int[] _countsBySlot;
        private readonly TeamLayout _teams;

        private bool _dirty = true;
        private int _distinctSides;
        private int _soleSlot = PlayerSlots.None;

        public CaptureOccupancy(int slotCount, TeamLayout teams = default)
        {
            _countsBySlot = new int[slotCount > 0 ? slotCount : PlayerSlots.MaxSupported];
            _teams = teams;
            Clear();
        }

        /// <summary>Сколько разных сторон представлено в зоне. Две и более — захват заморожен.</summary>
        public int DistinctSides
        {
            get
            {
                Recalculate();
                return _distinctSides;
            }
        }

        /// <summary>
        /// Слот, от имени которого сторона держит точку, или <see cref="PlayerSlots.None"/>.
        /// Из союзников выбирается тот, кто привёл больше тел: точка достаётся тому,
        /// кто её действительно взял, а не тому, у кого номер слота меньше.
        /// </summary>
        public int SoleSlot
        {
            get
            {
                Recalculate();
                return _soleSlot;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _countsBySlot.Length; i++)
                _countsBySlot[i] = 0;

            _dirty = true;
        }

        public void Add(int slot)
        {
            if (slot < 0 || slot >= _countsBySlot.Length)
                return;

            _countsBySlot[slot]++;
            _dirty = true;
        }

        /// <summary>Сколько тел на точке у стороны этого слота. По этому числу считается ускорение захвата.</summary>
        public int CountForSide(int slot)
        {
            if (slot < 0 || slot >= _countsBySlot.Length)
                return 0;

            int total = 0;

            for (int i = 0; i < _countsBySlot.Length; i++)
            {
                if (_countsBySlot[i] > 0 && _teams.SameSide(i, slot))
                    total += _countsBySlot[i];
            }

            return total;
        }

        /// <summary>
        /// Пересчёт сторон. Ленивый и полный, а не инкрементальный при каждом Add:
        /// слотов максимум четыре, и полный проход раз в такт дешевле, чем поддержка
        /// инкрементального состояния, которое обязано согласовываться с раскладкой команд.
        /// </summary>
        private void Recalculate()
        {
            if (!_dirty)
                return;

            _dirty = false;
            _distinctSides = 0;
            _soleSlot = PlayerSlots.None;

            int bestCount = 0;

            for (int i = 0; i < _countsBySlot.Length; i++)
            {
                if (_countsBySlot[i] == 0)
                    continue;

                bool newSide = true;

                for (int j = 0; j < i; j++)
                {
                    if (_countsBySlot[j] > 0 && _teams.SameSide(j, i))
                    {
                        newSide = false;
                        break;
                    }
                }

                if (newSide)
                    _distinctSides++;

                if (_countsBySlot[i] > bestCount)
                {
                    bestCount = _countsBySlot[i];
                    _soleSlot = i;
                }
            }

            if (_distinctSides != 1)
                _soleSlot = PlayerSlots.None;
        }
    }
}
