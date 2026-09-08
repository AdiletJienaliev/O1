using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Networking;
using Warlord.UI.Widgets;

namespace Warlord.UI.Screens
{
    /// <summary>
    /// Итоги матча (ГДД §2, §10.4). Победителя определяет сервер и присылает готовым —
    /// экран лишь раскладывает таблицу и объясняет причину завершения.
    /// </summary>
    public sealed class ResultScreen : UiScreen
    {
        [Header("Заголовок")]
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI reasonLabel;
        [SerializeField] private Image accentBar;

        [Header("Таблица")]
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private ResultRowView rowTemplate;

        [Header("Кнопки")]
        [SerializeField] private Button leaveButton;

        [Header("Цвета заголовка")]
        [SerializeField] private Color victoryColor = new(0.99f, 0.85f, 0.35f);
        [SerializeField] private Color defeatColor = new(0.72f, 0.75f, 0.82f);

        private readonly List<ResultRowView> _rows = new(4);
        private readonly List<PlayerState> _players = new(4);

        private MatchManager _match;
        private NetworkBootstrap _bootstrap;
        private MatchOutcome _outcome = MatchOutcome.None;

        protected override void Awake()
        {
            base.Awake();

            if (leaveButton != null)
                leaveButton.onClick.AddListener(Leave);
        }

        /// <summary>Вызывается корнем HUD, когда матч найден: подписка живёт весь сеанс.</summary>
        public void Initialize(MatchManager match)
        {
            if (_match == match)
                return;

            if (_match != null)
                _match.Events.MatchFinished -= OnMatchFinished;

            _match = match;

            if (_match != null)
                _match.Events.MatchFinished += OnMatchFinished;
        }

        private void OnDestroy()
        {
            if (_match != null)
                _match.Events.MatchFinished -= OnMatchFinished;
        }

        private void OnMatchFinished(MatchOutcome outcome)
        {
            _outcome = outcome;
            Rebuild();
        }

        protected override void OnShown() => Rebuild();

        private void Rebuild()
        {
            BuildRows();

            PlayerState local = PlayerState.Local;
            int localSlot = local != null ? local.Slot : PlayerSlots.None;
            bool localWon = _outcome.HasWinner && _outcome.WinnerSlot == localSlot;

            if (titleLabel != null)
            {
                titleLabel.text = !_outcome.HasWinner
                    ? "НИЧЬЯ"
                    : localWon ? "ПОБЕДА" : "ПОРАЖЕНИЕ";

                titleLabel.color = localWon ? victoryColor : defeatColor;
            }

            if (accentBar != null)
                accentBar.color = localWon ? victoryColor : defeatColor;

            if (reasonLabel != null)
            {
                string reason = UiText.EndReason(_outcome.Reason);

                reasonLabel.text = _outcome.HasWinner
                    ? UiText.PlayerName(_outcome.WinnerSlot) + " побеждает · " + reason
                    : reason;
            }

            RefreshTable(localSlot);
        }

        private void RefreshTable(int localSlot)
        {
            _players.Clear();
            _players.AddRange(FindObjectsByType<PlayerState>(FindObjectsInactive.Exclude));

            // Порядок таблицы = порядок победы: сначала время удержания флага, затем базы.
            _players.Sort(static (a, b) =>
            {
                int byTime = b.FlagHoldSeconds.CompareTo(a.FlagHoldSeconds);
                return byTime != 0 ? byTime : b.CapturedBases.CompareTo(a.CapturedBases);
            });

            TeamColorConfig colors = _match != null && _match.Config != null ? _match.Config.TeamColors : null;
            int shown = Mathf.Min(_players.Count, _rows.Count);

            for (int i = 0; i < shown; i++)
            {
                PlayerState player = _players[i];

                _rows[i].Show(true);
                _rows[i].Apply(
                    i + 1,
                    player.Slot,
                    TeamPalette.Primary(colors, player.Slot),
                    player.FlagHoldSeconds,
                    player.CapturedBases,
                    player.ArmyCount,
                    _outcome.HasWinner && player.Slot == _outcome.WinnerSlot);
            }

            for (int i = shown; i < _rows.Count; i++)
                _rows[i].Show(false);
        }

        private void BuildRows()
        {
            if (_rows.Count > 0 || rowContainer == null || rowTemplate == null)
                return;

            rowTemplate.gameObject.SetActive(false);

            for (int i = 0; i < PlayerSlots.MaxSupported; i++)
            {
                ResultRowView row = Instantiate(rowTemplate, rowContainer);
                row.name = "ResultRow_" + i;
                row.Show(false);
                _rows.Add(row);
            }
        }

        /// <summary>
        /// Выход из матча идёт через бутстрап: он знает, какой транспорт остановить
        /// и что выйти надо ещё и из лобби платформы.
        /// </summary>
        private void Leave()
        {
            _bootstrap ??= FindAnyObjectByType<NetworkBootstrap>();
            _bootstrap?.Shutdown();
        }
    }
}
