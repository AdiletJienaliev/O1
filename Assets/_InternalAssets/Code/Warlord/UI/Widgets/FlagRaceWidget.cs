using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Гонка за центральный флаг (ГДД §2, §9): сколько времени удержания набрал каждый игрок
    /// и кто держит флаг прямо сейчас. Фиксированной цели нет, поэтому полосы нормируются
    /// по лидеру — видно отставание, а не абстрактный процент.
    /// </summary>
    public sealed class FlagRaceWidget : HudWidget
    {
        [Header("Строки")]
        [SerializeField] private RectTransform container;
        [SerializeField] private FlagRaceRowView rowTemplate;

        [Header("Центральный флаг")]
        [SerializeField] private TextMeshProUGUI flagOwnerLabel;
        [SerializeField] private Image flagOwnerTab;
        [SerializeField] private Image captureFill;
        [SerializeField] private GameObject captureRoot;

        [Tooltip("Как часто пересобирать список игроков, с. Между пересборками строки просто обновляются.")]
        [SerializeField] private float rescanInterval = 2f;

        private readonly List<FlagRaceRowView> _rows = new(4);
        private readonly List<PlayerState> _players = new(4);

        private CapturePointBehaviour _centralFlag;
        private float _rescanTimer;

        protected override void OnInitialized()
        {
            BuildRows();
            Rescan();
        }

        public override void Refresh(PlayerState localPlayer)
        {
            if (!HasMatch || _rows.Count == 0)
                return;

            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
                Rescan();

            RefreshRows(localPlayer);
            RefreshCentralFlag();
        }

        private void RefreshRows(PlayerState localPlayer)
        {
            int localSlot = localPlayer != null ? localPlayer.Slot : PlayerSlots.None;
            int flagOwner = _centralFlag != null ? _centralFlag.OwnerSlot : PlayerSlots.None;

            float leader = 1f;
            for (int i = 0; i < _players.Count; i++)
            {
                float value = _players[i].FlagHoldSeconds;
                if (value > leader)
                    leader = value;
            }

            int shown = Mathf.Min(_players.Count, _rows.Count);

            for (int i = 0; i < shown; i++)
            {
                PlayerState player = _players[i];
                FlagRaceRowView row = _rows[i];

                row.Show(true);
                row.Bind(player.Slot, SlotColor(player.Slot), SlotSigil(player.Slot), player.Slot == localSlot);
                row.SetState(player.FlagHoldSeconds, player.FlagHoldSeconds / leader, player.Slot == flagOwner, player.IsEliminated);
            }

            for (int i = shown; i < _rows.Count; i++)
                _rows[i].Show(false);
        }

        private void RefreshCentralFlag()
        {
            if (_centralFlag == null)
            {
                if (captureRoot != null)
                    captureRoot.SetActive(false);

                return;
            }

            if (captureRoot != null && !captureRoot.activeSelf)
                captureRoot.SetActive(true);

            int owner = _centralFlag.OwnerSlot;
            CaptureStatus status = _centralFlag.Status;

            if (flagOwnerLabel != null)
                flagOwnerLabel.text = DescribeFlag(owner, status);

            Color color = SlotColor(owner);

            if (flagOwnerTab != null)
                flagOwnerTab.color = color;

            if (captureFill == null)
                return;

            // Пока идёт перехват, интереснее шкала претендента: она показывает, сколько осталось.
            bool challenging = status == CaptureStatus.Capturing || status == CaptureStatus.Losing;
            captureFill.fillAmount = challenging ? _centralFlag.ChallengerProgress : _centralFlag.OwnerProgress;
            captureFill.color = challenging ? SlotColor(_centralFlag.ChallengerSlot) : color;
        }

        private static string DescribeFlag(int owner, CaptureStatus status)
        {
            switch (status)
            {
                case CaptureStatus.Contested: return "ФЛАГ ОСПАРИВАЕТСЯ";
                case CaptureStatus.Capturing: return "ФЛАГ ЗАХВАТЫВАЮТ";
                case CaptureStatus.Losing: return "ФЛАГ ТЕРЯЕТСЯ";
                case CaptureStatus.Owned: return "ФЛАГ: " + UiText.PlayerName(owner).ToUpperInvariant();
                default: return "ФЛАГ СВОБОДЕН";
            }
        }

        private Color SlotColor(int slot)
        {
            TeamColorConfig colors = Match.Config != null ? Match.Config.TeamColors : null;
            return TeamPalette.Primary(colors, slot);
        }

        private Sprite SlotSigil(int slot)
        {
            TeamColorConfig colors = Match.Config != null ? Match.Config.TeamColors : null;
            return TeamPalette.Sigil(colors, slot);
        }

        private void BuildRows()
        {
            if (_rows.Count > 0 || container == null || rowTemplate == null)
                return;

            rowTemplate.gameObject.SetActive(false);

            for (int i = 0; i < PlayerSlots.MaxSupported; i++)
            {
                FlagRaceRowView row = Instantiate(rowTemplate, container);
                row.name = "FlagRow_" + i;
                row.Show(false);
                _rows.Add(row);
            }
        }

        /// <summary>
        /// Пересборка списка игроков. Состояния игроков — сетевые объекты, они появляются
        /// и исчезают по ходу матча, поэтому периодический скан проще и надёжнее подписок.
        /// </summary>
        private void Rescan()
        {
            _rescanTimer = Mathf.Max(0.25f, rescanInterval);

            _players.Clear();
            _players.AddRange(Object.FindObjectsByType<PlayerState>(FindObjectsInactive.Exclude));
            _players.Sort(static (a, b) => a.Slot.CompareTo(b.Slot));

            if (_centralFlag != null)
                return;

            CapturePointBehaviour[] points = Object.FindObjectsByType<CapturePointBehaviour>(FindObjectsInactive.Exclude);

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].Kind != CapturePointKind.CentralFlag)
                    continue;

                _centralFlag = points[i];
                break;
            }
        }
    }
}
