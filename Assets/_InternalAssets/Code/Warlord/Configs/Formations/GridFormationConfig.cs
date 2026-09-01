using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs.Formations
{
    /// <summary>
    /// «Квадрат» (ГДД §7): сетка ceil(sqrt(N)) на ceil(sqrt(N)).
    /// Хорош против окружения, плох против дальнего урона — юниты стоят кучно.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Formations/Grid", fileName = "Formation_Square")]
    public sealed class GridFormationConfig : FormationConfig
    {
        public override void GenerateSlots(int unitCount, List<Vector3> destination)
        {
            destination.Clear();
            if (unitCount <= 0)
                return;

            int columns = Mathf.CeilToInt(Mathf.Sqrt(unitCount));
            int rows = ClampRows(Mathf.CeilToInt(unitCount / (float)columns));

            // Если шеренги ограничены, добираем ширину, чтобы вместить всех.
            columns = Mathf.CeilToInt(unitCount / (float)Mathf.Max(1, rows));

            for (int index = 0; index < unitCount; index++)
            {
                int row = index / columns;
                int column = index % columns;

                // Последняя шеренга может быть неполной — центрируем её отдельно.
                int lengthOfThisRow = Mathf.Min(columns, unitCount - row * columns);

                float x = RowOffsetX(column, lengthOfThisRow, slotSpacing);
                float z = -row * slotSpacing;
                destination.Add(new Vector3(x, 0f, z));
            }
        }
    }
}
