using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs.Formations
{
    /// <summary>
    /// «Линия» (ГДД §7): одна шеренга, при большом отряде разбивается на две.
    /// Максимум юнитов в контакте по фронту, слабо держит прорыв в центр.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Formations/Line", fileName = "Formation_Line")]
    public sealed class LineFormationConfig : FormationConfig
    {
        [Tooltip("Начиная с этого числа юнитов линия становится двухшереножной.")]
        [Min(2)] public int splitThreshold = 12;

        public override void GenerateSlots(int unitCount, List<Vector3> destination)
        {
            destination.Clear();
            if (unitCount <= 0)
                return;

            int rows = unitCount > splitThreshold ? 2 : 1;
            rows = ClampRows(rows);
            rows = Mathf.Max(1, rows);

            int perRow = Mathf.CeilToInt(unitCount / (float)rows);

            for (int index = 0; index < unitCount; index++)
            {
                int row = index / perRow;
                int column = index % perRow;
                int lengthOfThisRow = Mathf.Min(perRow, unitCount - row * perRow);

                float x = RowOffsetX(column, lengthOfThisRow, slotSpacing);
                float z = -row * slotSpacing;
                destination.Add(new Vector3(x, 0f, z));
            }
        }
    }
}
