using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Stats;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Спавн и деспавн юнитов на сервере. Вынесен в отдельный класс, чтобы места,
    /// где юнит рождается, было ровно одно: очередь постройки, тесты и отладочные команды
    /// проходят через него же.
    /// </summary>
    public sealed class UnitFactory
    {
        private readonly NetworkManager _networkManager;
        private readonly UnitRosterConfig _roster;

        private IMatchContext _context;

        public UnitFactory(NetworkManager networkManager, UnitRosterConfig roster)
        {
            _networkManager = networkManager;
            _roster = roster;
        }

        /// <summary>Контекст приходит вторым шагом: фабрика — часть самого контекста.</summary>
        public void BindContext(IMatchContext context) => _context = context;

        public UnitEntity Spawn(
            int ownerSlot,
            int rosterIndex,
            ArmyStatsCache stats,
            Vector3 position,
            Quaternion rotation,
            NetworkConnection owner)
        {
            UnitConfig config = _roster != null ? _roster.Get(rosterIndex) : null;
            if (config == null || config.prefab == null)
            {
                Debug.LogError($"UnitFactory: нет префаба для индекса ростера {rosterIndex}");
                return null;
            }

            GameObject instance = Object.Instantiate(config.prefab, position, rotation);

            if (!instance.TryGetComponent(out UnitEntity unit))
            {
                Debug.LogError($"UnitFactory: на префабе {config.unitId} нет UnitEntity");
                Object.Destroy(instance);
                return null;
            }

            // Инициализация до Spawn: значения SyncVar попадут в стартовое состояние объекта,
            // и клиент увидит юнита уже с правильным владельцем и здоровьем.
            unit.ServerInitialize(_context, ownerSlot, rosterIndex, stats);

            _networkManager.ServerManager.Spawn(instance, owner);

            return unit;
        }

        public void Despawn(UnitEntity unit)
        {
            if (unit == null)
                return;

            NetworkObject networkObject = unit.NetworkObject;
            if (networkObject != null && networkObject.IsSpawned)
                _networkManager.ServerManager.Despawn(networkObject);
            else
                Object.Destroy(unit.gameObject);
        }
    }
}
