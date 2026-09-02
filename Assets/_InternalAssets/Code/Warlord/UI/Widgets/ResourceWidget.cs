using TMPro;
using UnityEngine;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Кошелёк игрока: золото с текущим доходом, доступный опыт и размер армии (ГДД §4, §10).
    /// Всё это уже лежит в SyncVar на <see cref="PlayerState"/> — виджет ничего не считает сам.
    /// </summary>
    public sealed class ResourceWidget : HudWidget
    {
        [Header("Золото")]
        [SerializeField] private TextMeshProUGUI goldLabel;
        [SerializeField] private TextMeshProUGUI incomeLabel;

        [Header("Опыт")]
        [SerializeField] private TextMeshProUGUI xpLabel;

        [Header("Армия")]
        [SerializeField] private TextMeshProUGUI armyLabel;

        [Tooltip("Цвет счётчика армии, когда лимит исчерпан.")]
        [SerializeField] private Color armyFullColor = new(0.95f, 0.35f, 0.32f);
        [SerializeField] private Color armyNormalColor = Color.white;

        private int _gold = int.MinValue;
        private int _xp = int.MinValue;
        private int _army = int.MinValue;
        private int _cap = int.MinValue;
        private float _income = float.NaN;

        public override void Refresh(PlayerState player)
        {
            if (player == null)
                return;

            if (goldLabel != null && player.Gold != _gold)
            {
                _gold = player.Gold;
                goldLabel.text = UiText.Compact(_gold);
            }

            if (incomeLabel != null && !Mathf.Approximately(player.IncomePerSecond, _income))
            {
                _income = player.IncomePerSecond;
                incomeLabel.text = "+" + _income.ToString("0.#") + "/с";
            }

            if (xpLabel != null && player.Xp != _xp)
            {
                _xp = player.Xp;
                xpLabel.text = UiText.Compact(_xp);
            }

            if (armyLabel != null && (player.ArmyCount != _army || player.UnitCap != _cap))
            {
                _army = player.ArmyCount;
                _cap = player.UnitCap;
                armyLabel.text = _army + "/" + _cap;
                armyLabel.color = _cap > 0 && _army >= _cap ? armyFullColor : armyNormalColor;
            }
        }
    }
}
