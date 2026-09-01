using System.Collections.Generic;
using Warlord.Core;

namespace Warlord.Domain.Match
{
    /// <summary>
    /// Определение победителя (ГДД §2). Чистая функция от счёта и списка живых игроков —
    /// её можно прогнать в юнит-тестах, не поднимая сеть.
    /// </summary>
    public sealed class VictoryEvaluator
    {
        private readonly ScoreBoard _scoreBoard;
        private readonly TiebreakRule[] _tiebreakOrder;

        public VictoryEvaluator(ScoreBoard scoreBoard, TiebreakRule[] tiebreakOrder)
        {
            _scoreBoard = scoreBoard;
            _tiebreakOrder = tiebreakOrder != null && tiebreakOrder.Length > 0
                ? tiebreakOrder
                : new[] { TiebreakRule.FlagHoldTime, TiebreakRule.FlagCaptures, TiebreakRule.BasesCaptured, TiebreakRule.UnitKills };
        }

        /// <summary>Досрочная победа: в матче остался один живой игрок.</summary>
        public bool TryGetEarlyWinner(IReadOnlyList<int> aliveSlots, out MatchOutcome outcome)
        {
            if (aliveSlots != null && aliveSlots.Count == 1)
            {
                outcome = new MatchOutcome(aliveSlots[0], MatchEndReason.LastPlayerStanding);
                return true;
            }

            outcome = MatchOutcome.None;
            return false;
        }

        /// <summary>Победа по истечении таймера: максимум времени удержания, дальше по тай-брейкам.</summary>
        public MatchOutcome EvaluateOnTimeExpired(IReadOnlyList<int> participatingSlots)
        {
            int best = PlayerSlots.None;

            if (participatingSlots != null)
            {
                for (int i = 0; i < participatingSlots.Count; i++)
                {
                    int slot = participatingSlots[i];
                    if (best == PlayerSlots.None || Compare(slot, best) > 0)
                        best = slot;
                }
            }

            return new MatchOutcome(best, MatchEndReason.TimeExpired);
        }

        /// <summary>Положительное значение — левый слот лучше правого.</summary>
        public int Compare(int leftSlot, int rightSlot)
        {
            PlayerScore left = _scoreBoard.Get(leftSlot);
            PlayerScore right = _scoreBoard.Get(rightSlot);

            for (int i = 0; i < _tiebreakOrder.Length; i++)
            {
                int result = CompareBy(_tiebreakOrder[i], left, right);
                if (result != 0)
                    return result;
            }

            // Полностью равный счёт: стабильный порядок по номеру слота, чтобы не было мигания в UI.
            return rightSlot.CompareTo(leftSlot);
        }

        private static int CompareBy(TiebreakRule rule, in PlayerScore left, in PlayerScore right)
        {
            switch (rule)
            {
                case TiebreakRule.FlagHoldTime: return left.FlagHoldSeconds.CompareTo(right.FlagHoldSeconds);
                case TiebreakRule.FlagCaptures: return left.FlagCaptures.CompareTo(right.FlagCaptures);
                case TiebreakRule.BasesCaptured: return left.BasesCaptured.CompareTo(right.BasesCaptured);
                case TiebreakRule.UnitKills: return left.UnitKills.CompareTo(right.UnitKills);
                default: return 0;
            }
        }
    }
}
