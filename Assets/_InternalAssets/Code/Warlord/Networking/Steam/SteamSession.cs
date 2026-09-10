using System;
using HeathenEngineering.SteamworksIntegration;
using Steamworks;
using API = HeathenEngineering.SteamworksIntegration.API;
using UnityEngine;
using Warlord.Configs;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Комната Steam: создание лобби, приглашения друзей и приём приглашений.
    /// Реализация <see cref="IPlatformSession"/> — про FishNet ничего не знает.
    /// </summary>
    /// <remarks>
    /// Адрес хоста едет в метаданных лобби, а не в поле «сервер» Steam: сервера как отдельной
    /// сущности у нас нет, хост — обычный игрок (ГДД §12), и подключаться к нему нужно по
    /// SteamID. Приглашение поэтому сводится к «войди в лобби и прочитай ключ хоста».
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SteamSession : MonoBehaviour, IPlatformSession
    {
        /// <summary>Ключ метаданных лобби, в котором лежит SteamID64 хоста.</summary>
        private const string HostKey = "warlord_host";

        /// <summary>Версия сборки хоста — на разных версиях протокол расходится.</summary>
        private const string VersionKey = "warlord_version";

        private const string NameKey = "name";

        /// <summary>Аргумент запуска, который Steam добавляет при клике «Присоединиться».</summary>
        private const string ConnectLobbyArgument = "+connect_lobby";

        [Header("Конфигурация")]
        [SerializeField] private GameConfig config;

        [Header("Steam")]
        [Tooltip("Ассет SteamSettings из пакета Heathen: в нём лежит App ID.")]
        [SerializeField] private SteamSettings settings;

        [Tooltip("Не выгружать объект Steam при смене сцены.")]
        [SerializeField] private bool keepAliveBetweenScenes = true;

        private LobbyData _lobby;
        private bool _inLobby;
        private bool _joining;
        private bool _enabled;
        private bool _hostAddressPublished;
        private bool _inviteAfterCreate;
        private int _maxMembers = 4;

        public bool IsReady { get; private set; }

        public string Status { get; private set; } = string.Empty;

        public bool InLobby => _inLobby;

        public bool IsLobbyOwner => _inLobby && _lobby.IsOwner;

        public string LocalAddress => IsReady ? UserData.Me.SteamId.ToString() : string.Empty;

        public int MemberCount => _inLobby ? _lobby.MemberCount : 0;

        /// <summary>
        /// Достаточно живого Steam: комнаты может ещё не быть, но её заведёт само нажатие.
        /// Иначе кнопка стояла бы серой всё время между запуском и созданием лобби —
        /// и навсегда, если хост не поднимался.
        /// </summary>
        public bool CanInvite => IsReady;

        public event Action<string> HostAddressReceived;

        public event Action Changed;

        private void Awake()
        {
            GameFlowConfig flow = config != null ? config.Flow : null;
            NetworkBackend backend = flow != null ? flow.backend : NetworkBackend.Localhost;

            // В режиме адреса Steam не поднимаем вовсе: игра обязана запускаться и без него,
            // иначе отладочный прогон требовал бы запущенного клиента Steam.
            _enabled = backend != NetworkBackend.Localhost;

            if (!_enabled)
            {
                Status = "Steam выключен в GameFlowConfig";
                return;
            }

            if (settings == null)
            {
                _enabled = false;
                Status = "Не задан SteamSettings";
                Debug.LogError("SteamSession: не задан ассет SteamSettings — выполните «Warlord/Настройка/15»", this);
                return;
            }

            Status = "Подключаемся к Steam...";

            API.App.evtSteamInitialized.AddListener(OnSteamInitialized);
            API.App.evtSteamInitializationError.AddListener(OnSteamInitializationError);

            SteamworksBehaviour.CreateIfMissing(settings, keepAliveBetweenScenes);

            // Steam мог подняться раньше нас — тогда события уже не будет.
            if (API.App.Initialized)
                OnSteamInitialized();
        }

        private void OnDestroy()
        {
            if (!_enabled)
                return;

            API.App.evtSteamInitialized.RemoveListener(OnSteamInitialized);
            API.App.evtSteamInitializationError.RemoveListener(OnSteamInitializationError);

            if (!IsReady)
                return;

            API.Overlay.Client.EventGameLobbyJoinRequested.RemoveListener(OnJoinRequested);
            API.Matchmaking.Client.EventLobbyDataUpdate.RemoveListener(OnLobbyDataUpdate);
            API.Matchmaking.Client.EventLobbyChatUpdate.RemoveListener(OnLobbyChatUpdate);

            LeaveLobby();
        }

        #region IPlatformSession

        public void HostLobby(int maxMembers)
        {
            if (!IsReady)
            {
                Debug.LogWarning("SteamSession: Steam ещё не готов — лобби не создать", this);
                return;
            }

            _maxMembers = Mathf.Max(1, maxMembers);

            // Приняли приглашение, а потом решили хостить сами: из чужой комнаты надо выйти,
            // иначе друзья будут звать нас туда, где сервера уже нет.
            LeaveLobby();

            Status = "Создаём лобби...";
            Changed?.Invoke();

            LobbyData.Create(Visibility(), _maxMembers, OnLobbyCreated);
        }

        public void LeaveLobby()
        {
            if (!_inLobby)
                return;

            _lobby.Leave();
            _lobby = default;
            _inLobby = false;
            _joining = false;
            _hostAddressPublished = false;

            if (IsReady)
                Status = SignedInStatus();

            Changed?.Invoke();
        }

        public void OpenInviteOverlay()
        {
            if (!IsReady)
            {
                Debug.LogWarning("SteamSession: Steam ещё не готов — звать друзей нечем", this);
                return;
            }

            // Комнаты может не быть: создание идёт асинхронно, а игрок мог и вовсе нажать
            // «пригласить» раньше, чем поднял хост. Заводим её здесь же и открываем оверлей
            // по готовности — для игрока это одно нажатие, а не «нажми ещё раз».
            if (!_inLobby)
            {
                _inviteAfterCreate = true;
                HostLobby(_maxMembers);
                return;
            }

            API.Overlay.Client.ActivateInviteDialog(_lobby);
        }

        public string GetMemberName(int index)
        {
            if (!_inLobby)
                return string.Empty;

            LobbyMemberData[] members = _lobby.Members;

            if (members == null || index < 0 || index >= members.Length)
                return string.Empty;

            return members[index].user.Name;
        }

        #endregion

        #region Точки подключения готовых виджетов Heathen

        /// <summary>
        /// Позвать конкретного друга, минуя оверлей. Нужно готовым виджетам Heathen:
        /// они отдают выбранного пользователя, а адресат приглашения — наше лобби.
        /// </summary>
        public void Invite(UserData user)
        {
            // Здесь, в отличие от оверлея, лобби обязано уже быть: виджет зовёт конкретного
            // человека, и ждать создания комнаты, держа его выбор, было бы неоткуда.
            if (!IsReady || !_inLobby || !user.IsValid)
                return;

            API.Matchmaking.Client.InviteUserToLobby(_lobby, user);
        }

        /// <summary>Текущее лобби — для компонентов Heathen, которым нужен сам LobbyData.</summary>
        public LobbyData Lobby => _lobby;

        #endregion

        #region Инициализация Steam

        private void OnSteamInitialized()
        {
            if (IsReady)
                return;

            IsReady = true;
            Status = SignedInStatus();

            API.Overlay.Client.EventGameLobbyJoinRequested.AddListener(OnJoinRequested);
            API.Matchmaking.Client.EventLobbyDataUpdate.AddListener(OnLobbyDataUpdate);
            API.Matchmaking.Client.EventLobbyChatUpdate.AddListener(OnLobbyChatUpdate);

            TryJoinFromLaunchArguments();

            Changed?.Invoke();
        }

        private void OnSteamInitializationError(string message)
        {
            IsReady = false;
            Status = "Steam недоступен: " + message;
            Debug.LogWarning("SteamSession: " + message, this);
            Changed?.Invoke();
        }

        private string SignedInStatus() => "Steam: " + UserData.Me.Name;

        /// <summary>
        /// Игру запустили кнопкой «Присоединиться» из Steam, когда она была закрыта.
        /// Steam кладёт id комнаты в аргументы запуска, и войти надо туда же, куда
        /// вошли бы по приглашению в уже запущенной игре.
        /// </summary>
        private void TryJoinFromLaunchArguments()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != ConnectLobbyArgument)
                    continue;

                if (ulong.TryParse(args[i + 1], out ulong lobbyId) && lobbyId != 0)
                    JoinLobby(LobbyData.Get(lobbyId));

                return;
            }
        }

        #endregion

        #region Лобби

        private ELobbyType Visibility()
        {
            GameFlowConfig flow = config != null ? config.Flow : null;
            SteamLobbyVisibility visibility = flow != null ? flow.lobbyVisibility : SteamLobbyVisibility.FriendsOnly;

            return visibility switch
            {
                SteamLobbyVisibility.Private => ELobbyType.k_ELobbyTypePrivate,
                SteamLobbyVisibility.Public => ELobbyType.k_ELobbyTypePublic,
                _ => ELobbyType.k_ELobbyTypeFriendsOnly
            };
        }

        private void OnLobbyCreated(EResult result, LobbyData lobby, bool ioError)
        {
            bool inviteRequested = _inviteAfterCreate;
            _inviteAfterCreate = false;

            if (ioError || result != EResult.k_EResultOK)
            {
                Status = "Не удалось создать лобби: " + result;
                Debug.LogError("SteamSession: " + Status, this);
                Changed?.Invoke();
                return;
            }

            _lobby = lobby;
            _inLobby = true;

            GameFlowConfig flow = config != null ? config.Flow : null;
            string lobbyName = flow != null && !string.IsNullOrWhiteSpace(flow.lobbyName)
                ? flow.lobbyName
                : UserData.Me.Name;

            API.Matchmaking.Client.SetLobbyData(_lobby, NameKey, lobbyName);
            API.Matchmaking.Client.SetLobbyData(_lobby, VersionKey, Application.version);
            API.Matchmaking.Client.SetLobbyData(_lobby, HostKey, UserData.Me.SteamId.ToString());
            API.Matchmaking.Client.SetLobbyJoinable(_lobby, true);

            Status = "Лобби готово — зовите друзей";
            Changed?.Invoke();

            if (inviteRequested)
                API.Overlay.Client.ActivateInviteDialog(_lobby);
        }

        /// <summary>Друг принял приглашение или нажал «Присоединиться» в списке друзей.</summary>
        private void OnJoinRequested(LobbyData lobby, UserData sender) => JoinLobby(lobby);

        private void JoinLobby(LobbyData lobby)
        {
            if (_joining || !lobby.IsValid)
                return;

            if (_inLobby && _lobby == lobby)
                return;

            LeaveLobby();

            _joining = true;
            Status = "Входим в лобби...";
            Changed?.Invoke();

            LobbyData.Join(lobby, OnLobbyEntered);
        }

        private void OnLobbyEntered(LobbyEnter enter, bool ioError)
        {
            _joining = false;

            if (ioError || enter.Response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Status = "Не удалось войти в лобби: " + enter.Response;
                Debug.LogWarning("SteamSession: " + Status, this);
                Changed?.Invoke();
                return;
            }

            _lobby = enter.Lobby;
            _inLobby = true;

            Status = "В лобби";
            Changed?.Invoke();

            PublishHostAddress();
        }

        /// <summary>
        /// Метаданные лобби могут доехать позже входа, поэтому адрес хоста ищется и здесь.
        /// </summary>
        private void OnLobbyDataUpdate(LobbyDataUpdateEventData data)
        {
            if (!_inLobby || data.lobby != _lobby)
                return;

            Changed?.Invoke();
            PublishHostAddress();
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t update)
        {
            if (_inLobby)
                Changed?.Invoke();
        }

        /// <summary>
        /// Отдать адрес хоста наверх. Своё собственное лобби пропускаем: хост подключается
        /// к себе по внутренней трубе транспорта, а не по SteamID.
        /// </summary>
        private void PublishHostAddress()
        {
            // Метаданные лобби обновляются много раз за вход; подключаться нужно один раз,
            // иначе каждое обновление рвало бы уже установленное соединение.
            if (!_inLobby || _lobby.IsOwner || _hostAddressPublished)
                return;

            string host = API.Matchmaking.Client.GetLobbyData(_lobby, HostKey);

            if (string.IsNullOrWhiteSpace(host))
                return;

            string hostVersion = API.Matchmaking.Client.GetLobbyData(_lobby, VersionKey);

            if (!string.IsNullOrWhiteSpace(hostVersion) && hostVersion != Application.version)
            {
                Status = $"Версия хоста {hostVersion}, у вас {Application.version}";
                Debug.LogWarning("SteamSession: " + Status, this);
                Changed?.Invoke();
                return;
            }

            _hostAddressPublished = true;
            HostAddressReceived?.Invoke(host);
        }

        #endregion
    }
}
