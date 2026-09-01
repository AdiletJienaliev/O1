using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;

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
        [Tooltip("Запускать матч сразу при первом подключении, минуя лобби. Только для тестов.")]
        [SerializeField] private bool autoStartMatch;

        private readonly Dictionary<NetworkConnection, PlayerState> _spawned = new();
        private readonly List<NetworkConnection> _pending = new();

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

            if (autoStartMatch && _matchManager != null && _matchManager.Phase == MatchPhase.Lobby)
                _matchManager.ServerStartMatch(_matchManager.Settings);

            if (!TrySpawnFor(connection))
                _pending.Add(connection);
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            if (phase != MatchPhase.Running || _pending.Count == 0)
                return;

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

            int slot = manager.ServerContext.Players.FindFreeSlot();
            if (!PlayerSlots.IsValid(slot))
            {
                Debug.LogWarning($"WarlordPlayerSpawner: свободных слотов нет, клиент {connection.ClientId} остаётся наблюдателем");
                return true;
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
