using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Configs.Bots;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Bots;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.World;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Создаёт для игрока пару объектов: состояние и полководца. Объекты появляются
    /// только после старта матча — в лобби контекста ещё нет, и слот выдавать не из чего.
    /// Подключившиеся раньше ждут в очереди, подключившиеся позже спавнятся сразу.
    /// </summary>
    public sealed class WarlordPlayerSpawner : MonoBehaviour
    {
        [Header("Префабы")]
        [SerializeField] private NetworkObject playerStatePrefab;
        [SerializeField] private NetworkObject heroPrefab;

        [Header("Отладка")]
        [Tooltip("Запускать матч сразу при первом подключении, минуя лобби. " +
                 "То же делает галочка skipLobby в GameFlowConfig — здесь ручной вариант.")]
        [SerializeField] private bool autoStartMatch;

        private readonly Dictionary<NetworkConnection, PlayerState> _spawned = new();
        private readonly List<NetworkConnection> _pending = new();

        /// <summary>Слоты, под которые бот уже создан. Второй раз его создавать нельзя.</summary>
        private readonly HashSet<int> _botSlots = new();

        private NetworkManager _networkManager;
        private MatchManager _matchManager;

        private void Awake()
        {
            _networkManager = GetComponentInParent<NetworkManager>();
            _networkManager ??= InstanceFinder.NetworkManager;

            if (_networkManager == null)
            {
                Debug.LogError("WarlordPlayerSpawner: NetworkManager не найден", this);
                return;
            }

            _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        private void Start()
        {
            _matchManager = MatchManager.Instance;
            if (_matchManager != null)
                _matchManager.Events.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (_matchManager != null)
                _matchManager.Events.PhaseChanged -= OnPhaseChanged;

            if (_networkManager == null)
                return;

            _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        private void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer)
                return;

            _matchManager ??= MatchManager.Instance;

            if (ShouldSkipLobby() && _matchManager.Phase == MatchPhase.Lobby)
                _matchManager.ServerStartMatch(_matchManager.Settings);

            if (!TrySpawnFor(connection))
                _pending.Add(connection);
        }

        /// <summary>
        /// Идём ли в матч мимо лобби: либо галочка в GameFlowConfig, либо ручной флаг на спавнере.
        /// </summary>
        private bool ShouldSkipLobby()
        {
            if (_matchManager == null)
                return false;

            return autoStartMatch || (_matchManager.Config != null && _matchManager.Config.SkipLobby);
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            if (phase != MatchPhase.Running)
                return;

            SpawnBots();

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (TrySpawnFor(_pending[i]))
                    _pending.RemoveAt(i);
            }
        }

        private bool TrySpawnFor(NetworkConnection connection)
        {
            if (connection == null || !connection.IsActive || _spawned.ContainsKey(connection))
                return true;

            MatchManager manager = _matchManager;
            if (manager == null || manager.ServerContext == null)
                return false;

            int slot = ResolveSlot(connection, manager);
            if (!PlayerSlots.IsValid(slot))
            {
                Debug.LogWarning($"WarlordPlayerSpawner: свободных слотов нет, клиент {connection.ClientId} остаётся наблюдателем");
                return true;
            }

            if (PlayerBase.Get(slot) == null)
            {
                Debug.LogError(
                    $"WarlordPlayerSpawner: в сцене нет базы для слота {slot}. " +
                    "Поставьте объект с PlayerBase (меню Warlord/Настройка) — иначе спавн уедет в начало координат.",
                    this);
            }

            PlayerBaseAnchor anchor = manager.ServerContext.Players.GetBaseAnchor(slot);
            Quaternion rotation = Quaternion.Euler(0f, anchor.YawDegrees, 0f);

            PlayerState player = SpawnPlayerState(connection, manager, slot);
            if (player == null)
                return true;

            HeroController hero = SpawnHero(connection, manager, slot, anchor.HeroSpawnPoint, rotation);

            _spawned[connection] = player;
            manager.ServerRegisterPlayer(player, hero);
            return true;
        }

        /// <summary>
        /// Слот игрока. Сначала спрашиваем состав из лобби: место, выбранное в комнате,
        /// должно достаться тому же человеку и в бою, иначе он окажется в слоте, который
        /// комната отдала боту. Без состава — как раньше, первый свободный.
        /// </summary>
        private int ResolveSlot(NetworkConnection connection, MatchManager manager)
        {
            MatchRoster roster = manager.ServerContext.Roster;

            if (roster != null && connection != null)
            {
                int reserved = roster.FindSlotForClient(connection.ClientId);

                if (PlayerSlots.IsValid(reserved) && manager.ServerContext.Players.Get(reserved) == null)
                    return reserved;
            }

            // Слот, обещанный боту, человеку не отдаём даже если бот ещё не появился:
            // иначе подключившийся первым занял бы чужое место и бот остался бы без слота.
            for (int i = 0; i < PlayerSlots.MaxSupported; i++)
            {
                if (manager.ServerContext.Players.Get(i) != null)
                    continue;

                if (roster != null && roster.IsBot(i))
                    continue;

                return i;
            }

            return PlayerSlots.None;
        }

        private PlayerState SpawnPlayerState(NetworkConnection connection, MatchManager manager, int slot)
        {
            if (playerStatePrefab == null)
            {
                Debug.LogError("WarlordPlayerSpawner: не задан префаб PlayerState", this);
                return null;
            }

            NetworkObject instance = _networkManager.GetPooledInstantiated(playerStatePrefab, Vector3.zero, Quaternion.identity, true);
            PlayerState player = instance.GetComponent<PlayerState>();

            // Инициализация до Spawn: клиент сразу получит объект с правильным слотом и балансом.
            player.ServerInitialize(manager.ServerContext, slot);
            _networkManager.ServerManager.Spawn(instance, connection);

            // У бота владельца нет, и «сцены владельца» для него не существует.
            if (connection != null)
                _networkManager.SceneManager.AddOwnerToDefaultScene(instance);

            return player;
        }

        private HeroController SpawnHero(
            NetworkConnection connection,
            MatchManager manager,
            int slot,
            Vector3 position,
            Quaternion rotation)
        {
            if (heroPrefab == null)
            {
                Debug.LogError("WarlordPlayerSpawner: не задан префаб полководца", this);
                return null;
            }

            NetworkObject instance = _networkManager.GetPooledInstantiated(heroPrefab, position, rotation, true);
            HeroController hero = instance.GetComponent<HeroController>();

            hero.ServerInitialize(manager.ServerContext, slot);
            _networkManager.ServerManager.Spawn(instance, connection);

            return hero;
        }

        #region Боты

        /// <summary>
        /// Создать ботов стартового состава. Бот получает ровно те же два объекта, что и
        /// человек: состояние игрока и полководца. Отличие ровно одно — владельца у них нет,
        /// поэтому тело ведёт сервер, а команды идут не по сети, а прямым вызовом.
        /// </summary>
        private void SpawnBots()
        {
            MatchManager manager = _matchManager;

            if (manager == null || manager.ServerContext == null)
                return;

            MatchRoster roster = manager.ServerContext.Roster;
            if (roster == null)
                return;

            BotSetConfig set = manager.Config != null ? manager.Config.Bots : null;

            for (int slot = 0; slot < roster.SlotCount; slot++)
            {
                MatchSlotSetup setup = roster.Get(slot);

                if (!setup.IsBot || _botSlots.Contains(slot))
                    continue;

                if (manager.ServerContext.Players.Get(slot) != null)
                    continue;

                if (set == null || set.Count == 0)
                {
                    Debug.LogError(
                        "WarlordPlayerSpawner: в комнате есть боты, но у GameConfig не задан набор ботов. " +
                        "Выполните пункт меню Warlord/Настройка/16.",
                        this);
                    return;
                }

                SpawnBot(manager, slot, in setup, set);
            }
        }

        private void SpawnBot(MatchManager manager, int slot, in MatchSlotSetup setup, BotSetConfig set)
        {
            int personalityIndex = set.IsValidIndex(setup.Personality) ? setup.Personality : set.DefaultPersonality;

            BotPersonalityConfig personality = set.Get(personalityIndex);
            if (personality == null)
                return;

            if (PlayerBase.Get(slot) == null)
            {
                Debug.LogError($"WarlordPlayerSpawner: в сцене нет базы для слота {slot} — бот не создан", this);
                return;
            }

            BotProfile profile = new(
                personality,
                set.GetDifficulty(setup.Difficulty),
                setup.Difficulty,
                personalityIndex,
                setup.NameIndex);

            PlayerBaseAnchor anchor = manager.ServerContext.Players.GetBaseAnchor(slot);
            Quaternion rotation = Quaternion.Euler(0f, anchor.YawDegrees, 0f);

            PlayerState player = SpawnPlayerState(null, manager, slot);
            if (player == null)
                return;

            // Пометка сразу после спавна: таблица счёта и HUD должны увидеть бота
            // с именем и сложностью, а не безымянного игрока.
            player.ServerMarkAsBot(personalityIndex, setup.Difficulty, setup.NameIndex);

            HeroController hero = SpawnHero(null, manager, slot, anchor.HeroSpawnPoint, rotation);
            if (hero == null)
                return;

            BotHeroPilot pilot = hero.gameObject.AddComponent<BotHeroPilot>();
            pilot.Configure(manager.Config.Hero, new System.Random(slot * 6151 + setup.NameIndex + 1));

            hero.ServerAttachBot(pilot);

            manager.ServerRegisterBot(player, hero, in profile, pilot);
            _botSlots.Add(slot);

            Debug.Log($"Warlord: слот {slot} занял бот {profile.Name} ({personality.displayName}, {setup.Difficulty})", this);
        }

        #endregion

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            _pending.Remove(connection);

            if (!_spawned.TryGetValue(connection, out PlayerState player))
                return;

            _spawned.Remove(connection);
            MatchManager.Instance?.ServerUnregisterPlayer(player);
        }
    }
}
