using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Строка гонки за флаг: игрок, его накопленное время удержания и доля от лидера.
    /// Это главный показатель матча (ГДД §2), поэтому он всегда на экране.
    /// </summary>
    public sealed class FlagRaceRowView : MonoBehaviour
    {
        [SerializeField] private Image colorTab;
        [SerializeField] private Image sigil;
        [SerializeField] private Image fill;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI timeLabel;
        [SerializeField] private GameObject holdingBadge;
        [SerializeField] private GameObject eliminatedBadge;
        [SerializeField] private CanvasGroup group;

        [Tooltip("Прозрачность строки выбывшего игрока: счёт остаётся, но перестаёт расти.")]
        [SerializeField] private float eliminatedAlpha = 0.45f;

        private int _lastSeconds = -1;

        public void Bind(int slot, Color color, Sprite sigilSprite, bool isLocal)
        {
            if (colorTab != null)
                colorTab.color = color;

            if (fill != null)
                fill.color = color;

            if (sigil != null)
            {
                sigil.sprite = sigilSprite;
                sigil.enabled = sigilSprite != null;
                sigil.color = color;
            }

            if (nameLabel == null)
                return;

            nameLabel.text = isLocal ? UiText.PlayerName(slot) + " (вы)" : UiText.PlayerName(slot);
            nameLabel.color = isLocal ? Color.white : new Color(0.82f, 0.85f, 0.9f);
        }

        public void SetState(float holdSeconds, float normalized, bool holdingFlag, bool eliminated)
        {
            if (fill != null)
                fill.fillAmount = Mathf.Clamp01(normalized);

            if (holdingBadge != null)
                holdingBadge.SetActive(holdingFlag);

            if (eliminatedBadge != null)
                eliminatedBadge.SetActive(eliminated);

            if (group != null)
                group.alpha = eliminated ? eliminatedAlpha : 1f;

            if (timeLabel == null)
                return;

            int seconds = Mathf.FloorToInt(holdSeconds);
            if (seconds == _lastSeconds)
                return;

            _lastSeconds = seconds;
            timeLabel.text = UiText.Clock(seconds);
        }

        public void Show(bool value)
        {
            if (gameObject.activeSelf != value)
                gameObject.SetActive(value);
        }
    }
}
