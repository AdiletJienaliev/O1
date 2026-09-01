using UnityEngine;

namespace Warlord.Domain.Match
{
    /// <summary>Таймер матча. Живёт на сервере, наружу отдаёт только оставшееся время.</summary>
    public sealed class MatchClock
    {
        private float _elapsed;

        public MatchClock(float duration) => Duration = Mathf.Max(1f, duration);

        public float Duration { get; }
        public float Elapsed => _elapsed;
        public float Remaining => Mathf.Max(0f, Duration - _elapsed);
        public bool IsExpired => _elapsed >= Duration;

        public void Tick(float deltaTime)
        {
            if (!IsExpired)
                _elapsed += deltaTime;
        }

        public void Reset() => _elapsed = 0f;
    }
}
