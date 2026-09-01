using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs.Formations
{
    /// <summary>
    /// «Треугольник» (ГДД §7): клин 1-2-3-4 по шеренгам.
    /// Хорош для прорыва линии, уязвим с флангов.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Formations/Wedge", fileName = "Formation_Wedge")]
    public sealed class WedgeFormationConfig : FormationConfig
    {
        public override void GenerateSlots(int unitCount, List<Vector3> destination)
        {
            destination.Clear();
            if (unitCount <= 0)
                return;

            int placed = 0;
            int row = 0;

            while (placed < unitCount)
            {
                int rowLength = row + 1;

                // При ограничении шеренг последняя расширяется на весь остаток.
                bool isLastAllowedRow = maxRows > 0 && row == maxRows - 1;
                if (isLastAllowedRow)
                    rowLength = unitCount - placed;

                rowLength = Mathf.Min(rowLength, unitCount - placed);

                for (int i = 0; i < rowLength; i++)
                {
                    float x = RowOffsetX(i, rowLength, slotSpacing);
                    float z = -row * slotSpacing;
                    destination.Add(new Vector3(x, 0f, z));
                }

                placed += rowLength;
                row++;
            }
        }
    }
}
