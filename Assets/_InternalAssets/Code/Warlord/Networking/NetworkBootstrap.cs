using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using UnityEngine;
using Warlord.Configs;

namespace Warlord.Networking
{
    /// <summary>
    /// Точка входа в сеть (ГДД §12: host-client, один из игроков — сервер).
    /// Здесь же живёт правило «выход хоста завершает матч»: host migration в v0.1 нет.
    /// </summary>
    /// <remarks>
    /// Транспортов два и они живут в сцене одновременно под <see cref="Multipass"/>: прямой
    /// (адрес и порт) и Steam P2P. Выбор делает <see cref="GameFlowConfig.backend"/> — вся
    /// разница между режимами сходится сюда, чтобы ни экраны, ни лобби про неё не знали.
    /// Поднимать сервер приходится через Multipass по индексу, а не через ServerManager:
    /// иначе он стартует все транспорты разом и в режиме адреса требовал бы живого Steam.
    /// </remarks>
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [Header("Конфигурация")]
        [SerializeField] private GameConfig config;

        [Header("Транспорты")]
        [Tooltip("Multipass со сцены. Без него доступен только транспорт, выбранный в NetworkManager.")]
        [SerializeField] private Multipass multipass;

        [Tooltip("Прямое подключение по адресу и порту (Tugboat).")]
        [SerializeField] private Transport directTransport;

        [Tooltip("Steam P2P. Пусто — режим Steam недоступен, игра идёт по адресу.")]
        [SerializeField] private Transport steamTransport;

        [Header("Платформа")]
        [Tooltip("Компонент, реализующий IPlatformSession (SteamSession). Пусто — играем без платформы.")]
        [SerializeField] private MonoBehaviour platformSource;

        [Header("Подключение")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7770;

        [Header("Автозапуск")]
        [Tooltip("Поднять хост сразу при старте сцены независимо от GameFlowConfig.")]
        [SerializeField] private bool autoStartHost;

        /// <summary>Сколько ждать инициализации платформы, прежде чем считать её недоступной.</summary>
        private const float PlatformWaitSeconds = 10f;

        private NetworkManager _networkManager;
        private IPlatformSession _platform = NullPlatformSession.Instance;
        private NetworkBackend _backend = NetworkBackend.Localhost;
        private bool _backendResolved;
        private bool _pendingAutoHost;
        private float _platformDeadline;

        /// <summary>Матч завершён из-за того, что хост вышел.</summary>
        public event System.Action HostDisconnected;

        /// <summary>Сеанс платформы. Всегда не null — без Steam это заглушка.</summary>
        public IPlatformSession Platform => _platform;

        /// <summary>Через что реально идёт соединение. До резолва — значение из конфига.</summary>
        public NetworkBackend Backend => _backend;

        /// <summary>Соединение идёт через Steam: экранам нужно прятать адрес и показывать приглашения.</summary>
        public bool UsesSteam => _backend == NetworkBackend.Steam;

        /// <summary>Можно поднимать хост или подключаться. В режиме Steam — только после его старта.</summary>
        public bool IsReady => _backendResolved && (!UsesSteam || _platform.IsReady);

        /// <summary>Что показать игроку, пока соединения нет.</summary>
        public string Status => UsesSteam || !_backendResolved ? _platform.Status : string.Empty;

        private void Awake()
        {
            _networkManager = InstanceFinder.NetworkManager;

            if (_networkManager == null)
            {
                Debug.LogError("NetworkBootstrap: NetworkManager не найден в сцене", this);
                return;
            }

            if (config != null && !config.Validate(out string error))
                Debug.LogError(error, this);

            ResolvePlatform();

            multipass ??= _networkManager.TransportManager.Transport as Multipass;

            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _platform.HostAddressReceived += OnHostAddressReceived;
        }

        private void Start()
        {
            GameFlowConfig flow = config != null ? config.Flow : null;

            if (flow != null)
            {
                // Адрес и порт из потока игры — чтобы отладочный запуск и ручное
                // подключение не расходились в двух разных местах.
                if (!string.IsNullOrWhiteSpace(flow.address))
                    defaultAddress = flow.address;

                if (flow.port != 0)
                    defaultPort = flow.port;
            }

            _pendingAutoHost = autoStartHost || (flow != null && flow.WantsAutoHost);
            _platformDeadline = Time.unscaledTime + PlatformWaitSeconds;

            TryResolveBackend();
        }

        private void Update()
        {
            // Steam поднимается асинхронно, а решение «через что играть» нужно до первого
            // подключения. Пока он не ответил — ждём, но не бесконечно: с выключенным Steam
            // игра обязана дойти до экрана подключения, а не зависнуть на пустом месте.
            if (!_backendResolved)
                TryResolveBackend();

            if (_pendingAutoHost && IsReady)
            {
                _pendingAutoHost = false;
                StartHost();
            }
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;

            _platform.HostAddressReceived -= OnHostAddressReceived;
        }

        /// <summary>Хост: сервер и клиент в одном процессе. Основной режим игры (ГДД §12).</summary>
        public void StartHost(ushort port = 0)
        {
            if (_networkManager == null)
                return;

            Transport transport = ActiveTransport();

            if (transport == null)
                return;

            ApplyEndpoint(transport, defaultAddress, port == 0 ? defaultPort : port);
            SelectClientTransport(transport);

            if (!StartServerTransport(transport))
            {
                Debug.LogError($"NetworkBootstrap: сервер не поднялся на {transport.GetType().Name}", this);
                return;
            }

            // Комната платформы создаётся уже после сервера: в её метаданных лежит адрес,
            // по которому к нам придут приглашённые, а до старта сервера его ещё нет.
            if (UsesSteam)
                _platform.HostLobby(MaxPlayers());

            _networkManager.ClientManager.StartConnection();
        }

        public void StartClient(string address, ushort port = 0)
        {
            if (_networkManager == null)
                return;

            Transport transport = ActiveTransport();

            if (transport == null)
                return;

            string target = string.IsNullOrWhiteSpace(address) ? defaultAddress : address;

            ApplyEndpoint(transport, target, port == 0 ? defaultPort : port);
            SelectClientTransport(transport);

            _networkManager.ClientManager.StartConnection();
        }

        /// <summary>Выделенный сервер. В v0.1 не используется, но схема его допускает.</summary>
        public void StartDedicatedServer(ushort port = 0)
        {
            if (_networkManager == null)
                return;

            Transport transport = ActiveTransport();

            if (transport == null)
                return;

            ApplyEndpoint(transport, defaultAddress, port == 0 ? defaultPort : port);
            StartServerTransport(transport);

            if (UsesSteam)
                _platform.HostLobby(MaxPlayers());
        }

        public void Shutdown()
        {
            if (_networkManager == null)
                return;

            _platform.LeaveLobby();

            if (_networkManager.IsClientStarted)
                _networkManager.ClientManager.StopConnection();

            if (_networkManager.IsServerStarted)
                StopServerTransport(ActiveTransport());
        }

        /// <summary>Позвать друзей в текущую комнату. Экран лобби вызывает это по кнопке.</summary>
        public void InviteFriends() => _platform.OpenInviteOverlay();

        #region Выбор режима

        private void ResolvePlatform()
        {
            if (platformSource == null)
                return;

            if (platformSource is IPlatformSession session)
                _platform = session;
            else
                Debug.LogError($"NetworkBootstrap: {platformSource.GetType().Name} не реализует IPlatformSession", this);
        }

        private void TryResolveBackend()
        {
            GameFlowConfig flow = config != null ? config.Flow : null;

            if (flow == null)
            {
                // Молчаливый откат на адрес выглядит как «Steam не работает», хотя на деле
                // ассет потока просто отвязан от GameConfig и режим читать неоткуда.
                Debug.LogWarning("NetworkBootstrap: у GameConfig не задан GameFlowConfig — " +
                                 "играем по адресу. Выполните «Warlord/Настройка/1»", this);
                Commit(NetworkBackend.Localhost);
                return;
            }

            NetworkBackend requested = flow.backend;

            if (requested == NetworkBackend.Localhost)
            {
                Commit(NetworkBackend.Localhost);
                return;
            }

            if (steamTransport == null)
            {
                Debug.LogWarning("NetworkBootstrap: транспорт Steam не назначен — играем по адресу", this);
                Commit(NetworkBackend.Localhost);
                return;
            }

            if (_platform.IsReady)
            {
                Commit(NetworkBackend.Steam);
                return;
            }

            if (Time.unscaledTime < _platformDeadline)
                return;

            if (requested == NetworkBackend.Auto)
            {
                Debug.LogWarning("NetworkBootstrap: Steam не поднялся — играем по адресу", this);
                Commit(NetworkBackend.Localhost);
                return;
            }

            // Режим Steam выбран явно: подменять его на адрес нельзя — игрок ждёт лобби
            // с друзьями, а не тихого переезда на localhost. Фиксируем и показываем причину.
            Commit(NetworkBackend.Steam);
        }

        private void Commit(NetworkBackend backend)
        {
            _backend = backend;
            _backendResolved = true;

            // Режим выбирается из конфига в рантайме, и увидеть его иначе негде: в логе
            // FishNet видно только имя транспорта, но не то, почему выбран этот.
            Debug.Log("NetworkBootstrap: соединение через " +
                      (backend == NetworkBackend.Steam ? "Steam" : "адрес и порт"), this);
        }

        private Transport ActiveTransport()
        {
            Transport transport = UsesSteam ? steamTransport : directTransport;

            if (transport != null)
                return transport;

            // Сцена может быть собрана и без Multipass — тогда транспорт ровно один.
            Transport fallback = _networkManager.TransportManager.Transport;

            if (fallback is Multipass)
            {
                Debug.LogError("NetworkBootstrap: в Multipass не назначены транспорты", this);
                return null;
            }

            return fallback;
        }

        private void SelectClientTransport(Transport transport)
        {
            if (multipass != null)
                multipass.SetClientTransport(transport);
        }

        private bool StartServerTransport(Transport transport)
        {
            if (multipass != null)
                return multipass.StartConnection(server: true, transport.Index);

            return _networkManager.ServerManager.StartConnection();
        }

        private void StopServerTransport(Transport transport)
        {
            if (multipass != null && transport != null)
                multipass.StopServerConnection(sendDisconnectMessage: true, transport.Index);
            else
                _networkManager.ServerManager.StopConnection(true);
        }

        private int MaxPlayers()
        {
            return config != null && config.GameMode != null ? config.GameMode.maxPlayers : 4;
        }

        #endregion

        private static void ApplyEndpoint(Transport transport, string address, ushort port)
        {
            transport.SetClientAddress(address);
            transport.SetPort(port);
        }

        /// <summary>
        /// Друг принял приглашение. В лобби подключаемся сразу, в бою — только если это
        /// разрешено конфигом: иначе принятое случайно приглашение обрывало бы живой матч.
        /// </summary>
        private void OnHostAddressReceived(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return;

            GameFlowConfig flow = config != null ? config.Flow : null;
            bool inMatch = _networkManager != null && _networkManager.IsServerStarted;

            if (inMatch && (flow == null || !flow.acceptInvitesDuringMatch))
            {
                Debug.LogWarning("NetworkBootstrap: приглашение пришло во время своей игры — проигнорировано", this);
                return;
            }

            if (_networkManager != null && _networkManager.IsClientStarted)
                _networkManager.ClientManager.StopConnection();

            StartClient(address);
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped)
                return;

            // Клиент потерял соединение: если мы не сервер, значит ушёл хост — матч окончен.
            if (!_networkManager.IsServerStarted)
                HostDisconnected?.Invoke();
        }
    }
}
