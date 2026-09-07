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

        /// <summary>Цель ещё существует (объект не уничтожен) и урон осмысленный.</summary>
        public bool IsValid => Target.Exists() && RawDamage > 0;

        /// <summary>Атакующий, если он ещё существует. Заявка без него остаётся в силе — урон анонимный.</summary>
        public ICombatTarget LivingAttacker => Attacker.OrNull();
    }
}
