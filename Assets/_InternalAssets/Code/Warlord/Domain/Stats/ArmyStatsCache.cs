using Warlord.Configs;
using Warlord.Domain.Upgrades;

namespace Warlord.Domain.Stats
{
    /// <summary>
    /// Кэш итоговых статов на игрока: по одному <see cref="UnitStats"/> на тип из ростера.
    /// Пересчитывается только при смене уровней древа — прокачка применяется мгновенно
    /// ко всем живым юнитам (ГДД §11), поэтому юниты читают статы отсюда, а не хранят копию.
    /// </summary>
    public sealed class ArmyStatsCache
    {
        private readonly UnitRosterConfig _roster;
        private readonly UnitStatsResolver _resolver;
        private readonly UnitStats[] _stats;

        private UpgradeLevels _levels;

        public ArmyStatsCache(UnitRosterConfig roster, UnitStatsResolver resolver)
        {
            _roster = roster;
            _resolver = resolver;
            _stats = new UnitStats[roster != null ? roster.Count : 0];
            Rebuild(default);
        }

        /// <summary>Растёт при каждом пересчёте: юниты сравнивают её со своей и обновляют HP-масштаб.</summary>
        public int Version { get; private set; }

        public UpgradeLevels Levels => _levels;

        public UnitStats Get(int rosterIndex)
        {
            return rosterIndex >= 0 && rosterIndex < _stats.Length ? _stats[rosterIndex] : default;
        }

        /// <summary>Возвращает true, если уровни действительно изменились и кэш пересобран.</summary>
        public bool ApplyLevels(in UpgradeLevels levels)
        {
            if (_levels.Equals(levels) && Version > 0)
                return false;

            Rebuild(levels);
            return true;
        }

        private void Rebuild(in UpgradeLevels levels)
        {
            _levels = levels;

            for (int i = 0; i < _stats.Length; i++)
                _stats[i] = _resolver.Resolve(_roster.Get(i), in _levels);

            Version++;
        }
    }
}
