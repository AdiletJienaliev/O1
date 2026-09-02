using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Карточка юнита в панели покупки (ГДД §5.1). Знает только про свой конфиг и цену,
    /// причину недоступности решает <see cref="UnitShopWidget"/>.
    /// </summary>
    public sealed class UnitCardView : MonoBehaviour
    {
        [Header("Содержимое")]
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private TextMeshProUGUI statsLabel;

        [Header("Состояние")]
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private GameObject lockedOverlay;

        [Header("Цвета цены")]
        [SerializeField] private Color affordableColor = new(0.99f, 0.85f, 0.35f);
        [SerializeField] private Color deniedColor = new(0.94f, 0.42f, 0.4f);

        [Header("Затемнение")]
        [SerializeField] private Color availableTint = Color.white;
        [SerializeField] private Color unavailableTint = new(0.55f, 0.55f, 0.6f);

        private int _index = -1;
        private bool _lastAvailable = true;
        private bool _lastAffordable = true;
        private int _lastCost = -1;

        public int RosterIndex => _index;

        /// <summary>Разовая привязка карточки к записи ростера.</summary>
        public void Bind(int rosterIndex, UnitConfig unit, Sprite fallbackIcon, Action<int> onClick)
        {
            _index = rosterIndex;

            if (nameLabel != null)
                nameLabel.text = UiText.UnitName(unit, rosterIndex);

            if (icon != null)
            {
                Sprite sprite = unit != null && unit.icon != null ? unit.icon : fallbackIcon;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (statsLabel != null && unit != null)
                statsLabel.text = unit.maxHealth + " HP · " + unit.damagePerHit + " урон";

            if (button == null)
                return;

            button.onClick.RemoveAllListeners();

            int captured = rosterIndex;
            button.onClick.AddListener(() => onClick?.Invoke(captured));
        }

        public void SetCost(int cost)
        {
            if (cost == _lastCost)
                return;

            _lastCost = cost;

            if (costLabel != null)
                costLabel.text = cost.ToString();
        }

        /// <summary>
        /// affordable — хватает золота, available — покупка разрешена целиком
        /// (зона базы, лимит армии, фаза матча).
        /// </summary>
        public void SetState(bool available, bool affordable)
        {
            if (available == _lastAvailable && affordable == _lastAffordable)
                return;

            _lastAvailable = available;
            _lastAffordable = affordable;

            if (button != null)
                button.interactable = available;

            if (frame != null)
                frame.color = available ? availableTint : unavailableTint;

            if (icon != null)
                icon.color = available ? availableTint : unavailableTint;

            if (costLabel != null)
                costLabel.color = affordable ? affordableColor : deniedColor;

            if (lockedOverlay != null)
                lockedOverlay.SetActive(!available);
        }
    }
}
