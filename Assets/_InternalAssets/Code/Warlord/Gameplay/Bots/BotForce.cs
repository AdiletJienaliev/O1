using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Units;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Перевод живых боевых сущностей в одно число — «сколько тут силы». Одно место на весь
    /// код ботов: и своя армия, и чужая, и гарнизон точки считаются одинаково, иначе бот
    /// сравнивал бы килограммы с метрами и лез в заведомо проигранный бой.
    ///
    /// Формула та же, что в <see cref="Warlord.Domain.Bots.BotUnitRole"/>: корень из
    /// произведения урона в секунду на живучесть. Она не претендует на точность —
    /// она обязана лишь верно упорядочивать «сильнее / слабее».
    /// </summary>
    public static class BotForce
    {
        /// <summary>Сила одного юнита с учётом его текущего здоровья: раненый строй слабее целого.</summary>
        public static float PowerOf(UnitEntity unit)
        {
            if (unit == null || !unit.IsAlive)
                return 0f;

            Domain.Stats.UnitStats stats = unit.Stats;

            float dps = Mathf.Max(0.01f, stats.Dps);
            float health = Mathf.Max(1f, unit.Health * (1f + stats.Armor / 10f));

            return Mathf.Sqrt(dps * health);
        }

        /// <summary>
        /// Сила полководца. Считается по его конфигу, а не по «на глаз»: полководец
        /// живучее и бьёт больнее любого юнита, и недооценка его в оценке боя —
        /// самая дорогая ошибка, которую бот может сделать (ГДД §8).
        /// </summary>
        public static float PowerOf(HeroController hero, HeroConfig config)
        {
            if (hero == null || !hero.IsAlive || config == null)
                return 0f;

            float dps = config.attackCooldown > 0.01f ? config.attackDamage / config.attackCooldown : config.attackDamage;
            float health = Mathf.Max(1f, hero.Health);

            return Mathf.Sqrt(Mathf.Max(0.01f, dps) * health);
        }

        /// <summary>Сила произвольной боевой цели. Полководца отличаем по типу, а не по слоту.</summary>
        public static float PowerOf(ICombatTarget target, HeroConfig heroConfig)
        {
            switch (target)
            {
                case null: return 0f;
                case UnitEntity unit: return PowerOf(unit);
                case HeroController hero: return PowerOf(hero, heroConfig);
                default: return 0f;
            }
        }
    }
}
