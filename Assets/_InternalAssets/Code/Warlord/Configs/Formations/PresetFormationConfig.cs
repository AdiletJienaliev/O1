using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs.Formations
{
    /// <summary>
    /// «Свой строй» (ГДД §7, расширение): форму задаёт не ассет, а сам игрок в панели
    /// расстановки. Ассет нужен ровно для того, чтобы пресет попал в общий набор построений
    /// и переключался той же клавишей и той же кнопкой, что линия, квадрат и клин —
    /// ни сеть, ни HUD про пресет знать не обязаны, для них это просто индекс построения.
    ///
    /// Пока игрок ничего не расставил, работает запасная сетка: пустой строй хуже
    /// любого осмысленного.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Formations/Preset", fileName = "Formation_Preset")]
    public sealed class PresetFormationConfig : FormationConfig
    {
        [Header("Сетка расстановки")]
        [Tooltip("Ширина поля расстановки в клетках. Нечётное число даёт ровный центр.")]
        [Range(3, 21)] public int gridColumns = 11;

        [Tooltip("Глубина поля расстановки в шеренгах.")]
        [Range(2, 12)] public int gridRows = 6;

        /// <summary>Запасная раскладка: обычная сетка, пока пресет не заполнен.</summary>
        public override void GenerateSlots(int unitCount, List<Vector3> destination)
        {
            destination.Clear();
            if (unitCount <= 0)
                return;

            int columns = Mathf.CeilToInt(Mathf.Sqrt(unitCount));
            int rows = ClampRows(Mathf.CeilToInt(unitCount / (float)columns));
            columns = Mathf.CeilToInt(unitCount / (float)Mathf.Max(1, rows));

            for (int index = 0; index < unitCount; index++)
            {
                int row = index / columns;
                int column = index % columns;
                int lengthOfThisRow = Mathf.Min(columns, unitCount - row * columns);

                destination.Add(new Vector3(
                    RowOffsetX(column, lengthOfThisRow, slotSpacing),
                    0f,
                    -row * slotSpacing));
            }
        }
    }
}
