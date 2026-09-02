using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Core;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Объяснение отказов сервера (ГДД §12). Сервер уже прислал причину отдельным событием —
    /// задача виджета показать её так, чтобы игрок понял, что делать, и не переспрашивал.
    /// </summary>
    public sealed class ToastWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image background;

        [Header("Тайминги")]
        [SerializeField] private float holdDuration = 1.6f;
        [SerializeField] private float fadeDuration = 0.35f;

        [Header("Оформление")]
        [SerializeField] private Color warningColor = new(0.94f, 0.42f, 0.4f, 0.92f);

        private float _timer;
        private CommandRejection _lastReason = CommandRejection.None;

        protected override void OnInitialized()
        {
            Match.Events.CommandRejected += OnCommandRejected;

            if (group != null)
                group.alpha = 0f;
        }

        protected override void OnShutdown()
        {
            if (HasMatch)
                Match.Events.CommandRejected -= OnCommandRejected;
        }

        public override void Refresh(PlayerState player)
        {
            if (group == null || _timer <= 0f)
                return;

            _timer -= Time.deltaTime;

            group.alpha = _timer >= fadeDuration
                ? 1f
                : Mathf.Clamp01(_timer / Mathf.Max(0.01f, fadeDuration));

            if (_timer <= 0f)
                _lastReason = CommandRejection.None;
        }

        private void OnCommandRejected(int slot, CommandRejection reason)
        {
            PlayerState local = PlayerState.Local;
            if (local == null || local.Slot != slot)
                return;

            // Удержание кнопки покупки шлёт один и тот же отказ десятки раз подряд:
            // повтор только продлевает показ, а не мигает текстом.
            if (reason != _lastReason && label != null)
                label.text = UiText.Rejection(reason);

            _lastReason = reason;
            _timer = holdDuration + fadeDuration;

            if (background != null)
                background.color = warningColor;

            if (group != null)
                group.alpha = 1f;
        }
    }
}
