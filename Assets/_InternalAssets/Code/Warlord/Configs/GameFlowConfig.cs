using UnityEngine;

namespace Warlord.Configs
{
    /// <summary>
    /// Как игра запускается и что показывает по пути к матчу. Отдельный ассет от правил боя:
    /// это настройки сеанса, а не баланса, и менять их приходится по десять раз за день отладки.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Game Flow", fileName = "GameFlowConfig")]
    public sealed class GameFlowConfig : ScriptableObject
    {
        [Header("Быстрый старт")]
        [Tooltip("Сразу в бой: хост поднимается сам, экран подключения и лобби не показываются, " +
                 "матч стартует по первому подключившемуся. Главный тумблер для отладки.")]
        public bool skipLobby;

        [Tooltip("Поднять хост при старте сцены, но лобби всё равно показать. " +
                 "Не нужен, если включён skipLobby — тот и так поднимает хост.")]
        public bool autoStartHost;

        [Header("Подключение")]
        [Tooltip("Через что соединяться. Localhost — адрес и порт как раньше, Steam — лобби " +
                 "с приглашениями друзей, Auto — Steam, если он запустился, иначе адрес и порт.")]
        public NetworkBackend backend = NetworkBackend.Localhost;

        public string address = "127.0.0.1";
        public ushort port = 7770;

        [Header("Steam")]
        [Tooltip("Кто видит созданное лобби. FriendsOnly — только друзья и приглашённые, " +
                 "Private — вообще только приглашённые, Public — ещё и в поиске комнат.")]
        public SteamLobbyVisibility lobbyVisibility = SteamLobbyVisibility.FriendsOnly;

        [Tooltip("Имя комнаты в Steam. Пусто — подставится ник хоста.")]
        public string lobbyName = "";

        [Tooltip("Принимать приглашение, когда игрок уже в матче. Обычно не нужно: " +
                 "текущий бой оборвётся, а host migration в v0.1 нет.")]
        public bool acceptInvitesDuringMatch;

        [Header("Отсчёт")]
        [Tooltip("Взять длительность обратного отсчёта отсюда, а не из GameModeConfig.")]
        public bool overrideCountdown;

        [Tooltip("Отсчёт перед боем, с. При отладке удобно поставить 0 и не ждать каждый запуск.")]
        [Min(0f)] public float countdownDuration;

        [Header("Управление")]
        [Tooltip("Захватывать курсор в матче: мышь вращает камеру, приказ отдаётся по прицелу в центре экрана.")]
        public bool lockCursorInMatch = true;

        /// <summary>Нужно ли поднимать хост самим при старте сцены.</summary>
        public bool WantsAutoHost => skipLobby || autoStartHost;

        /// <summary>Отсчёт для матча: своё значение, если включено переопределение, иначе из режима.</summary>
        public float ResolveCountdown(float modeValue) => overrideCountdown ? countdownDuration : modeValue;
    }
}
