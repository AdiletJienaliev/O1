using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Upgrades
{
    /// <summary>
    /// Древо прокачки (ГДД §11). Ноды раскладываются по веткам и уровням один раз при загрузке,
    /// чтобы рантайм-запросы «какой следующий перк в ветке» были O(1).
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Upgrades/Tree", fileName = "UpgradeTreeConfig")]
    public sealed class UpgradeTreeConfig : ScriptableObject
    {
        public const int BranchCount = 6;

        [SerializeField] private List<UpgradeNodeConfig> nodes = new();

        private UpgradeNodeConfig[][] _byBranch;

        public IReadOnlyList<UpgradeNodeConfig> Nodes => nodes;

        /// <summary>Максимальный уровень в ветке.</summary>
        public int GetMaxLevel(UpgradeBranch branch)
        {
            EnsureLookup();
            return _byBranch[(int)branch].Length;
        }

        /// <summary>Нода ветки на конкретном уровне (1..MaxLevel) или null.</summary>
        public UpgradeNodeConfig GetNode(UpgradeBranch branch, int level)
        {
            EnsureLookup();
            UpgradeNodeConfig[] branchNodes = _byBranch[(int)branch];
            int index = level - 1;
            return index >= 0 && index < branchNodes.Length ? branchNodes[index] : null;
        }

        /// <summary>Суммарный вклад ветки при данном уровне: множитель для Multiplicative, сумма для Flat.</summary>
        public float GetAccumulatedValue(UpgradeBranch branch, int level)
        {
            EnsureLookup();
            UpgradeNodeConfig[] branchNodes = _byBranch[(int)branch];
            int capped = Mathf.Clamp(level, 0, branchNodes.Length);
            if (capped <= 0)
                return branchNodes.Length > 0 && branchNodes[0].modifierType == ModifierType.Multiplicative ? 1f : 0f;

            // Уровни в ГДД заданы кумулятивно (+8% / +16% / +25%), поэтому берём значение верхнего уровня.
            UpgradeNodeConfig node = branchNodes[capped - 1];
            return node.modifierType == ModifierType.Multiplicative ? 1f + node.value : node.value;
        }

        private void EnsureLookup()
        {
            if (_byBranch != null)
                return;

            List<UpgradeNodeConfig>[] buckets = new List<UpgradeNodeConfig>[BranchCount];
            for (int i = 0; i < BranchCount; i++)
                buckets[i] = new List<UpgradeNodeConfig>();

            for (int i = 0; i < nodes.Count; i++)
            {
                UpgradeNodeConfig node = nodes[i];
                if (node == null)
                    continue;

                int branchIndex = (int)node.branch;
                if (branchIndex >= 0 && branchIndex < BranchCount)
                    buckets[branchIndex].Add(node);
            }

            _byBranch = new UpgradeNodeConfig[BranchCount][];
            for (int i = 0; i < BranchCount; i++)
            {
                buckets[i].Sort(static (a, b) => a.level.CompareTo(b.level));
                _byBranch[i] = buckets[i].ToArray();
            }
        }

        private void OnDisable() => _byBranch = null;
        private void OnValidate() => _byBranch = null;
    }
}
