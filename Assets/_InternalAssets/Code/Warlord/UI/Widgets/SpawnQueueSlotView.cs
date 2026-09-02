using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;

namespace Warlord.UI.Widgets
{
    /// <summary>Один слот очереди постройки: иконка типа и полоса готовности.</summary>
    public sealed class SpawnQueueSlotView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI etaLabel;
        [SerializeField] private CanvasGroup group;

        [Tooltip("Прозрачность слотов, которые ещё не начали строиться.")]
        [SerializeField] private float pendingAlpha = 0.45f;

        public void Show(UnitConfig unit, Sprite fallbackIcon, float progress, float secondsLeft, bool inProgress)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (icon != null)
            {
                Sprite sprite = unit != null && unit.icon != null ? unit.icon : fallbackIcon;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (progressFill != null)
                progressFill.fillAmount = Mathf.Clamp01(progress);

            if (group != null)
                group.alpha = inProgress ? 1f : pendingAlpha;

            if (etaLabel == null)
                return;

            etaLabel.text = inProgress ? secondsLeft.ToString("0.0") : string.Empty;
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
