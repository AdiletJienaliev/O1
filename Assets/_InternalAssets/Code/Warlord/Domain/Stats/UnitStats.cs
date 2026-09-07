namespace Warlord.Domain.Stats
{
    /// <summary>
    /// Итоговые статы юнита после применения прокачки. Иммутабельная копия:
    /// юнит держит её у себя и обновляет только когда меняется уровень древа.
    /// </summary>
    public readonly struct UnitStats
    {
        public readonly int MaxHealth;
        public readonly int DamagePerHit;
        public readonly float AttackInterval;
        public readonly float AttackRange;
        public readonly float MoveSpeed;

        /// <summary>Скорость шага. Юнит идёт ею, когда до цели уже недалеко или когда держит строй.</summary>
        public readonly float WalkSpeed;

        /// <summary>С какого расстояния до цели юнит переходит на бег, м.</summary>
        public readonly float RunDistance;

        public readonly float TurnSpeed;
        public readonly int Armor;
        public readonly float AggroRadius;
        public readonly bool IsRanged;
        public readonly float ProjectileSpeed;

        /// <summary>Радиус поражения вокруг цели. 0 — обычный удар по одной цели.</summary>
        public readonly float SplashRadius;

        /// <summary>Доля урона по задетым взрывом, кроме самой цели.</summary>
        public readonly float SplashDamageFactor;

        /// <summary>Носит ли юнит щит: только такие поднимают его по приказу «Защита».</summary>
        public readonly bool HasShield;

        /// <summary>Половина сектора блока, град, отсчитывается от взгляда юнита.</summary>
        public readonly float BlockAngle;

        /// <summary>Доля урона, проходящая сквозь поднятый щит спереди.</summary>
        public readonly float BlockDamageFactor;

        public UnitStats(
            int maxHealth,
            int damagePerHit,
            float attackInterval,
            float attackRange,
            float moveSpeed,
            float walkSpeed,
            float runDistance,
            float turnSpeed,
            int armor,
            float aggroRadius,
            bool isRanged,
            float projectileSpeed,
            float splashRadius = 0f,
            float splashDamageFactor = 0f,
            bool hasShield = false,
            float blockAngle = 0f,
            float blockDamageFactor = 0f)
        {
            MaxHealth = maxHealth;
            DamagePerHit = damagePerHit;
            AttackInterval = attackInterval;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            WalkSpeed = walkSpeed;
            RunDistance = runDistance;
            TurnSpeed = turnSpeed;
            Armor = armor;
            AggroRadius = aggroRadius;
            IsRanged = isRanged;
            ProjectileSpeed = projectileSpeed;
            SplashRadius = splashRadius;
            SplashDamageFactor = splashDamageFactor;
            HasShield = hasShield;
            BlockAngle = blockAngle;
            BlockDamageFactor = blockDamageFactor;
        }

        /// <summary>Бьёт ли этот тип по площади.</summary>
        public bool IsSplash => SplashRadius > 0.01f;

        public float Dps => AttackInterval > 0f ? DamagePerHit / AttackInterval : 0f;
    }
}
