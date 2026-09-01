using Warlord.Configs.Upgrades;
using Warlord.Core;

namespace Warlord.Domain.Upgrades
{
    /// <summary>
    /// Правила покупки перка (ГДД §11): уровни линейные, отката нет, покупка не требует базы.
    /// Чистая логика без сети — сервер вызывает её же, что и клиент для предпросмотра кнопок.
    /// </summary>
    public static class UpgradePurchase
    {
        /// <summary>Следующий доступный перк ветки. Null, если ветка выкачана до конца.</summary>
        public static UpgradeNodeConfig GetNextNode(UpgradeTreeConfig tree, in UpgradeLevels levels, UpgradeBranch branch)
        {
            if (tree == null)
                return null;

            int nextLevel = levels.Get(branch) + 1;
            return tree.GetNode(branch, nextLevel);
        }

        /// <summary>
        /// Полная проверка покупки. Возвращает None, если покупка возможна,
        /// и заполняет node тем перком, который будет куплен.
        /// </summary>
        public static CommandRejection Validate(
            UpgradeTreeConfig tree,
            in UpgradeLevels levels,
            int availableXp,
            UpgradeBranch branch,
            out UpgradeNodeConfig node)
        {
            node = GetNextNode(tree, in levels, branch);

            if (node == null)
                return CommandRejection.UpgradeUnavailable;

            if (availableXp < node.xpCost)
                return CommandRejection.NotEnoughXp;

            return CommandRejection.None;
        }

        /// <summary>Суммарная стоимость всего древа — для баланс-проверок из ГДД §4.</summary>
        public static int GetFullTreeCost(UpgradeTreeConfig tree)
        {
            if (tree == null)
                return 0;

            int total = 0;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                UpgradeNodeConfig node = tree.Nodes[i];
                if (node != null)
                    total += node.xpCost;
            }

            return total;
        }
    }
}
