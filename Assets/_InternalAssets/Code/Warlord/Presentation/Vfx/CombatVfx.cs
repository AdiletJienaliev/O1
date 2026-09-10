using UnityEngine;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Units;

namespace Warlord.Presentation.Vfx
{
    /// <summary>
    /// Боевые эффекты юнита или полководца. Работает целиком на клиенте: здоровье и так
    /// приезжает SyncVar-ом, поэтому падение полосы — достаточный признак попадания,
    /// и ради вспышки не нужно ни одного лишнего пакета.
    ///
    /// Урон из нескольких источников за один кадр даёт одну вспышку, а не три: в свалке
    /// на двадцать юнитов три вспышки в одной точке всё равно неразличимы, зато стоят втрое.
    /// </summary>
    public sealed class CombatVfx : MonoBehaviour
    {
        [SerializeField] private VfxLibrary library;

        [Tooltip("Источник здоровья. Пусто — ищется на объекте и родителях.")]
        [SerializeField] private MonoBehaviour healthSource;

        private IHealthSource _health;
        private UnitEntity _unit;
        private int _previousHealth;
        private bool _dead;

        private void Awake()
        {
            _health = healthSource as IHealthSource;
            _health ??= GetComponentInParent<IHealthSource>();
            _unit = GetComponentInParent<UnitEntity>();
        }

        private void OnEnable()
        {
            // Юниты берутся из пула, поэтому состояние сбрасывается при каждом включении,
            // иначе переиспользованный юнит на первом же кадре «получает» весь свой запас
            // здоровья как урон и взрывается вспышкой.
            _dead = false;
            _previousHealth = _health != null ? _health.Health : 0;

            if (_unit != null)
                _unit.AttackPerformed += OnAttack;
        }

        private void OnDisable()
        {
            if (_unit != null)
                _unit.AttackPerformed -= OnAttack;
        }

        private void LateUpdate()
        {
            if (_health == null || library == null)
                return;

            int current = _health.Health;

            if (current >= _previousHealth)
            {
                // Лечение и восстановление после респавна: просто запоминаем и живём дальше.
                _previousHealth = current;
                _dead = false;
                return;
            }

            _previousHealth = current;

            if (current > 0)
            {
                Play(library.meleeHit, transform.position + Vector3.up * library.hitHeight);
                return;
            }

            if (_dead)
                return;

            _dead = true;
            Play(library.death, transform.position + Vector3.up * (library.hitHeight * 0.6f));
        }

        private void OnAttack() => Play(library != null ? library.swing : null,
            transform.position + Vector3.up * library.hitHeight + transform.forward * 0.6f);

        private void Play(GameObject prefab, Vector3 position)
        {
            if (prefab == null || library == null)
                return;

            VfxPool.Play(prefab, position, Quaternion.identity, library.lifetime, library.maxConcurrent);
        }
    }
}
