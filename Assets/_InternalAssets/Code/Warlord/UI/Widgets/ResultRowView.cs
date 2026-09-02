using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Строка итоговой таблицы. Показывает то, что реально доступно клиенту:
    /// время удержания флага — главный критерий победы, остальное как контекст (ГДД §2).
    /// </summary>
    public sealed class ResultRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI flagTimeLabel;
        [SerializeField] private TextMeshProUGUI basesLabel;
        [SerializeField] private TextMeshProUGUI armyLabel;
        [SerializeField] private Image colorTab;
        [SerializeField] private Image background;
        [SerializeField] private GameObject winnerBadge;

        [Header("Подсветка")]
        [SerializeField] private Color winnerBackground = new(0.99f, 0.85f, 0.35f, 0.22f);
        [SerializeField] private Color normalBackground = new(1f, 1f, 1f, 0.06f);

        public void Apply(int rank, int slot, Color color, float flagSeconds, int bases, int army, bool winner)
        {
            if (rankLabel != null)
                rankLabel.text = rank.ToString();

            if (nameLabel != null)
                nameLabel.text = UiText.PlayerName(slot);

            if (flagTimeLabel != null)
                flagTimeLabel.text = UiText.Clock(flagSeconds);

            if (basesLabel != null)
                basesLabel.text = bases.ToString();

            if (armyLabel != null)
                armyLabel.text = army.ToString();

            if (colorTab != null)
                colorTab.color = color;

            if (background != null)
                background.color = winner ? winnerBackground : normalBackground;

            if (winnerBadge != null)
                winnerBadge.SetActive(winner);
        }

        public void Show(bool value)
        {
            if (gameObject.activeSelf != value)
                gameObject.SetActive(value);
        }
    }
}
