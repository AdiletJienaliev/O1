using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Domain.Formations
{
    /// <summary>
    /// Одна клетка пользовательской расстановки: какой тип юнита и где он стоит.
    /// Координаты целочисленные и в клетках, а не в метрах: игрок расставляет армию
    /// по сетке, а шаг сетки задаётся построением — так один и тот же пресет
    /// одинаково работает и при плотном, и при разреженном строе.
    /// </summary>
    public readonly struct ArmyPresetSlot
    {
        /// <summary>Индекс типа в ростере.</summary>
        public readonly byte RosterIndex;

        /// <summary>Колонка. 0 — по центру строя, минус — влево.</summary>
        public readonly sbyte Column;

        /// <summary>Шеренга. 0 — передняя, дальше растёт назад.</summary>
        public readonly sbyte Row;

        public ArmyPresetSlot(byte rosterIndex, sbyte column, sbyte row)
        {
            RosterIndex = rosterIndex;
            Column = column;
            Row = row;
        }
    }

    /// <summary>
    /// Пользовательская расстановка армии (пресет). Игрок раскладывает типы юнитов
    /// по сетке до боя, а дальше пресет работает как ещё одно построение наравне
    /// с линией, квадратом и клином.
    ///
    /// Хранит только форму, но не конкретных юнитов: армия меняется каждый бой,
    /// и пресет должен переживать и потери, и докупку.
    /// </summary>
    public sealed class ArmyPreset
    {
        /// <summary>Потолок клеток. Больше юнитов в армии всё равно не бывает (ГДД §5.1).</summary>
        public const int MaxSlots = 64;

        private const int BytesPerSlot = 3;

        private readonly List<ArmyPresetSlot> _slots = new(MaxSlots);

        public IReadOnlyList<ArmyPresetSlot> Slots => _slots;

        public int Count => _slots.Count;

        public bool IsEmpty => _slots.Count == 0;

        public void Clear() => _slots.Clear();

        public void Add(in ArmyPresetSlot slot)
        {
            if (_slots.Count < MaxSlots)
                _slots.Add(slot);
        }

        /// <summary>
        /// Упаковка для сети: три байта на клетку. Пресет ходит целиком и редко —
        /// его отправляют один раз при настройке, поэтому дельты и версии тут излишни.
        /// </summary>
        public byte[] Pack()
        {
            byte[] data = new byte[_slots.Count * BytesPerSlot];

            for (int i = 0; i < _slots.Count; i++)
            {
                ArmyPresetSlot slot = _slots[i];
                int offset = i * BytesPerSlot;

                data[offset] = slot.RosterIndex;
                data[offset + 1] = (byte)(slot.Column + 128);
                data[offset + 2] = (byte)(slot.Row + 128);
            }

            return data;
        }

        /// <summary>
        /// Разбор присланного клиентом пресета. Данные приходят снаружи, поэтому
        /// проверяется всё: и длина, и диапазон типов, и число клеток.
        /// </summary>
        public bool Unpack(byte[] data, int rosterCount)
        {
            _slots.Clear();

            if (data == null || data.Length == 0)
                return true;

            if (data.Length % BytesPerSlot != 0 || data.Length / BytesPerSlot > MaxSlots)
                return false;

            for (int offset = 0; offset < data.Length; offset += BytesPerSlot)
            {
                byte rosterIndex = data[offset];

                if (rosterIndex >= rosterCount)
                    return false;

                sbyte column = (sbyte)(data[offset + 1] - 128);
                sbyte row = (sbyte)(data[offset + 2] - 128);

                _slots.Add(new ArmyPresetSlot(rosterIndex, column, row));
            }

            return true;
        }

        /// <summary>Самая дальняя занятая шеренга. Нужна, чтобы дописать лишних юнитов позади строя.</summary>
        public int DeepestRow()
        {
            int deepest = 0;

            for (int i = 0; i < _slots.Count; i++)
                deepest = Mathf.Max(deepest, _slots[i].Row);

            return deepest;
        }
    }
}
