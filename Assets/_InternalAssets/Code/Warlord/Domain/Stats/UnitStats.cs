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
        public readonly float TurnSpeed;
        public readonly int Armor;
        public readonly float AggroRadius;
        public readonly bool IsRanged;
        public readonly float ProjectileSpeed;

        public UnitStats(
            int maxHealth,
            int damagePerHit,
            float attackInterval,
            float attackRange,
            float moveSpeed,
            float turnSpeed,
            int armor,
            float aggroRadius,
            bool isRanged,
            float projectileSpeed)
        {
            MaxHealth = maxHealth;
            DamagePerHit = damagePerHit;
            AttackInterval = attackInterval;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            TurnSpeed = turnSpeed;
            Armor = armor;
            AggroRadius = aggroRadius;
            IsRanged = isRanged;
            ProjectileSpeed = projectileSpeed;
        }

        public float Dps => AttackInterval > 0f ? DamagePerHit / AttackInterval : 0f;
    }
}
