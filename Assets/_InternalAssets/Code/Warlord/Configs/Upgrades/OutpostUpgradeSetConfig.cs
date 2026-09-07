using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Upgrades
{
    /// <summary>
    /// Набор улучшений аванпоста. Индекс в списке — сетевой id выбора: по сети ходит byte,
    /// а не тип и не строка, ровно как у ростера юнитов.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Outpost Upgrade Set", fileName = "OutpostUpgradeSetConfig")]
    public sealed class OutpostUpgradeSetConfig : ScriptableObject
    {
        [SerializeField] private List<OutpostUpgradeConfig> upgrades = new();

        [Tooltip("Что достаётся точке, если игрок не выбрал ничего за отведённые секунды (ГДД §2.5).")]
        [SerializeField] private int defaultIndex;

        public int Count => upgrades.Count;

        public int DefaultIndex => Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, Count - 1));

        public bool IsValidIndex(int index) => index >= 0 && index < upgrades.Count;

        public OutpostUpgradeConfig Get(int index) => IsValidIndex(index) ? upgrades[index] : null;

        public int IndexOf(OutpostUpgradeType type)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] != null && upgrades[i].type == type)
                    return i;
            }

            return -1;
        }
    }
}
