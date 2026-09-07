using UnityEngine;

namespace Warlord.Presentation.Combat
{
    /// <summary>
    /// Выпускает видимый снаряд в момент выстрела. Живёт на стрелке и знает только,
    /// откуда стрела вылетает и какой префаб брать — куда она летит и сколько, решает сервер.
    ///
    /// Стрела намеренно не сетевой объект: восемьдесят юнитов дали бы сотни лишних
    /// NetworkObject ради того, что и так одинаково просчитывается у всех по одному числу.
    /// </summary>
    public sealed class ProjectileEmitter : MonoBehaviour
    {
        [Tooltip("Префаб снаряда. Пусто — берётся projectilePrefab из конфига юнита.")]
        [SerializeField] private GameObject projectilePrefab;

        [Tooltip("Откуда вылетает стрела: кость лука или руки. Пусто — точка по смещению ниже.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Смещение точки вылета в осях юнита, если кость не указана.")]
        [SerializeField] private Vector3 muzzleOffset = new(0f, 1.2f, 0.35f);

        private GameObject _fallbackPrefab;

        /// <summary>Префаб из конфига юнита. Используется, если на самом эмиттере ничего не задано.</summary>
        public void SetFallbackPrefab(GameObject prefab) => _fallbackPrefab = prefab;

        /// <param name="target">Тело цели или null, если её уже нет.</param>
        /// <param name="flightTime">Время полёта, посчитанное сервером.</param>
        public void Emit(Transform target, float flightTime)
        {
            GameObject prefab = projectilePrefab != null ? projectilePrefab : _fallbackPrefab;

            if (prefab == null)
                return;

            Vector3 origin = muzzle != null ? muzzle.position : transform.TransformPoint(muzzleOffset);

            // Стрела рождается в корне сцены, а не под юнитом: стрелка могут убить в полёте,
            // и вместе с ним исчезла бы стрела, которая уже почти воткнулась в цель.
            GameObject instance = Instantiate(prefab, origin, Quaternion.identity);

            if (instance.TryGetComponent(out ArrowProjectileView view))
                view.Launch(origin, target, flightTime);
            else
                Destroy(instance, 5f);
        }
    }
}
