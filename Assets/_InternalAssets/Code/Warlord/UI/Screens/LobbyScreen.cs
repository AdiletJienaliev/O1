using System.Collections.Generic;
using System.Text;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;
using Warlord.Configs.Bots;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.World;
using Warlord.Networking;
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

        [Header("Боты")]
        [Tooltip("Сажает ботов во все свободные слоты. Виден только хосту.")]
        [SerializeField] private Button fillBotsButton;

        [Tooltip("Сложность, с которой садятся новые боты. Каждому потом можно сменить её в его строке.")]
        [SerializeField] private Button botDifficultyButton;
        [SerializeField] private TextMeshProUGUI botDifficultyLabel;

        [Header("Заголовок")]
        [SerializeField] private TextMeshProUGUI mapLabel;

        [Header("Приглашения")]
        [Tooltip("Открывает оверлей Steam со списком друзей. В режиме адреса кнопка скрыта.")]
        [SerializeField] private Button inviteButton;

        [Tooltip("Кто уже в комнате платформы — до того, как подключился к матчу.")]
        [SerializeField] private TextMeshProUGUI platformMembersLabel;

        private readonly List<LobbySlotView> _slotViews = new(4);
        private readonly List<Button> _colorButtons = new(4);
        private readonly StringBuilder _members = new();

        private LobbyManager _lobby;
        private MatchManager _match;
        private NetworkBootstrap _bootstrap;
        private bool _localReady;
        private bool _settingsBound;

        /// <summary>
        /// С какой сложностью садится следующий бот. Дальше её крутит кнопка в самой строке —
        /// это лишь значение по умолчанию, чтобы набрать комнату одинаковых противников
        /// в четыре клика, а не в восемь.
        /// </summary>
        private BotDifficulty _newBotDifficulty = BotDifficulty.Normal;

        protected override void Awake()
        {
            base.Awake();

            if (readyButton != null)
                readyButton.onClick.AddListener(ToggleReady);

            if (startButton != null)
                startButton.onClick.AddListener(RequestStart);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(Leave);

            if (inviteButton != null)
                inviteButton.onClick.AddListener(InviteFriends);

            if (fillBotsButton != null)
                fillBotsButton.onClick.AddListener(FillWithBots);

            if (botDifficultyButton != null)
                botDifficultyButton.onClick.AddListener(CycleNewBotDifficulty);
        }

        private bool HasEmptySlot()
        {
            for (int i = 0; i < _lobby.Slots.Count; i++)
            {
                if (!_lobby.Slots[i].Occupied)
                    return true;
            }

            return false;
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
            RefreshPlatform();
        }

        private void Resolve()
        {
            _lobby ??= FindAnyObjectByType<LobbyManager>();
            _match ??= MatchManager.Instance;
            _bootstrap ??= FindAnyObjectByType<NetworkBootstrap>();
        }

        /// <summary>
        /// Приглашения показываются только там, где они работают: в режиме адреса звать
        /// друзей нечем, и кнопка, которая ничего не делает, хуже её отсутствия.
        /// </summary>
        private void RefreshPlatform()
        {
            IPlatformSession platform = _bootstrap != null ? _bootstrap.Platform : NullPlatformSession.Instance;

            if (inviteButton != null)
            {
                // Кнопку показывает сам режим, а не готовность Steam: пока тот поднимается,
                // она должна стоять серой на своём месте, а не появляться из ниоткуда.
                bool steam = _bootstrap != null && _bootstrap.UsesSteam;
                inviteButton.gameObject.SetActive(steam || platform.IsReady);
                inviteButton.interactable = platform.CanInvite;
            }

            if (platformMembersLabel == null)
                return;

            platformMembersLabel.gameObject.SetActive(platform.InLobby);

            if (!platform.InLobby)
                return;

            _members.Clear();

            for (int i = 0; i < platform.MemberCount; i++)
            {
                if (_members.Length > 0)
                    _members.Append(", ");

                _members.Append(platform.GetMemberName(i));
            }

            platformMembersLabel.text = _members.ToString();
        }

        private void InviteFriends() => _bootstrap?.InviteFriends();

        private void RefreshSlots()
        {
            int localClientId = LocalClientId();
            const int HostClientId = 0;

            bool localIsHost = InstanceFinder.IsHostStarted;
            bool botsAvailable = _lobby.BotsAvailable;

            for (int i = 0; i < _slotViews.Count; i++)
            {
                bool exists = i < _lobby.Slots.Count;
                LobbySlotInfo info = exists ? _lobby.Slots[i] : LobbySlotInfo.Empty(i);

                bool isLocal = info.IsHuman && info.ClientId == localClientId;
                if (isLocal)
                    _localReady = info.Ready;

                _slotViews[i].Apply(new LobbySlotView.Data
                {
                    Index = i,
                    Info = info,
                    IsLocal = isLocal,
                    IsHostSlot = info.IsHuman && info.ClientId == HostClientId,
                    LocalIsHost = localIsHost,
                    BotsAvailable = botsAvailable,
                    TeamColor = TeamPalette.Primary(TeamColors(), info.ColorId),
                    DisplayName = ResolveName(in info, i, isLocal),
                    PersonalityName = ResolvePersonality(in info)
                });
            }
        }

        /// <summary>Как зовётся сидящий в слоте. Имя бота выводится из набора — по сети оно не едет.</summary>
        private string ResolveName(in LobbySlotInfo info, int index, bool isLocal)
        {
            if (info.IsBot)
            {
                BotSetConfig set = _lobby.BotSet;
                return set != null ? set.ResolveName(info.BotPersonality, info.BotNameIndex) : "Бот";
            }

            if (!info.Occupied)
                return "Свободно";

            return isLocal ? UiText.PlayerName(index) + " (вы)" : UiText.PlayerName(index);
        }

        private string ResolvePersonality(in LobbySlotInfo info)
        {
            BotSetConfig set = _lobby.BotSet;
            BotPersonalityConfig personality = set != null ? set.Get(info.BotPersonality) : null;

            return personality != null ? personality.displayName : "Характер";
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

            bool botsAvailable = _lobby.BotsAvailable;

            if (fillBotsButton != null)
            {
                fillBotsButton.gameObject.SetActive(isHost && botsAvailable);
                fillBotsButton.interactable = HasEmptySlot();
            }

            if (botDifficultyButton != null)
                botDifficultyButton.gameObject.SetActive(isHost && botsAvailable);

            if (botDifficultyLabel != null)
                botDifficultyLabel.text = "НОВЫЕ БОТЫ: " + UiText.Difficulty(_newBotDifficulty).ToUpperInvariant();

            if (mapLabel == null || _match == null || _match.Config == null)
                return;

            mapLabel.text = MatchArena.Title;
        }

        /// <summary>Готовность ждём только от живых: бот готов всегда, ему нечего нажимать.</summary>
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

                if (info.IsHuman && !info.Ready)
                    allReady = false;
            }

            return allReady;
        }

        /// <summary>Посадить ботов во все пустые слоты — самый частый способ начать игру одному.</summary>
        private void FillWithBots()
        {
            if (_lobby == null || !_lobby.BotsAvailable)
                return;

            for (int i = 0; i < _lobby.Slots.Count; i++)
            {
                if (!_lobby.Slots[i].Occupied)
                    _lobby.CmdAddBot((byte)i, (byte)_newBotDifficulty);
            }
        }

        /// <summary>Сложность, с которой садятся следующие боты. Перебирается по кругу.</summary>
        private void CycleNewBotDifficulty()
        {
            int next = ((int)_newBotDifficulty + 1) % ((int)BotDifficulty.Brutal + 1);
            _newBotDifficulty = (BotDifficulty)next;
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

                BindSlotButtons(view, i);
                _slotViews.Add(view);
            }
        }

        /// <summary>
        /// Кнопки строки привязываются один раз при создании: подписка из Update
        /// накопила бы обработчики и слала бы на сервер по команде за кадр.
        /// Сложность нового бота берётся из ползунка комнаты, дальше её крутит сама строка.
        /// </summary>
        private void BindSlotButtons(LobbySlotView view, int index)
        {
            byte slot = (byte)index;

            if (view.AddBotButton != null)
                view.AddBotButton.onClick.AddListener(() => _lobby?.CmdAddBot(slot, (byte)_newBotDifficulty));

            if (view.RemoveBotButton != null)
                view.RemoveBotButton.onClick.AddListener(() => _lobby?.CmdRemoveBot(slot));

            if (view.PersonalityButton != null)
                view.PersonalityButton.onClick.AddListener(() => _lobby?.CmdCycleBotPersonality(slot));

            if (view.DifficultyButton != null)
                view.DifficultyButton.onClick.AddListener(() => _lobby?.CmdCycleBotDifficulty(slot));

            if (view.TeamButton != null)
                view.TeamButton.onClick.AddListener(() => _lobby?.CmdCycleTeam(slot));
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

        /// <summary>
        /// Выход из комнаты идёт через бутстрап: он же знает, какой транспорт остановить
        /// и что выйти надо ещё и из лобби платформы.
        /// </summary>
        private void Leave()
        {
            _bootstrap ??= FindAnyObjectByType<NetworkBootstrap>();
            _bootstrap?.Shutdown();
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
