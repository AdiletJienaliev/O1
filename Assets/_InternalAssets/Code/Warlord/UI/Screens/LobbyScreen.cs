using System.Collections.Generic;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Match;
using Warlord.Networking.Lobby;
using Warlord.UI.Widgets;

namespace Warlord.UI.Screens
{
    /// <summary>
    /// Комната перед матчем (ГДД §14): состав, цвета, готовность и настройки хоста.
    /// Все изменения уходят на сервер командами — экран лишь показывает подтверждённое состояние.
    /// </summary>
    public sealed class LobbyScreen : UiScreen
    {
        [Header("Слоты")]
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private LobbySlotView slotTemplate;

        [Header("Цвета")]
        [SerializeField] private RectTransform colorContainer;
        [SerializeField] private Button colorTemplate;

        [Header("Управление")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI startHintLabel;
        [SerializeField] private Button leaveButton;

        [Header("Настройки хоста")]
        [SerializeField] private GameObject hostSettingsRoot;
        [SerializeField] private Slider durationSlider;
        [SerializeField] private TextMeshProUGUI durationLabel;
        [SerializeField] private Slider startingGoldSlider;
        [SerializeField] private TextMeshProUGUI startingGoldLabel;

        [Header("Заголовок")]
        [SerializeField] private TextMeshProUGUI mapLabel;

        private readonly List<LobbySlotView> _slotViews = new(4);
        private readonly List<Button> _colorButtons = new(4);

        private LobbyManager _lobby;
        private MatchManager _match;
        private bool _localReady;
        private bool _settingsBound;

        protected override void Awake()
        {
            base.Awake();

            if (readyButton != null)
                readyButton.onClick.AddListener(ToggleReady);

            if (startButton != null)
                startButton.onClick.AddListener(RequestStart);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(Leave);
        }

        private void Update()
        {
            if (!IsVisible)
                return;

            Resolve();

            if (_lobby == null)
                return;

            BuildSlots();
            BuildColors();
            BindHostSettings();
            RefreshSlots();
            RefreshControls();
        }

        private void Resolve()
        {
            _lobby ??= FindAnyObjectByType<LobbyManager>();
            _match ??= MatchManager.Instance;
        }

        private void RefreshSlots()
        {
            int localClientId = LocalClientId();
            int hostClientId = 0;

            for (int i = 0; i < _slotViews.Count; i++)
            {
                bool exists = i < _lobby.Slots.Count;
                LobbySlotInfo info = exists ? _lobby.Slots[i] : default;

                bool isLocal = exists && info.Occupied && info.ClientId == localClientId;
                if (isLocal)
                    _localReady = info.Ready;

                _slotViews[i].Apply(
                    i,
                    exists && info.Occupied,
                    info.Ready,
                    isLocal,
                    exists && info.Occupied && info.ClientId == hostClientId,
                    TeamPalette.Primary(TeamColors(), info.ColorId));
            }
        }

        private void RefreshControls()
        {
            bool isHost = InstanceFinder.IsHostStarted;
            bool allReady = AllOccupiedReady(out int occupied);

            if (readyLabel != null)
                readyLabel.text = _localReady ? "НЕ ГОТОВ" : "ГОТОВ";

            if (hostSettingsRoot != null)
                hostSettingsRoot.SetActive(isHost);

            if (startButton != null)
            {
                startButton.gameObject.SetActive(isHost);
                startButton.interactable = allReady && occupied >= 1;
            }

            if (startHintLabel != null)
            {
                startHintLabel.gameObject.SetActive(isHost && !allReady);
                startHintLabel.text = "Ждём готовности всех игроков";
            }

            if (mapLabel == null || _match == null || _match.Config == null)
                return;

            MapConfig map = _match.Config.Map;
            mapLabel.text = map != null ? map.displayName : "Карта не задана";
        }

        private bool AllOccupiedReady(out int occupied)
        {
            occupied = 0;
            bool allReady = true;

            for (int i = 0; i < _lobby.Slots.Count; i++)
            {
                LobbySlotInfo info = _lobby.Slots[i];
                if (!info.Occupied)
                    continue;

                occupied++;
                if (!info.Ready)
                    allReady = false;
            }

            return allReady;
        }

        private void BuildSlots()
        {
            if (_slotViews.Count > 0 || slotContainer == null || slotTemplate == null)
                return;

            slotTemplate.gameObject.SetActive(false);

            int count = _match != null && _match.Config != null && _match.Config.GameMode != null
                ? _match.Config.GameMode.maxPlayers
                : PlayerSlots.MaxSupported;

            for (int i = 0; i < count; i++)
            {
                LobbySlotView view = Instantiate(slotTemplate, slotContainer);
                view.gameObject.SetActive(true);
                view.name = "LobbySlot_" + i;
                _slotViews.Add(view);
            }
        }

        private void BuildColors()
        {
            if (_colorButtons.Count > 0 || colorContainer == null || colorTemplate == null)
                return;

            TeamColorConfig colors = TeamColors();
            int count = colors != null && colors.Count > 0 ? colors.Count : PlayerSlots.MaxSupported;

            colorTemplate.gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                Button button = Instantiate(colorTemplate, colorContainer);
                button.gameObject.SetActive(true);
                button.name = "Color_" + i;

                if (button.targetGraphic is Image image)
                    image.color = TeamPalette.Primary(colors, i);

                byte colorId = (byte)i;
                button.onClick.AddListener(() => _lobby.CmdSelectColor(colorId));
                _colorButtons.Add(button);
            }
        }

        /// <summary>
        /// Ползунки настроек комнаты привязываются один раз: подписка внутри Update
        /// накопила бы обработчики и слала бы на сервер по команде за кадр.
        /// </summary>
        private void BindHostSettings()
        {
            if (_settingsBound || _match == null || _match.Config == null || _match.Config.GameMode == null)
                return;

            GameModeConfig mode = _match.Config.GameMode;
            MatchSettings settings = _lobby.Settings;

            if (durationSlider != null)
            {
                durationSlider.minValue = 60f;
                durationSlider.maxValue = Mathf.Max(300f, mode.matchDuration * 2f);
                durationSlider.value = settings.MatchDuration > 0f ? settings.MatchDuration : mode.matchDuration;
                durationSlider.onValueChanged.AddListener(_ => PushSettings());
            }

            if (startingGoldSlider != null)
            {
                startingGoldSlider.minValue = 0f;
                startingGoldSlider.maxValue = Mathf.Max(1000f, mode.startingGold * 3f);
                startingGoldSlider.wholeNumbers = true;
                startingGoldSlider.value = settings.StartingGold > 0 ? settings.StartingGold : mode.startingGold;
                startingGoldSlider.onValueChanged.AddListener(_ => PushSettings());
            }

            _settingsBound = true;
            RefreshSettingLabels();
        }

        private void PushSettings()
        {
            RefreshSettingLabels();

            if (_lobby == null || !InstanceFinder.IsHostStarted)
                return;

            MatchSettings settings = _lobby.Settings;

            if (durationSlider != null)
                settings.MatchDuration = durationSlider.value;

            if (startingGoldSlider != null)
                settings.StartingGold = Mathf.RoundToInt(startingGoldSlider.value);

            _lobby.CmdUpdateSettings(settings);
        }

        private void RefreshSettingLabels()
        {
            if (durationLabel != null && durationSlider != null)
                durationLabel.text = UiText.Clock(durationSlider.value);

            if (startingGoldLabel != null && startingGoldSlider != null)
                startingGoldLabel.text = Mathf.RoundToInt(startingGoldSlider.value).ToString();
        }

        private void ToggleReady()
        {
            if (_lobby == null)
                return;

            _localReady = !_localReady;
            _lobby.CmdSetReady(_localReady);
        }

        private void RequestStart() => _lobby?.CmdStartMatch();

        private static void Leave()
        {
            if (InstanceFinder.NetworkManager == null)
                return;

            if (InstanceFinder.IsClientStarted)
                InstanceFinder.ClientManager.StopConnection();

            if (InstanceFinder.IsServerStarted)
                InstanceFinder.ServerManager.StopConnection(true);
        }

        private TeamColorConfig TeamColors()
        {
            return _match != null && _match.Config != null ? _match.Config.TeamColors : null;
        }

        private static int LocalClientId()
        {
            return InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection != null
                ? InstanceFinder.ClientManager.Connection.ClientId
                : -1;
        }
    }
}
