using System;
using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Match;
using Warlord.Gameplay.Capture;

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

            /// <summary>Точка, на которой появится охранник. Для полевых юнитов всегда null.</summary>
            public CapturePointBehaviour Point;

            /// <summary>
            /// Сколько золота списали при заказе. Возврат идёт именно этой суммой, а не
            /// пересчитанной ценой: охранник на точке с «Наёмниками» стоит на 30 % дешевле,
            /// и возврат по прайсу превращал бы потерю точки в источник дохода.
            /// </summary>
            public int PaidGold;
        }

        private readonly List<Build> _waiting = new(16);
        private readonly List<Build> _active = new(4);
        private readonly List<CapturePointBehaviour> _guardPoints = new(8);
        private readonly GameModeConfig _mode;
        private readonly UnitRosterConfig _roster;
        private readonly MatchSettings _settings;

        public UnitSpawnQueue(GameModeConfig mode, UnitRosterConfig roster, in MatchSettings settings)
        {
            _mode = mode;
            _roster = roster;
            _settings = settings;
        }

        /// <summary>
        /// Юнит достроен. Второй аргумент — точка гарнизона, если это охранник, иначе null:
        /// охранник появляется сразу на своей точке, а не идёт к ней пешком через полкарты (ГДД §1.6).
        /// </summary>
        public event Action<int, CapturePointBehaviour, int> BuildCompleted;

        /// <summary>
        /// Постройка охранника отменена — точка потеряна, пока он стоял в очереди (ГДД §1.6).
        /// Аргумент — сколько золота вернуть. Кладёт его обратно подписчик: очередь про кошелёк
        /// не знает и знать не должна.
        /// </summary>
        public event Action<int> BuildCancelled;

        /// <summary>
        /// Множитель времени постройки от улучшения «Кузница» (ГДД §2.5). Применяется к новым
        /// стройкам, а не к уже идущим: перезапускать таймер на середине — значит показать
        /// игроку, что прогресс откатился назад, купив ускорение.
        /// </summary>
        public float SpawnTimeMultiplier { get; set; } = 1f;

        /// <summary>Состав очереди изменился — пора обновить SyncList для владельца.</summary>
        public event Action QueueChanged;

        public int PendingCount => _waiting.Count + _active.Count;

        /// <summary>
        /// Точки охранников, которые сейчас в очереди — и ждущих, и уже строящихся.
        /// Нужны валидации покупки: без них кольцо гарнизона переполняется заказами,
        /// а лишние потом отменяются с возвратом денег и выглядят как сбой.
        /// </summary>
        public IReadOnlyList<CapturePointBehaviour> QueuedGuardPoints
        {
            get
            {
                _guardPoints.Clear();

                for (int i = 0; i < _active.Count; i++)
                {
                    if (_active[i].Point != null)
                        _guardPoints.Add(_active[i].Point);
                }

                for (int i = 0; i < _waiting.Count; i++)
                {
                    if (_waiting[i].Point != null)
                        _guardPoints.Add(_waiting[i].Point);
                }

                return _guardPoints;
            }
        }

        public void Enqueue(int rosterIndex) => Enqueue(rosterIndex, null, 0);

        /// <param name="point">Точка гарнизона для охранника или null для полевого юнита.</param>
        /// <param name="paidGold">Списанная сумма — она же вернётся при отмене.</param>
        public void Enqueue(int rosterIndex, CapturePointBehaviour point, int paidGold)
        {
            _waiting.Add(new Build { RosterIndex = rosterIndex, Point = point, PaidGold = paidGold });
            PromoteWaiting();
            QueueChanged?.Invoke();
        }

        /// <summary>
        /// Снимает с очереди охранников, чью точку игрок больше не держит. Проверку владения
        /// делает вызывающий: очередь знает про точку только то, что она есть.
        /// </summary>
        public void CancelInvalidGuards(Predicate<CapturePointBehaviour> isStillValid)
        {
            bool changed = false;

            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                if (_waiting[i].Point == null || isStillValid(_waiting[i].Point))
                    continue;

                int refund = _waiting[i].PaidGold;
                _waiting.RemoveAt(i);
                changed = true;
                BuildCancelled?.Invoke(refund);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Point == null || isStillValid(_active[i].Point))
                    continue;

                int refund = _active[i].PaidGold;
                _active.RemoveAt(i);
                changed = true;
                BuildCancelled?.Invoke(refund);
            }

            if (changed && PromoteWaiting())
                QueueChanged?.Invoke();
            else if (changed)
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
                BuildCompleted?.Invoke(build.RosterIndex, build.Point, build.PaidGold);
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
                    RosterIndex = (byte)_waiting[i].RosterIndex,
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
                Build build = _waiting[0];
                _waiting.RemoveAt(0);

                float duration = _settings.ResolveSpawnTime(_roster.Get(build.RosterIndex), _mode)
                    * Mathf.Clamp(SpawnTimeMultiplier, 0.1f, 4f);

                build.Remaining = duration;
                build.Total = duration;

                _active.Add(build);
                changed = true;
            }

            return changed;
        }
    }
}
