using UnityEngine;
using Warlord.Configs;

namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Единственное место, где сырой урон превращается в итоговый.
    /// Порядок: множитель матрицы типов, затем щит, затем плоская броня, затем минимум в 1.
    ///
    /// Щит стоит до брони и до нижнего порога намеренно: броня по правилу никогда не обнуляет
    /// урон, а поднятый щит спереди — обязан, иначе приказ «Защита» не давал бы ничего,
    /// кроме потерянного темпа.
    /// </summary>
    public sealed class DamageCalculator
    {
        private readonly DamageMatrixConfig _matrix;

        public DamageCalculator(DamageMatrixConfig matrix) => _matrix = matrix;

        public int Resolve(in DamageEvent damageEvent) => Resolve(in damageEvent, out _);

        /// <param name="blocked">Удар попал в поднятый щит. Урон при этом мог и не обнулиться.</param>
        public int Resolve(in DamageEvent damageEvent, out bool blocked)
        {
            blocked = false;

            if (!damageEvent.IsValid)
                return 0;

            // Атакующий мог быть уничтожен между постановкой заявки и её разрешением
            // (умер и был деспавнен, игрок вышел). Урон при этом не пропадает, но считается
            // без его типа и без сектора щита.
            ICombatTarget attacker = damageEvent.LivingAttacker;

            float multiplier = 1f;
            if (_matrix != null && attacker != null)
            {
                multiplier = _matrix.GetMultiplier(
                    attacker.UnitTypeIndex,
                    damageEvent.Target.UnitTypeIndex);
            }

            int afterMatrix = Mathf.RoundToInt(damageEvent.RawDamage * multiplier);

            // Сектор считаем от того места, где атакующий стоит в момент разрешения такта.
            // Для стрелы это точка прилёта, а не выстрела: щит держат против того, кто перед тобой
            // сейчас, и обойти строй за время полёта — законный способ пробить блок.
            if (attacker != null)
            {
                float block = ShieldBlock.ResolveMultiplier(damageEvent.Target, attacker.Position);

                blocked = block < 1f;

                if (block <= 0f)
                    return 0;

                afterMatrix = Mathf.RoundToInt(afterMatrix * block);
            }

            int afterArmor = afterMatrix - damageEvent.Target.Armor;

            // Броня никогда не обнуляет урон полностью, иначе тяжёлые юниты становятся бессмертными.
            return Mathf.Max(1, afterArmor);
        }
    }
}
