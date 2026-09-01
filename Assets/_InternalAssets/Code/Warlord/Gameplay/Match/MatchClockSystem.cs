using Warlord.Core;
using Warlord.Domain.Match;

namespace Warlord.Gameplay.Match
{
    /// <summary>Отсчёт времени матча. Отдельная система, потому что таймер должен идти первым в такте.</summary>
    public sealed class MatchClockSystem : IServerSystem
    {
        private readonly MatchClock _clock;
        private readonly IMatchContext _context;

        public MatchClockSystem(IMatchContext context, MatchClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public int Order => ServerSystemOrder.MatchClock;

        public void Tick(float deltaTime)
        {
            if (_context.Phase == MatchPhase.Running)
                _clock.Tick(deltaTime);
        }
    }
}
