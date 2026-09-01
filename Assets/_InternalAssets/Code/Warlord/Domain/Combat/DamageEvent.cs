namespace Warlord.Domain.Combat
{
    /// <summary>
    /// Заявка на урон, накопленная за боевой такт. Урон не применяется сразу:
    /// два юнита, убивающие друг друга в одном такте, умирают оба (ГДД §12).
    /// </summary>
    public readonly struct DamageEvent
    {
        public readonly ICombatTarget Attacker;
        public readonly ICombatTarget Target;
        public readonly int RawDamage;

        public DamageEvent(ICombatTarget attacker, ICombatTarget target, int rawDamage)
        {
            Attacker = attacker;
            Target = target;
            RawDamage = rawDamage;
        }

        public bool IsValid => Target != null && RawDamage > 0;
    }
}
