using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Древо прокачки (ГДД §11). Покупка не требует нахождения на базе, поэтому панель
    /// доступна в любой момент матча. Список веток строится из конфига дерева.
    /// </summary>
    public sealed class UpgradeWidget : HudWidget
    {
        [Header("Ветки")]
        [SerializeField] private RectTransform container;
        [SerializeField] private UpgradeBranchView branchTemplate;

        [Tooltip("Иконки веток в порядке UpgradeBranch: урон, живучесть, дальность, лимит, скорость, полководец.")]
        [SerializeField] private Sprite[] branchIcons = new Sprite[UpgradeTreeConfig.BranchCount];

        [Header("Шапка")]
        [SerializeField] private TextMeshProUGUI xpLabel;

        private readonly List<UpgradeBranchView> _branches = new(6);
        private bool _built;

        protected override void OnInitialized() => Build();

        public override void Refresh(PlayerState player)
        {
            if (!HasMatch || player == null)
                return;

            Build();

            UpgradeTreeConfig tree = Match.Config != null ? Match.Config.UpgradeTree : null;
            if (tree == null)
                return;

            UpgradeLevels levels = player.Upgrades;

            if (xpLabel != null)
                xpLabel.text = UiText.Compact(player.Xp);

            for (int i = 0; i < _branches.Count; i++)
            {
                UpgradeBranchView view = _branches[i];
                UpgradeBranch branch = view.Branch;

                UpgradeNodeConfig next = UpgradePurchase.GetNextNode(tree, in levels, branch);
                view.SetState(levels.Get(branch), next, player.Xp);
            }
        }

        private void Build()
        {
            if (_built || !HasMatch || container == null || branchTemplate == null)
                return;

            UpgradeTreeConfig tree = Match.Config != null ? Match.Config.UpgradeTree : null;
            if (tree == null)
                return;

            branchTemplate.gameObject.SetActive(false);

            for (int i = 0; i < UpgradeTreeConfig.BranchCount; i++)
            {
                UpgradeBranch branch = (UpgradeBranch)i;
                int maxLevel = tree.GetMaxLevel(branch);

                // Пустые ветки в дерево не добавлены — не показываем их и в UI.
                if (maxLevel <= 0)
                    continue;

                UpgradeBranchView view = Instantiate(branchTemplate, container);
                view.gameObject.SetActive(true);
                view.name = "Branch_" + branch;
                view.Bind(branch, maxLevel, Icon(i), Buy);
                _branches.Add(view);
            }

            _built = true;
        }

        private Sprite Icon(int index)
        {
            return branchIcons != null && index >= 0 && index < branchIcons.Length ? branchIcons[index] : null;
        }

        private static void Buy(UpgradeBranch branch) => PlayerState.Local?.CmdBuyUpgrade((byte)branch);
    }
}
