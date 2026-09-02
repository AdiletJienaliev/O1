using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs
{
    /// <summary>Статы полководца (ГДД §8).</summary>
    [CreateAssetMenu(menuName = "Warlord/Hero", fileName = "HeroConfig")]
    public sealed class HeroConfig : ScriptableObject
    {
        [Header("Живучесть")]
        [Min(1)] public int maxHealth = 250;
        [Min(0f)] public float outOfCombatRegen = 8f;
        [Min(0f)] public float outOfCombatDelay = 6f;
        [Min(0f)] public float respawnTime = 8f;

        [Header("Движение")]
        [Min(0f)] public float moveSpeed = 6f;
        [Min(0f)] public float sprintSpeed = 8.5f;
        [Min(0f)] public float jumpHeight = 1.6f;

        [Tooltip("Множитель гравитации для более резкого прыжка.")]
        [Min(0.1f)] public float gravityScale = 3f;

        [Tooltip("Ограничение скорости падения, м/с.")]
        [Min(1f)] public float terminalVelocity = 40f;

        [Header("Поворот")]
        [Tooltip("FaceMovement — тело доворачивается в сторону шага. FaceCamera — всегда лицом по камере, A и D дают стрейф.")]
        public HeroRotationMode rotationMode = HeroRotationMode.FaceMovement;

        [Tooltip("Скорость доворота, град/с. 0 — мгновенно, тогда смена направления выглядит рывком.")]
        [Min(0f)] public float turnSpeed = 900f;

        [Header("Атака")]
        [Min(0)] public int attackDamage = 35;
        [Min(0.05f)] public float attackCooldown = 0.8f;
        [Min(0f)] public float attackRange = 2.5f;

        [Tooltip("Полуугол конуса удара, град. Сервер валидирует попадание по нему.")]
        [Range(5f, 180f)] public float attackHalfAngle = 60f;

        [Min(1)] public int maxTargetsPerHit = 1;

        [Tooltip("Запас к дальности при серверной валидации — компенсирует дрожание интерполяции.")]
        [Min(0f)] public float attackRangeTolerance = 0.5f;

        [Header("База")]
        [Tooltip("Радиус зоны покупки вокруг своей базы.")]
        [Min(1f)] public float buyZoneRadius = 12f;
    }
}
