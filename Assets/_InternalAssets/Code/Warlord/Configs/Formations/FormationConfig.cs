using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs.Formations
{
    /// <summary>
    /// Построение (ГДД §7). Базовый класс задаёт общие данные, раскладку определяет наследник.
    /// Новое построение = новый наследник + новый ассет, менять существующий код не нужно.
    /// </summary>
    public abstract class FormationConfig : ScriptableObject
    {
        [Header("Идентификация")]
        public string formationId = "square";
        public string displayName = "Квадрат";
        public Sprite icon;

        [Header("Раскладка")]
        [Tooltip("Расстояние между соседними слотами, м.")]
        [Min(0.2f)] public float slotSpacing = 1.8f;

        [Tooltip("Ограничение числа шеренг. 0 = без ограничения.")]
        [Min(0)] public int maxRows;

        /// <summary>
        /// Заполняет список локальными оффсетами слотов относительно якоря построения.
        /// Ось +Z смотрит вперёд, слот с индексом 0 — самый передний.
        /// Реализация обязана очистить список и выдать ровно unitCount позиций.
        /// </summary>
        public abstract void GenerateSlots(int unitCount, List<Vector3> destination);

        /// <summary>Центрирует шеренгу по оси X: слоты 0..count-1 превращаются в симметричные оффсеты.</summary>
        protected static float RowOffsetX(int indexInRow, int rowLength, float spacing)
        {
            return (indexInRow - (rowLength - 1) * 0.5f) * spacing;
        }

        protected int ClampRows(int desiredRows)
        {
            return maxRows > 0 ? Mathf.Min(desiredRows, maxRows) : desiredRows;
        }
    }
}
