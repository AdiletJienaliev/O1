using UnityEngine;
using Warlord.Configs;

namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Единственное место, где сырой урон превращается в итоговый.
    /// Порядок: множитель матрицы типов, затем плоская броня, затем минимум в 1.
    /// </summary>
    public sealed class DamageCalculator
    {
        private readonly DamageMatrixConfig _matrix;

        public DamageCalculator(DamageMatrixConfig matrix) => _matrix = matrix;

        public int Resolve(in DamageEvent damageEvent)
        {
            if (!damageEvent.IsValid)
                return 0;

            float multiplier = 1f;
            if (_matrix != null && damageEvent.Attacker != null)
            {
                multiplier = _matrix.GetMultiplier(
                    damageEvent.Attacker.UnitTypeIndex,
                    damageEvent.Target.UnitTypeIndex);
            }

            int afterMatrix = Mathf.RoundToInt(damageEvent.RawDamage * multiplier);
            int afterArmor = afterMatrix - damageEvent.Target.Armor;

            // Броня никогда не обнуляет урон полностью, иначе тяжёлые юниты становятся бессмертными.
            return Mathf.Max(1, afterArmor);
        }
    }
}
