using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warlord.UI.Widgets
{
    /// <summary>Строка списка игроков в лобби (ГДД §14): кто занял слот, его цвет и готовность.</summary>
    public sealed class LobbySlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI indexLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private Image background;
        [SerializeField] private GameObject readyBadge;
        [SerializeField] private GameObject hostBadge;
        [SerializeField] private GameObject emptyHint;

        [Header("Цвета строки")]
        [SerializeField] private Color occupiedColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color localHighlight = new(0.99f, 0.85f, 0.35f);

        public void Apply(int index, bool occupied, bool ready, bool isLocal, bool isHost, Color teamColor)
        {
            if (indexLabel != null)
                indexLabel.text = (index + 1).ToString();

            if (nameLabel != null)
            {
                nameLabel.text = occupied
                    ? (isLocal ? UiText.PlayerName(index) + " (вы)" : UiText.PlayerName(index))
                    : "Свободно";

                nameLabel.color = isLocal ? localHighlight : Color.white;
            }

            if (colorSwatch != null)
            {
                colorSwatch.color = teamColor;
                colorSwatch.enabled = occupied;
            }

            if (background != null)
                background.color = occupied ? occupiedColor : emptyColor;

            if (readyBadge != null)
                readyBadge.SetActive(occupied && ready);

            if (hostBadge != null)
                hostBadge.SetActive(occupied && isHost);

            if (emptyHint != null)
                emptyHint.SetActive(!occupied);
        }
    }
}
