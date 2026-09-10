using UnityEngine;
using Warlord.Configs.Bots;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Upgrades;

namespace Warlord.Domain.Bots
{
    /// <summary>
    /// Куда бот вкладывает опыт (ГДД §11). Ветки не зашиты списком приоритетов «сверху вниз»:
    /// у каждой есть ситуативная польза, которая считается из положения дел, и приоритет
    /// характера, который её взвешивает. Поэтому «лимит армии» дорожает, когда бот упёрся
    /// в потолок, а «дальность» — когда у него полстроя стрелков.
    /// </summary>
    public static class BotUpgradePlanner
    {
        /// <summary>
        /// Выбрать ветку для покупки или вернуть false, если тратить рано.
        /// Проверку доступности и цены делает вызывающий через серверную валидацию —
        /// здесь только предпочтение.
        /// </summary>
        public static bool TryChoose(
            UpgradeTreeConfig tree,
            in UpgradeLevels levels,
            int availableXp,
            BotSituation situation,
            BotPersonalityConfig personality,
            BotDifficultyConfig difficulty,
            float rangedShare,
            System.Random random,
            out UpgradeBranch branch)
        {
            branch = UpgradeBranch.Damage;

            if (tree == null || personality == null || situation == null)
                return false;

            float best = 0f;
            bool found = false;

            for (int i = 0; i < UpgradeTreeConfig.BranchCount; i++)
            {
                UpgradeBranch candidate = (UpgradeBranch)i;

                UpgradeNodeConfig node = UpgradePurchase.GetNextNode(tree, in levels, candidate);
                if (node == null)
                    continue;

                // Терпение: бот с низкой охотой к прокачке ждёт, пока опыта станет
                // заметно больше цены, и копит на следующий уровень.
                float patience = Mathf.Lerp(2f, 1f, Mathf.Clamp01(personality.upgradeEagerness));
                if (availableXp < node.xpCost * patience)
                    continue;

                float score = personality.BranchPriority(candidate) * SituationalValue(candidate, situation, rangedShare);

                // Дисциплина: чем ниже, тем чаще опыт уходит не туда, куда стоило бы.
                float noise = difficulty != null ? 1f - difficulty.upgradeSkill : 0.5f;
                if (random != null)
                    score *= 1f + ((float)random.NextDouble() * 2f - 1f) * noise;

                if (score <= best)
                    continue;

                best = score;
                branch = candidate;
                found = true;
            }

            return found;
        }

        /// <summary>Насколько ветка полезна прямо сейчас. Ноль не возвращаем: любая прокачка что-то даёт.</summary>
        private static float SituationalValue(UpgradeBranch branch, BotSituation situation, float rangedShare)
        {
            float fill = situation.ArmyFill;

            switch (branch)
            {
                case UpgradeBranch.UnitCap:
                    // Лимит бесценен ровно тогда, когда в него упёрлись, и почти бесполезен,
                    // когда армии нет и денег на неё тоже.
                    return 0.4f + Mathf.Pow(fill, 2f) * 1.6f;

                case UpgradeBranch.Damage:
                    return 0.6f + fill * 0.8f;

                case UpgradeBranch.Health:
                    return 0.6f + fill * 0.7f;

                case UpgradeBranch.AttackRange:
                    // Дальность работает только на тех, кто стреляет.
                    return 0.3f + rangedShare * 1.2f;

                case UpgradeBranch.MoveSpeed:
                    // Чем крупнее карта, тем дороже каждая секунда перехода.
                    return 0.3f + Mathf.Clamp01(situation.MapScale / 120f) * 0.9f;

                case UpgradeBranch.Hero:
                    return 0.5f;

                default:
                    return 0.5f;
            }
        }
    }
}
