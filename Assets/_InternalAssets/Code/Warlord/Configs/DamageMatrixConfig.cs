using UnityEngine;

namespace Warlord.Configs
{
    /// <summary>
    /// Матрица множителей урона «атакующий тип по типу цели» (ГДД §5.3).
    /// Хранится плоским массивом, индексы совпадают с порядком в <see cref="UnitRosterConfig"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Damage Matrix", fileName = "DamageMatrixConfig")]
    public sealed class DamageMatrixConfig : ScriptableObject
    {
        [Tooltip("Выключено по умолчанию: все множители 1.0 (ГДД §5.3).")]
        [SerializeField] private bool useDamageMatrix;

        [SerializeField] private UnitRosterConfig roster;

        [Tooltip("Плоская матрица size на size: строка — атакующий, столбец — цель.")]
        [SerializeField] private float[] multipliers;

        [SerializeField] private int size;

        public bool UseDamageMatrix => useDamageMatrix;

        /// <summary>Множитель урона. Возвращает 1.0, если матрица выключена или индексы вне таблицы.</summary>
        public float GetMultiplier(int attackerTypeIndex, int targetTypeIndex)
        {
            if (!useDamageMatrix || multipliers == null || size <= 0)
                return 1f;

            if (attackerTypeIndex < 0 || attackerTypeIndex >= size)
                return 1f;
            if (targetTypeIndex < 0 || targetTypeIndex >= size)
                return 1f;

            float value = multipliers[attackerTypeIndex * size + targetTypeIndex];
            return value > 0f ? value : 1f;
        }

        /// <summary>Приводит размер матрицы к текущему ростеру, сохраняя уже выставленные значения.</summary>
        public void ResizeToRoster()
        {
            int newSize = roster != null ? roster.Count : 0;
            float[] resized = new float[newSize * newSize];

            for (int row = 0; row < newSize; row++)
            {
                for (int column = 0; column < newSize; column++)
                {
                    bool insideOld = multipliers != null && row < size && column < size;
                    resized[row * newSize + column] = insideOld ? multipliers[row * size + column] : 1f;
                }
            }

            multipliers = resized;
            size = newSize;
        }

        private void OnValidate()
        {
            int expected = roster != null ? roster.Count : 0;
            if (size != expected || multipliers == null || multipliers.Length != expected * expected)
                ResizeToRoster();
        }
    }
}
