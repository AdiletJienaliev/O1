using UnityEngine;

namespace Warlord.Configs
{
    /// <summary>
    /// Описание типа юнита (ГДД §5). Это чистые данные — никакой рантайм-математики здесь нет,
    /// итоговые статы всегда считает <see cref="Warlord.Domain.Stats.UnitStatsResolver"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Unit", fileName = "UnitConfig")]
    public sealed class UnitConfig : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Стабильный строковый id. Используется в матрице урона и в настройках комнаты.")]
        public string unitId = "swordsman";

        public string displayName = "Мечник";
        public Sprite icon;

        [Tooltip("Префаб с NetworkObject + UnitEntity. Обязан лежать в DefaultPrefabObjects.")]
        public GameObject prefab;

        [Header("Покупка")]
        [Min(0)] public int cost = 60;
        [Min(0.1f)] public float spawnTime = 2f;

        [Header("Бой")]
        [Min(1)] public int maxHealth = 100;
        [Min(0)] public int damagePerHit = 10;
        [Min(0.05f)] public float attackInterval = 0.5f;
        [Min(0.1f)] public float attackRange = 2f;

        [Tooltip("Плоское снижение входящего урона.")]
        [Min(0)] public int armor;

        [Header("Движение")]
        [Min(0.1f)] public float moveSpeed = 4.5f;
        [Min(1f)] public float turnSpeed = 360f;

        [Header("Поведение")]
        [Tooltip("Персональный радиус агро. 0 = взять глобальный из CommandConfig.")]
        [Min(0f)] public float aggroRadiusOverride;

        [Tooltip("Кто встаёт вперёд в построении: меньше — ближе к первой шеренге.")]
        [Min(0)] public int formationSlotPriority;

        [Header("Дальний бой")]
        public bool isRanged;
        public GameObject projectilePrefab;
        [Min(1f)] public float projectileSpeed = 25f;

        /// <summary>Расчётный DPS без прокачки — только для инспектора и баланс-таблиц.</summary>
        public float EstimatedDps => attackInterval > 0f ? damagePerHit / attackInterval : 0f;
    }
}
