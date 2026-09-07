using UnityEngine;

namespace Warlord.Presentation.Combat
{
    /// <summary>
    /// Летящая стрела. Чистая презентация: урон считает сервер в
    /// <see cref="Warlord.Gameplay.Combat.ProjectileSystem"/>, а эта стрела просто повторяет
    /// его расчёт визуально и втыкается в того, в кого попали.
    ///
    /// Время полёта приходит снаружи — то же самое, по которому сервер начисляет урон.
    /// Считать его здесь заново нельзя: клиент видит цель с задержкой, стрела прилетала бы
    /// раньше или позже удара, и попадание переставало бы читаться как попадание.
    /// </summary>
    public sealed class ArrowProjectileView : MonoBehaviour
    {
        [Header("Полёт")]
        [Tooltip("Высота навесной дуги в середине полёта, м. 0 — стрела летит по прямой.")]
        [Min(0f)] [SerializeField] private float arcHeight = 1.1f;

        [Tooltip("Куда целиться относительно точки цели: цель — это ноги, а попадать нужно в корпус.")]
        [SerializeField] private Vector3 hitOffset = new(0f, 1f, 0f);

        [Header("Попадание")]
        [Tooltip("Сколько секунд стрела торчит в цели, прежде чем исчезнуть.")]
        [Min(0f)] [SerializeField] private float stickSeconds = 8f;

        [Tooltip("Насколько глубоко стрела уходит в тело при попадании, м.")]
        [Min(0f)] [SerializeField] private float penetration = 0.2f;

        [Header("Модель")]
        [Tooltip("Доворот модели, если её остриё смотрит не по оси Z. Обычно 0 или (0, 90, 0).")]
        [SerializeField] private Vector3 modelEulerOffset;

        private Transform _target;
        private Vector3 _from;
        private Vector3 _to;
        private Vector3 _previous;

        private float _flightTime;
        private float _elapsed;
        private bool _stuck;

        /// <param name="target">Тело цели. Может стать null по дороге — цель успевают убить.</param>
        /// <param name="flightTime">Время полёта, посчитанное сервером.</param>
        public void Launch(Vector3 from, Transform target, float flightTime)
        {
            _from = from;
            _target = target;
            _flightTime = Mathf.Max(0.05f, flightTime);
            _elapsed = 0f;
            _stuck = false;

            _to = target != null ? target.position + hitOffset : from + transform.forward;
            _previous = from;

            transform.position = from;
            Aim(_to - from);
        }

        private void Update()
        {
            if (_stuck)
                return;

            _elapsed += Time.deltaTime;

            // Цель на месте не стоит: пока стрела в воздухе, точка прилёта едет за ней.
            // Иначе стрела втыкалась бы туда, где противник был в момент выстрела.
            if (_target != null)
                _to = _target.position + hitOffset;

            float t = Mathf.Clamp01(_elapsed / _flightTime);
            Vector3 position = Vector3.Lerp(_from, _to, t);

            // Навес: парабола, обнуляющаяся на обоих концах, поэтому старт и прилёт точные.
            position.y += arcHeight * 4f * t * (1f - t);

            Aim(position - _previous);
            _previous = position;
            transform.position = position;

            if (t >= 1f)
                Stick();
        }

        /// <summary>
        /// Попадание. Стрела становится дочерней цели и дальше едет вместе с ней — ради этого
        /// всё и затевалось. Если цель к этому моменту убрали, стрела просто остаётся висеть
        /// в точке прилёта и истлевает по таймеру.
        /// </summary>
        private void Stick()
        {
            _stuck = true;

            if (_target != null)
                transform.SetParent(_target, true);

            transform.position += transform.forward * penetration;

            Destroy(gameObject, stickSeconds);
        }

        private void Aim(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.000001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(modelEulerOffset);
        }
    }
}
