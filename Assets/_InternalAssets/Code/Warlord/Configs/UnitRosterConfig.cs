using System.Collections.Generic;
using UnityEngine;

namespace Warlord.Configs
{
    /// <summary>
    /// Доступный ростер (ГДД §15). Индекс в этом списке — сетевой id типа юнита:
    /// по сети ходит byte, а не строка.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Unit Roster", fileName = "UnitRosterConfig")]
    public sealed class UnitRosterConfig : ScriptableObject
    {
        [SerializeField] private List<UnitConfig> availableUnits = new();
        [SerializeField] private int defaultSelectedIndex;

        private Dictionary<UnitConfig, int> _indexByConfig;
        private Dictionary<string, int> _indexById;

        public IReadOnlyList<UnitConfig> AvailableUnits => availableUnits;
        public int Count => availableUnits.Count;
        public int DefaultSelectedIndex => Mathf.Clamp(defaultSelectedIndex, 0, Mathf.Max(0, Count - 1));

        public bool IsValidIndex(int index) => index >= 0 && index < availableUnits.Count;

        public UnitConfig Get(int index) => IsValidIndex(index) ? availableUnits[index] : null;

        public int IndexOf(UnitConfig config)
        {
            if (config == null)
                return -1;

            EnsureLookup();
            return _indexByConfig.TryGetValue(config, out int index) ? index : -1;
        }

        public int IndexOf(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                return -1;

            EnsureLookup();
            return _indexById.TryGetValue(unitId, out int index) ? index : -1;
        }

        private void EnsureLookup()
        {
            if (_indexByConfig != null && _indexByConfig.Count == availableUnits.Count)
                return;

            _indexByConfig = new Dictionary<UnitConfig, int>(availableUnits.Count);
            _indexById = new Dictionary<string, int>(availableUnits.Count);

            for (int i = 0; i < availableUnits.Count; i++)
            {
                UnitConfig unit = availableUnits[i];
                if (unit == null)
                    continue;

                _indexByConfig[unit] = i;
                if (!string.IsNullOrEmpty(unit.unitId))
                    _indexById[unit.unitId] = i;
            }
        }

        private void OnDisable() => _indexByConfig = null;
    }
}
