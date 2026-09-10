using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Stats;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Что бот думает об одном типе юнита. Все числа выведены из статов, а не заданы вручную:
    /// в описании роли нет ни одного идентификатора юнита, и добавленный завтра тип
    /// получает свою роль сам, из своих же цифр.
    /// </summary>
    public readonly struct BotUnitRole
    {
        public readonly int RosterIndex;
        public readonly bool IsGarrison;
        public readonly bool IsRanged;
        public readonly int Cost;

        /// <summary>Урон в секунду с учётом прокачки владельца.</summary>
        public readonly float Dps;

        /// <summary>Здоровье с поправкой на броню: броня снимает урон с каждого удара, а не разово.</summary>
        public readonly float EffectiveHealth;

        /// <summary>Боевая сила одного тела. Корень из произведения — чтобы «толстый ноль» не выглядел бойцом.</summary>
        public readonly float Power;

        /// <summary>Сила за единицу золота. Основной критерий покупки при прочих равных.</summary>
        public readonly float Value;

        /// <summary>Насколько юнит «мясо»: живучесть выше средней при уроне ниже среднего.</summary>
        public readonly float TankScore;

        /// <summary>Насколько юнит бьёт по площади. 0 — одиночная цель.</summary>
        public readonly float SplashScore;

        /// <summary>Дальность удара, м. По ней бот отличает первую шеренгу от задней.</summary>
        public readonly float Reach;

        public readonly float MoveSpeed;

        public BotUnitRole(int rosterIndex, UnitConfig config, in UnitStats stats, int cost)
        {
            RosterIndex = rosterIndex;
            IsGarrison = config != null && config.isGarrison;
            IsRanged = stats.IsRanged;
            Cost = Mathf.Max(1, cost);

            Dps = Mathf.Max(0.01f, stats.Dps);

            // Броня работает против каждого удара, поэтому её вклад зависит от типичного
            // урона по карте. Десятка — грубая, но устойчивая точка отсчёта: точнее посчитать
            // нельзя, не зная, кто именно будет бить, а порядок величины она держит верно.
            EffectiveHealth = Mathf.Max(1f, stats.MaxHealth * (1f + stats.Armor / 10f));

            Power = Mathf.Sqrt(Dps * EffectiveHealth);
            Value = Power / Cost;

            // «Мясо» — это много живучести на единицу своего же урона. Двадцатка в делителе
            // подобрана так, чтобы обычный боец давал около половины шкалы, а щитоносец —
            // заметно больше: важна не абсолютная цифра, а порядок между типами.
            TankScore = Mathf.Clamp01(EffectiveHealth / (Dps * 20f));
            SplashScore = stats.IsSplash ? Mathf.Clamp01(stats.SplashRadius / 5f) : 0f;

            Reach = Mathf.Max(0.5f, stats.AttackRange);
            MoveSpeed = Mathf.Max(0.1f, stats.MoveSpeed);
        }
    }

    /// <summary>
    /// Взгляд бота на ростер: роли всех типов, пересчитанные под его собственную прокачку.
    /// Это и есть главная защита от «переписать ботов при добавлении юнита»: бот нигде
    /// не спрашивает «а это мечник?», он спрашивает «сколько силы за золото и кого он бьёт
    /// лучше всех», и ответ приходит из тех же данных, что и весь остальной баланс.
    ///
    /// Пересобирается только при смене версии кэша статов — то есть при покупке перка.
    /// </summary>
    public sealed class BotUnitCatalog
    {
        private readonly UnitRosterConfig _roster;
        private readonly DamageMatrixConfig _matrix;
        private BotUnitRole[] _roles;
        private int _statsVersion = -1;

        public BotUnitCatalog(UnitRosterConfig roster, DamageMatrixConfig matrix)
        {
            _roster = roster;
            _matrix = matrix;
            _roles = new BotUnitRole[roster != null ? roster.Count : 0];
        }

        public int Count => _roles.Length;

        public BotUnitRole Get(int rosterIndex)
        {
            return rosterIndex >= 0 && rosterIndex < _roles.Length ? _roles[rosterIndex] : default;
        }

        /// <summary>Средняя сила тела в ростере. Ей нормируются оценки армий.</summary>
        public float AveragePower { get; private set; } = 1f;

        /// <summary>
        /// Пересобрать роли под текущую прокачку. <paramref name="resolveCost"/> приходит
        /// снаружи, потому что цена зависит от настроек комнаты и улучшений точки, а не от юнита.
        /// </summary>
        public void Refresh(ArmyStatsCache stats, System.Func<UnitConfig, int> resolveCost)
        {
            if (stats == null || _roster == null)
                return;

            if (_statsVersion == stats.Version)
                return;

            _statsVersion = stats.Version;

            if (_roles.Length != _roster.Count)
                _roles = new BotUnitRole[_roster.Count];

            // Первый проход — средняя сила: она нужна самим ролям как точка отсчёта.
            float total = 0f;
            int counted = 0;

            for (int i = 0; i < _roster.Count; i++)
            {
                UnitStats unit = stats.Get(i);
                if (unit.MaxHealth <= 0)
                    continue;

                total += Mathf.Sqrt(Mathf.Max(0.01f, unit.Dps) * Mathf.Max(1f, unit.MaxHealth));
                counted++;
            }

            AveragePower = counted > 0 ? total / counted : 1f;

            for (int i = 0; i < _roster.Count; i++)
            {
                UnitConfig config = _roster.Get(i);
                UnitStats unit = stats.Get(i);
                int cost = resolveCost != null ? resolveCost(config) : (config != null ? config.cost : 1);

                _roles[i] = new BotUnitRole(i, config, in unit, cost);
            }
        }

        /// <summary>
        /// Насколько тип <paramref name="attacker"/> хорош против типа <paramref name="target"/>.
        /// Если матрица типов выключена (ГДД §5.3), остаётся честная физика: свой урон
        /// по чужой живучести и своя живучесть под чужим уроном. Работает при любом ростере.
        /// </summary>
        public float Matchup(int attacker, int target)
        {
            BotUnitRole a = Get(attacker);
            BotUnitRole b = Get(target);

            if (a.Power <= 0f || b.Power <= 0f)
                return 1f;

            float multiplier = _matrix != null ? _matrix.GetMultiplier(attacker, target) : 1f;

            float damageRatio = a.Dps * multiplier / b.EffectiveHealth;
            float survivalRatio = a.EffectiveHealth / Mathf.Max(0.01f, b.Dps);

            // Дальник получает поправку вверх: он успевает отработать по подходящему.
            float reachBonus = a.IsRanged && !b.IsRanged ? 1.15f : 1f;

            return Mathf.Sqrt(Mathf.Max(0.01f, damageRatio * survivalRatio)) * reachBonus;
        }
    }
}
