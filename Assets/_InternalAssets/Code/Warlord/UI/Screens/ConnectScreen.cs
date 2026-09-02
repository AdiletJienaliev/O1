using FishNet;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Networking;

namespace Warlord.UI.Screens
{
    /// <summary>
    /// Вход в игру: поднять хост или подключиться к нему (ГДД §12 — host-client, выделенного
    /// сервера в v0.1 нет). Экран не знает про транспорт: всё делает <see cref="NetworkBootstrap"/>.
    /// </summary>
    public sealed class ConnectScreen : UiScreen
    {
        [Header("Сеть")]
        [SerializeField] private NetworkBootstrap bootstrap;

        [Header("Кнопки")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button quitButton;

        [Header("Адрес")]
        [SerializeField] private TMP_InputField addressField;
        [SerializeField] private TMP_InputField portField;

        [Header("Статус")]
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private TextMeshProUGUI versionLabel;

        [Header("Значения по умолчанию")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7770;

        protected override void Awake()
        {
            base.Awake();

            bootstrap ??= FindAnyObjectByType<NetworkBootstrap>();

            if (addressField != null && string.IsNullOrEmpty(addressField.text))
                addressField.text = defaultAddress;

            if (portField != null && string.IsNullOrEmpty(portField.text))
                portField.text = defaultPort.ToString();

            if (versionLabel != null)
                versionLabel.text = Application.version;

            if (hostButton != null)
                hostButton.onClick.AddListener(StartHost);

            if (joinButton != null)
                joinButton.onClick.AddListener(StartClient);

            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);
        }

        private void OnEnable()
        {
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState += OnConnectionState;
        }

        private void OnDisable()
        {
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState -= OnConnectionState;
        }

        private void StartHost()
        {
            if (!EnsureBootstrap())
                return;

            SetStatus("Поднимаем хост...");
            bootstrap.StartHost(ReadPort());
        }

        private void StartClient()
        {
            if (!EnsureBootstrap())
                return;

            string address = addressField != null && !string.IsNullOrWhiteSpace(addressField.text)
                ? addressField.text.Trim()
                : defaultAddress;

            SetStatus("Подключаемся к " + address + "...");
            bootstrap.StartClient(address, ReadPort());
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private ushort ReadPort()
        {
            if (portField == null || !ushort.TryParse(portField.text, out ushort port) || port == 0)
                return defaultPort;

            return port;
        }

        private bool EnsureBootstrap()
        {
            bootstrap ??= FindAnyObjectByType<NetworkBootstrap>();

            if (bootstrap != null)
                return true;

            SetStatus("NetworkBootstrap не найден в сцене");
            return false;
        }

        private void OnConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Starting:
                    SetStatus("Соединение...");
                    break;
                case LocalConnectionState.Started:
                    SetStatus("Подключено");
                    break;
                case LocalConnectionState.Stopped:
                    SetStatus("Соединение разорвано");
                    break;
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }
    }
}
