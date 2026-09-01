using System;
using System.Collections.Generic;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Match
{
    /// <summary>
    /// Проверка условий победы (ГДД §2). Стоит последней в такте: к этому моменту
    /// уже посчитаны захваты, выбывания и время удержания за текущий такт.
    /// </summary>
    public sealed class VictorySystem : IServerSystem
    {
        private readonly IMatchContext _context;
        private readonly MatchClock _clock;
        private readonly VictoryEvaluator _evaluator;

        private bool _resolved;

        public VictorySystem(IMatchContext context, MatchClock clock, VictoryEvaluator evaluator)
        {
            _context = context;
            _clock = clock;
            _evaluator = evaluator;
        }

        public int Order => ServerSystemOrder.Victory;

        /// <summary>Матч завершён. Подписчик (MatchManager) переводит фазу и рассылает результат.</summary>
        public event Action<MatchOutcome> MatchResolved;

        public void Tick(float deltaTime)
        {
            if (_resolved || _context.Phase != MatchPhase.Running)
                return;

            IReadOnlyList<int> alive = _context.Players.GetAliveSlots();

            // Досрочная победа: остался один живой игрок.
            if (alive.Count <= 1 && _context.Players.Active.Count > 1
                && _evaluator.TryGetEarlyWinner(alive, out MatchOutcome early))
            {
                Resolve(early);
                return;
            }

            if (_clock.IsExpired)
                Resolve(_evaluator.EvaluateOnTimeExpired(CollectParticipants()));
        }

        public void Reset() => _resolved = false;

        private List<int> CollectParticipants()
        {
            List<int> participants = new(_context.Players.Active.Count);

            for (int i = 0; i < _context.Players.Active.Count; i++)
            {
                PlayerState player = _context.Players.Active[i];
                if (player != null)
                    participants.Add(player.Slot);
            }

            return participants;
        }

        private void Resolve(in MatchOutcome outcome)
        {
            _resolved = true;
            MatchResolved?.Invoke(outcome);
        }
    }
}
