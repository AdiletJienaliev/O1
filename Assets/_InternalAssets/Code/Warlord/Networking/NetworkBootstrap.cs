using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Configs;

namespace Warlord.Networking
{
    /// <summary>
    /// Точка входа в сеть (ГДД §12: host-client, один из игроков — сервер).
    /// Здесь же живёт правило «выход хоста завершает матч»: host migration в v0.1 нет.
    /// </summary>
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [Header("Конфигурация")]
        [SerializeField] private GameConfig config;

        [Header("Подключение")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7770;

        [Header("Автозапуск")]
        [Tooltip("Поднять хост сразу при старте сцены независимо от GameFlowConfig.")]
        [SerializeField] private bool autoStartHost;

        private NetworkManager _networkManager;

        /// <summary>Матч завершён из-за того, что хост вышел.</summary>
        public event System.Action HostDisconnected;

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

            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
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

            if (autoStartHost || (flow != null && flow.WantsAutoHost))
                StartHost();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        /// <summary>Хост: сервер и клиент в одном процессе. Основной режим игры (ГДД §12).</summary>
        public void StartHost(ushort port = 0)
        {
            ApplyEndpoint(defaultAddress, port == 0 ? defaultPort : port);

            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        public void StartClient(string address, ushort port = 0)
        {
            ApplyEndpoint(string.IsNullOrEmpty(address) ? defaultAddress : address, port == 0 ? defaultPort : port);
            _networkManager.ClientManager.StartConnection();
        }

        /// <summary>Выделенный сервер. В v0.1 не используется, но схема его допускает.</summary>
        public void StartDedicatedServer(ushort port = 0)
        {
            ApplyEndpoint(defaultAddress, port == 0 ? defaultPort : port);
            _networkManager.ServerManager.StartConnection();
        }

        public void Shutdown()
        {
            if (_networkManager.IsClientStarted)
                _networkManager.ClientManager.StopConnection();

            if (_networkManager.IsServerStarted)
                _networkManager.ServerManager.StopConnection(true);
        }

        private void ApplyEndpoint(string address, ushort port)
        {
            Transport transport = _networkManager.TransportManager.Transport;
            transport.SetClientAddress(address);
            transport.SetPort(port);
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
