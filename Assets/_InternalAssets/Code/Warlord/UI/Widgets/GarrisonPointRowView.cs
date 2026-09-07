using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs.Upgrades;
using Warlord.Gameplay.Capture;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Строка одной точки в панели гарнизона (ГДД §1.9, §2.5): счётчик охранников,
    /// кнопка покупки и выбор улучшения для аванпоста.
    ///
    /// Строка ничего не решает сама — доступность и цифры ей приносит
    /// <see cref="GarrisonShopWidget"/>, а сервер всё равно проверяет команду заново.
    /// </summary>
    public sealed class GarrisonPointRowView : MonoBehaviour
    {
        [Header("Содержимое")]
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI guardLabel;
        [SerializeField] private TextMeshProUGUI costLabel;

        [Header("Покупка")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Image frame;

        [Header("Улучшения")]
        [SerializeField] private GameObject upgradeGroup;
        [SerializeField] private Button[] upgradeButtons = Array.Empty<Button>();
        [SerializeField] private Image[] upgradeFrames = Array.Empty<Image>();
        [SerializeField] private TextMeshProUGUI[] upgradeLabels = Array.Empty<TextMeshProUGUI>();

        [Header("Цвета")]
        [SerializeField] private Color availableTint = Color.white;
        [SerializeField] private Color unavailableTint = new(0.55f, 0.55f, 0.6f);
        [SerializeField] private Color chosenTint = new(0.55f, 0.87f, 0.55f);
        [SerializeField] private Color affordableColor = new(0.99f, 0.85f, 0.35f);
        [SerializeField] private Color deniedColor = new(0.94f, 0.42f, 0.4f);

        private CapturePointBehaviour _point;

        public CapturePointBehaviour Point => _point;

        /// <summary>Разовая привязка строки к точке. Названия улучшений приходят из набора.</summary>
        public void Bind(
            CapturePointBehaviour point,
            OutpostUpgradeSetConfig upgrades,
            Action<CapturePointBehaviour> onBuy,
            Action<CapturePointBehaviour, int> onUpgrade)
        {
            _point = point;

            if (nameLabel != null)
                nameLabel.text = point != null && point.Config != null ? point.Config.displayName : "Точка";

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => onBuy?.Invoke(point));
            }

            bool hasUpgrades = point != null && point.HasUpgradeSlot && upgrades != null && upgrades.Count > 0;

            if (upgradeGroup != null)
                upgradeGroup.SetActive(hasUpgrades);

            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                Button button = upgradeButtons[i];
                if (button == null)
                    continue;

                bool exists = hasUpgrades && upgrades.IsValidIndex(i);
                button.gameObject.SetActive(exists);

                if (!exists)
                    continue;

                if (i < upgradeLabels.Length && upgradeLabels[i] != null)
                    upgradeLabels[i].text = upgrades.Get(i).displayName;

                int captured = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onUpgrade?.Invoke(point, captured));
            }
        }

        /// <param name="guards">Сколько охранников стоит на точке уже.</param>
        /// <param name="limit">Сколько всего помещается: меньшее из лимита точки и лимита типа.</param>
        public void Refresh(int guards, int limit, int cost, bool canBuy, bool affordable, int chosenUpgrade)
        {
            if (guardLabel != null)
                guardLabel.text = guards + " / " + limit;

            if (costLabel != null)
            {
                costLabel.text = cost.ToString();
                costLabel.color = affordable ? affordableColor : deniedColor;
            }

            if (buyButton != null)
                buyButton.interactable = canBuy;

            if (frame != null)
                frame.color = canBuy ? availableTint : unavailableTint;

            for (int i = 0; i < upgradeFrames.Length; i++)
            {
                if (upgradeFrames[i] != null)
                    upgradeFrames[i].color = i == chosenUpgrade ? chosenTint : availableTint;
            }
        }
    }
}
