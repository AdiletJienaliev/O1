using System.Collections.Generic;

namespace Warlord.Core
{
    /// <summary>
    /// Список серверных систем, отсортированный по <see cref="IServerSystem.Order"/>.
    /// Владеет только порядком вызова — вся логика живёт в самих системах.
    /// </summary>
    public sealed class ServerSystemScheduler
    {
        private readonly List<IServerSystem> _systems = new();
        private readonly List<IServerSystemLifecycle> _lifecycles = new();
        private bool _dirty;

        public IReadOnlyList<IServerSystem> Systems => _systems;

        public void Register(IServerSystem system)
        {
            if (system == null || _systems.Contains(system))
                return;

            _systems.Add(system);
            if (system is IServerSystemLifecycle lifecycle)
                _lifecycles.Add(lifecycle);

            _dirty = true;
        }

        public void Unregister(IServerSystem system)
        {
            if (system == null)
                return;

            _systems.Remove(system);
            if (system is IServerSystemLifecycle lifecycle)
                _lifecycles.Remove(lifecycle);
        }

        public void Clear()
        {
            _systems.Clear();
            _lifecycles.Clear();
        }

        public void NotifyMatchStarted()
        {
            for (int i = 0; i < _lifecycles.Count; i++)
                _lifecycles[i].OnMatchStarted();
        }

        public void NotifyMatchFinished()
        {
            for (int i = 0; i < _lifecycles.Count; i++)
                _lifecycles[i].OnMatchFinished();
        }

        public void Tick(float deltaTime)
        {
            if (_dirty)
            {
                _systems.Sort(static (a, b) => a.Order.CompareTo(b.Order));
                _dirty = false;
            }

            for (int i = 0; i < _systems.Count; i++)
                _systems[i].Tick(deltaTime);
        }
    }
}
