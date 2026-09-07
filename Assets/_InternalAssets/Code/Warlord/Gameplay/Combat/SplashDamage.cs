using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Combat;

namespace Warlord.Gameplay.Combat
{
    /// <summary>
    /// Раздача урона по площади (маг, ГДД §5.2). Одно место на весь проект: удар в упор
    /// и прилетевший снаряд обязаны накрывать одинаково, иначе баланс мага зависел бы
    /// от того, каким путём урон попал в очередь.
    ///
    /// Буфер общий и статический — боевой такт однопоточный, как и весь серверный цикл,
    /// а аллокация списка на каждый взрыв при 80 юнитах стоит дороже, чем этот компромисс.
    /// </summary>
    public static class SplashDamage
    {
        private static readonly List<ICombatTarget> Buffer = new(32);

        /// <summary>
        /// Заявка на урон по цели и всем врагам вокруг неё. При нулевом радиусе
        /// вырождается в обычный одиночный удар, поэтому вызывать можно всегда.
        /// </summary>
        public static void Apply(
            DamageQueue damage,
            TargetingService targeting,
            ICombatTarget attacker,
            ICombatTarget primary,
            int rawDamage,
            float splashRadius,
            float splashFactor)
        {
            if (damage == null || !primary.IsAliveTarget() || rawDamage <= 0)
                return;

            attacker = attacker.OrNull();

            damage.Enqueue(attacker, primary, rawDamage);

            if (targeting == null || splashRadius <= 0.01f || splashFactor <= 0f)
                return;

            // Урон по задетым всегда хотя бы 1: иначе маг с малым множителем
            // просто ничего не делал бы по площади из-за округления.
            int splashDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * Mathf.Clamp01(splashFactor)));
            int attackerSlot = attacker != null ? attacker.OwnerSlot : PlayerSlots.None;

            targeting.CollectInRadius(primary.Position, splashRadius, null, Buffer);

            for (int i = 0; i < Buffer.Count; i++)
            {
                ICombatTarget candidate = Buffer[i];

                if (!candidate.Exists() || ReferenceEquals(candidate, primary))
                    continue;

                // Своих взрывом не задевает: дружественный огонь в ГДД не предусмотрен,
                // а без этой проверки маг выкашивал бы собственный строй.
                if (!PlayerSlots.AreEnemies(candidate.OwnerSlot, attackerSlot))
                    continue;

                damage.Enqueue(attacker, candidate, splashDamage);
            }

            Buffer.Clear();
        }
    }
}
