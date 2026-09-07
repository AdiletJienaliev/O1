using UnityEngine;

namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Проверки живучести ссылки на цель.
    ///
    /// <see cref="ICombatTarget"/> реализуют компоненты Unity, а ссылки на цели живут дольше
    /// одного такта: они лежат в очереди урона, в снарядах в полёте, в памяти об обидчике.
    /// Юнит за это время может быть уничтожен — и обычная проверка на null через интерфейс
    /// её не увидит, потому что перегруженный оператор Unity работает только для статического
    /// типа Object. Отсюда падения вида MissingReferenceException при обращении к Position.
    /// Поэтому любую сохранённую ссылку проверяем только через эти методы.
    /// </summary>
    public static class CombatTargetExtensions
    {
        /// <summary>Ссылка есть и объект за ней не уничтожен.</summary>
        public static bool Exists(this ICombatTarget target)
        {
            if (target == null)
                return false;

            // Приведение к Object обязательно: только так вызовется перегрузка Unity,
            // отличающая уничтоженный объект от живого.
            if (target is Object unityObject)
                return unityObject != null;

            return true;
        }

        /// <summary>Объект не уничтожен и цель ещё жива. Единственная корректная проверка перед ударом.</summary>
        public static bool IsAliveTarget(this ICombatTarget target) => target.Exists() && target.IsAlive;

        /// <summary>Ссылка, очищенная от уничтоженных объектов. Удобно для необязательного атакующего.</summary>
        public static ICombatTarget OrNull(this ICombatTarget target) => target.Exists() ? target : null;
    }
}
