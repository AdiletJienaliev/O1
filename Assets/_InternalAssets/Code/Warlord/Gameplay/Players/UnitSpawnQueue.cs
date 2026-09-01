using System;
using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Match;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Очередь постройки юнитов одного игрока (ГДД §5.1): очередь общая,
    /// параллельно строится parallelSpawnSlots штук. Живёт только на сервере.
    /// </summary>
    public sealed class UnitSpawnQueue
    {
        private struct Build
        {
            public int RosterIndex;
            public float Remaining;
            public float Total;
        }

        private readonly List<int> _waiting = new(16);
        private readonly List<Build> _active = new(4);
        private readonly GameModeConfig _mode;
        private readonly UnitRosterConfig _roster;
        private readonly MatchSettings _settings;

        public UnitSpawnQueue(GameModeConfig mode, UnitRosterConfig roster, in MatchSettings settings)
        {
            _mode = mode;
            _roster = roster;
            _settings = settings;
        }

        /// <summary>Юнит достроен. Аргумент — индекс типа в ростере.</summary>
        public event Action<int> BuildCompleted;

        /// <summary>Состав очереди изменился — пора обновить SyncList для владельца.</summary>
        public event Action QueueChanged;

        public int PendingCount => _waiting.Count + _active.Count;

        public void Enqueue(int rosterIndex)
        {
            _waiting.Add(rosterIndex);
            PromoteWaiting();
            QueueChanged?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            bool changed = false;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Build build = _active[i];
                build.Remaining -= deltaTime;

                if (build.Remaining > 0f)
                {
                    _active[i] = build;
                    continue;
                }

                _active.RemoveAt(i);
                changed = true;
                BuildCompleted?.Invoke(build.RosterIndex);
            }

            if (PromoteWaiting())
                changed = true;

            if (changed)
                QueueChanged?.Invoke();
        }

        /// <summary>Снимок очереди для репликации владельцу. Активные стройки идут первыми.</summary>
        public void CopyTo(List<SpawnTicket> destination, uint currentTick, float tickDelta)
        {
            destination.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                Build build = _active[i];
                uint remainingTicks = (uint)Mathf.Max(1, Mathf.CeilToInt(build.Remaining / tickDelta));

                destination.Add(new SpawnTicket
                {
                    RosterIndex = (byte)build.RosterIndex,
                    FinishTick = currentTick + remainingTicks,
                    DurationTicks = (ushort)Mathf.Max(1, Mathf.CeilToInt(build.Total / tickDelta))
                });
            }

            for (int i = 0; i < _waiting.Count; i++)
            {
                destination.Add(new SpawnTicket
                {
                    RosterIndex = (byte)_waiting[i],
                    FinishTick = 0u,
                    DurationTicks = 0
                });
            }
        }

        public void Clear()
        {
            _waiting.Clear();
            _active.Clear();
            QueueChanged?.Invoke();
        }

        private bool PromoteWaiting()
        {
            int slots = _mode != null ? Mathf.Max(1, _mode.parallelSpawnSlots) : 1;
            bool changed = false;

            while (_active.Count < slots && _waiting.Count > 0)
            {
                int rosterIndex = _waiting[0];
                _waiting.RemoveAt(0);

                float duration = _settings.ResolveSpawnTime(_roster.Get(rosterIndex), _mode);
                _active.Add(new Build { RosterIndex = rosterIndex, Remaining = duration, Total = duration });
                changed = true;
            }

            return changed;
        }
    }
}
