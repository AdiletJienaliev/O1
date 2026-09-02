using TMPro;
using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Часы матча и обратный отсчёт перед стартом (ГДД §2).
    /// Оставшееся время считает <see cref="Warlord.Gameplay.Match.MatchManager"/> локально
    /// от тика окончания, поэтому виджет просто читает значение каждый кадр.
    /// </summary>
    public sealed class MatchTimerWidget : HudWidget
    {
        [Header("Часы")]
        [SerializeField] private TextMeshProUGUI clockLabel;
        [SerializeField] private TextMeshProUGUI phaseLabel;

        [Header("Обратный отсчёт")]
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private TextMeshProUGUI countdownLabel;

        [Header("Тревожное время")]
        [Tooltip("Ниже этого порога часы перекрашиваются: последний рывок за флагом.")]
        [SerializeField] private float alarmThreshold = 60f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color alarmColor = new(0.95f, 0.35f, 0.32f);

        private MatchPhase _lastPhase = MatchPhase.None;
        private int _lastShownSeconds = -1;

        public override void Refresh(PlayerState player)
        {
            if (!HasMatch)
                return;

            MatchPhase phase = Match.Phase;

            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _lastShownSeconds = -1;

                if (phaseLabel != null)
                    phaseLabel.text = UiText.Phase(phase);

                if (countdownRoot != null)
                    countdownRoot.SetActive(phase == MatchPhase.Countdown);
            }

            if (phase == MatchPhase.Countdown)
                RefreshCountdown();

            RefreshClock(phase);
        }

        private void RefreshCountdown()
        {
            if (countdownLabel == null)
                return;

            float remaining = Match.CountdownRemaining;

            // На чистом клиенте отсчёт не синкается — там показываем нейтральную надпись,
            // чтобы не выводить фальшивый ноль.
            countdownLabel.text = remaining > 0.05f
                ? Mathf.CeilToInt(remaining).ToString()
                : "К бою!";
        }

        private void RefreshClock(MatchPhase phase)
        {
            if (clockLabel == null)
                return;

            float remaining = phase == MatchPhase.Finished ? 0f : Match.RemainingSeconds;
            int seconds = Mathf.CeilToInt(remaining);

            // Текст обновляем только при смене секунды: TMP пересобирает меш на каждом set.
            if (seconds == _lastShownSeconds)
                return;

            _lastShownSeconds = seconds;
            clockLabel.text = UiText.Clock(remaining);
            clockLabel.color = remaining <= alarmThreshold && phase == MatchPhase.Running ? alarmColor : normalColor;
        }
    }
}
