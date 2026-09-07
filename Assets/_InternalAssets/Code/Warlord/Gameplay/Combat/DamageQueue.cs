using System.Collections.Generic;
using Warlord.Domain.Combat;

namespace Warlord.Gameplay.Combat
{
    /// <summary>
    /// Буфер заявок на урон за текущий боевой такт (ГДД §12).
    /// Все атакующие — юниты, полководцы, прилетевшие снаряды — пишут сюда,
    /// а применяется всё разом в конце такта. Это и есть механика взаимной смерти.
    /// </summary>
    public sealed class DamageQueue
    {
        private readonly List<DamageEvent> _pending = new(128);

        public int Count => _pending.Count;

        public void Enqueue(ICombatTarget attacker, ICombatTarget target, int rawDamage)
        {
            if (!target.IsAliveTarget() || rawDamage <= 0)
                return;

            _pending.Add(new DamageEvent(attacker, target, rawDamage));
        }

        public IReadOnlyList<DamageEvent> Pending => _pending;

        public void Clear() => _pending.Clear();
    }
}
