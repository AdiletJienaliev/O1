using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs.Upgrades;
using Warlord.Core;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Одна ветка древа прокачки (ГДД §11): уровни линейные, поэтому показываем
    /// цепочку делений и стоимость ровно следующего перка.
    /// </summary>
    public sealed class UpgradeBranchView : MonoBehaviour
    {
        [Header("Заголовок")]
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;

        [Header("Уровни")]
        [SerializeField] private RectTransform pipContainer;
        [SerializeField] private Image pipTemplate;
        [SerializeField] private Color pipOnColor = new(0.99f, 0.85f, 0.35f);
        [SerializeField] private Color pipOffColor = new(1f, 1f, 1f, 0.22f);

        [Header("Покупка")]
        [SerializeField] private Button buyButton;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private GameObject maxedBadge;

        [Header("Цвета цены")]
        [SerializeField] private Color affordableColor = new(0.62f, 0.88f, 0.6f);
        [SerializeField] private Color deniedColor = new(0.94f, 0.42f, 0.4f);

        private readonly List<Image> _pips = new(8);

        private UpgradeBranch _branch;
        private int _lastLevel = -1;
        private int _lastCost = int.MinValue;
        private bool _lastAffordable;

        public UpgradeBranch Branch => _branch;

        public void Bind(UpgradeBranch branch, int maxLevel, Sprite sprite, Action<UpgradeBranch> onBuy)
        {
            _branch = branch;

            if (nameLabel != null)
                nameLabel.text = UiText.Branch(branch);

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            BuildPips(maxLevel);

            if (buyButton == null)
                return;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy?.Invoke(branch));
        }

        /// <summary>node == null означает, что ветка выкачана до конца.</summary>
        public void SetState(int level, UpgradeNodeConfig node, int availableXp)
        {
            if (level != _lastLevel)
            {
                _lastLevel = level;
                ApplyPips(level);
            }

            bool maxed = node == null;
            int cost = maxed ? 0 : node.xpCost;
            bool affordable = !maxed && availableXp >= cost;

            if (maxedBadge != null)
                maxedBadge.SetActive(maxed);

            if (buyButton != null)
                buyButton.interactable = !maxed && affordable;

            if (descriptionLabel != null)
                descriptionLabel.text = maxed ? "Максимальный уровень" : Describe(node);

            if (costLabel == null)
                return;

            if (cost == _lastCost && affordable == _lastAffordable)
                return;

            _lastCost = cost;
            _lastAffordable = affordable;

            costLabel.text = maxed ? "—" : cost.ToString();
            costLabel.color = maxed || affordable ? affordableColor : deniedColor;
        }

        private static string Describe(UpgradeNodeConfig node)
        {
            if (!string.IsNullOrEmpty(node.description))
                return node.description;

            // Ассеты без описания всё равно должны читаться: собираем текст из самих чисел.
            return node.modifierType == ModifierType.Multiplicative
                ? "+" + Mathf.RoundToInt(node.value * 100f) + "%"
                : "+" + node.value.ToString("0.#");
        }

        private void BuildPips(int maxLevel)
        {
            if (pipContainer == null || pipTemplate == null)
                return;

            pipTemplate.gameObject.SetActive(false);

            for (int i = 0; i < maxLevel; i++)
            {
                Image pip = Instantiate(pipTemplate, pipContainer);
                pip.gameObject.SetActive(true);
                pip.name = "Pip_" + i;
                pip.color = pipOffColor;
                _pips.Add(pip);
            }
        }

        private void ApplyPips(int level)
        {
            for (int i = 0; i < _pips.Count; i++)
                _pips[i].color = i < level ? pipOnColor : pipOffColor;
        }
    }
}
