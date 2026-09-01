using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Domain.Combat;

namespace Warlord.Networking.LagCompensation
{
    /// <summary>
    /// История позиций в кольцевом буфере на каждую цель. Память фиксированная:
    /// глубина истории на частоту боевого такта, аллокаций в рантайме нет.
    /// </summary>
    public sealed class SnapshotLagCompensator : ILagCompensator
    {
        private sealed class History
        {
            public readonly float[] Times;
            public readonly Vector3[] Positions;
            public int Count;
            public int Head;

            public History(int capacity)
            {
                Times = new float[capacity];
                Positions = new Vector3[capacity];
            }

            public void Push(float time, Vector3 position)
            {
                Times[Head] = time;
                Positions[Head] = position;
                Head = (Head + 1) % Times.Length;
                if (Count < Times.Length)
                    Count++;
            }

            /// <summary>Линейная интерполяция между двумя ближайшими снимками.</summary>
            public Vector3 Sample(float time, Vector3 fallback)
            {
                if (Count == 0)
                    return fallback;

                int capacity = Times.Length;
                Vector3 newerPosition = fallback;
                float newerTime = float.MaxValue;

                for (int step = 1; step <= Count; step++)
                {
                    int index = (Head - step + capacity) % capacity;
                    float sampleTime = Times[index];

                    if (sampleTime <= time)
                    {
                        if (newerTime >= float.MaxValue)
                            return Positions[index];

                        float span = newerTime - sampleTime;
                        float t = span > 0.0001f ? (time - sampleTime) / span : 0f;
                        return Vector3.Lerp(Positions[index], newerPosition, t);
                    }

                    newerTime = sampleTime;
                    newerPosition = Positions[index];
                }

                // Запрошено время старше всей истории — отдаём самый старый известный снимок.
                return newerPosition;
            }
        }

        private readonly Dictionary<ICombatTarget, History> _histories = new(128);
        private readonly int _capacity;
        private readonly float _maxRewind;

        public SnapshotLagCompensator(NetworkConfig network)
        {
            int tickRate = network != null ? network.combatTickRate : 20;
            float seconds = network != null ? network.lagCompensationHistorySeconds : 1f;

            _capacity = Mathf.Max(4, Mathf.CeilToInt(tickRate * seconds));
            _maxRewind = network != null ? network.LagCompensationCapSeconds : 0.2f;
        }

        /// <summary>Максимальная перемотка, с. Сервер обрезает по ней RTT/2.</summary>
        public float MaxRewindSeconds => _maxRewind;

        public void Track(ICombatTarget target)
        {
            if (target != null && !_histories.ContainsKey(target))
                _histories.Add(target, new History(_capacity));
        }

        public void Untrack(ICombatTarget target)
        {
            if (target != null)
                _histories.Remove(target);
        }

        public void CaptureSnapshot(float serverTime)
        {
            foreach (KeyValuePair<ICombatTarget, History> pair in _histories)
            {
                if (pair.Key != null && pair.Key.IsAlive)
                    pair.Value.Push(serverTime, pair.Key.Position);
            }
        }

        public Vector3 GetPositionAt(ICombatTarget target, float serverTime)
        {
            if (target == null)
                return Vector3.zero;

            return _histories.TryGetValue(target, out History history)
                ? history.Sample(serverTime, target.Position)
                : target.Position;
        }

        public void Clear() => _histories.Clear();
    }
}
