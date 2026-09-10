using System.Collections;
using System.Collections.Generic;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using API = HeathenEngineering.SteamworksIntegration.API;

namespace Warlord.Networking.Steam
{
    /// <summary>
    /// Чат комнаты Steam. Сообщения ходят через само лобби, а не через FishNet.
    /// </summary>
    /// <remarks>
    /// Так чат работает раньше и дольше матча: он жив с момента создания комнаты, переживает
    /// смену сцены и не занимает канал игровой сети. Шаблоны сообщений — готовые префабы
    /// Heathen (`My/Their Chat Message Template`), нам нужен только их <see cref="IChatMessage"/>,
    /// поэтому вид сообщения меняется подменой префаба и правок кода не требует.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SteamLobbyChat : MonoBehaviour
    {
        [Header("Сеанс")]
        [SerializeField] private SteamSession session;

        [Header("Разметка")]
        [Tooltip("Панель чата целиком: прячется, пока комнаты нет.")]
        [SerializeField] private GameObject root;

        [SerializeField] private TMP_InputField input;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform messageRoot;

        [Header("Шаблоны Heathen")]
        [SerializeField] private GameObject myMessageTemplate;
        [SerializeField] private GameObject theirMessageTemplate;

        [Header("История")]
        [Tooltip("Сколько сообщений держать на экране. Старые удаляются сверху.")]
        [SerializeField, Min(10)] private int maxMessages = 200;

        private readonly List<IChatMessage> _messages = new();
        private bool _subscribed;

        private void Awake() => session ??= FindAnyObjectByType<SteamSession>();

        private void OnEnable()
        {
            if (session == null)
            {
                Debug.LogError("SteamLobbyChat: не найден SteamSession — чат работать не будет", this);
                return;
            }

            API.Matchmaking.Client.EventLobbyChatMsg.AddListener(OnChatMessage);

            if (input != null)
                input.onSubmit.AddListener(OnSubmit);

            _subscribed = true;
            ApplyVisibility();
        }

        private void OnDisable()
        {
            if (!_subscribed)
                return;

            API.Matchmaking.Client.EventLobbyChatMsg.RemoveListener(OnChatMessage);

            if (input != null)
                input.onSubmit.RemoveListener(OnSubmit);

            _subscribed = false;
        }

        private void Update() => ApplyVisibility();

        /// <summary>Отправить строку в комнату. Годится и как цель кнопки «отправить».</summary>
        public void Send(string message)
        {
            if (session == null || !session.InLobby || string.IsNullOrWhiteSpace(message))
                return;

            session.Lobby.SendChatMessage(message);
        }

        private void OnSubmit(string message)
        {
            Send(message);

            if (input == null)
                return;

            input.text = string.Empty;

            // Ввод теряет фокус на Enter, а чат обычно набирают подряд.
            StartCoroutine(RefocusInput());
        }

        private void ApplyVisibility()
        {
            if (root == null || session == null)
                return;

            bool visible = session.InLobby;

            if (root.activeSelf != visible)
                root.SetActive(visible);
        }

        private void OnChatMessage(LobbyChatMsg message)
        {
            // Комнат может быть несколько (например, групповая и матчевая) — берём только свою.
            if (session == null || !session.InLobby || message.lobby != session.Lobby)
                return;

            if (messageRoot == null)
                return;

            bool mine = message.sender == UserData.Me;
            GameObject template = mine ? myMessageTemplate : theirMessageTemplate;

            if (template == null)
                return;

            while (_messages.Count >= maxMessages && _messages.Count > 0)
            {
                Destroy(_messages[0].GameObject);
                _messages.RemoveAt(0);
            }

            GameObject instance = Instantiate(template, messageRoot);
            instance.transform.SetAsLastSibling();

            if (!instance.TryGetComponent(out IChatMessage view))
            {
                Destroy(instance);
                return;
            }

            view.Initialize(message);

            // Подряд идущие сообщения одного человека показываются без повтора шапки.
            if (_messages.Count > 0 && _messages[_messages.Count - 1].User == view.User)
                view.IsExpanded = false;

            _messages.Add(view);
            StartCoroutine(ScrollToBottom());
        }

        /// <summary>
        /// Прокрутка и фокус ждут конца кадра: раскладка нового сообщения считается вёрсткой
        /// уже после нас, и до неё позиция прокрутки относится к прежнему содержимому.
        /// </summary>
        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (scroll != null)
                scroll.verticalNormalizedPosition = 0f;
        }

        private IEnumerator RefocusInput()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            if (input != null)
                input.ActivateInputField();
        }
    }
}
