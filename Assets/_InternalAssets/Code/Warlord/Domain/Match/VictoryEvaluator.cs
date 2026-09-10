using System.Collections.Generic;
using Warlord.Core;

namespace Warlord.Domain.Match
{
    /// <summary>
    /// Определение победителя (ГДД §2). Чистая функция от счёта и списка живых игроков —
    /// её можно прогнать в юнит-тестах, не поднимая сеть.
    ///
    /// Победа считается стороной, а не слотом: в FFA сторона состоит из одного игрока,
    /// поэтому обычный матч проходит ровно тем же кодом, что и командный. Наружу всё
    /// равно уходит слот — по нему UI берёт цвет и имя, — но выбирается он уже внутри
    /// выигравшей стороны.
    /// </summary>
    public sealed class VictoryEvaluator
    {
        private readonly ScoreBoard _scoreBoard;
        private readonly TiebreakRule[] _tiebreakOrder;
        private readonly TeamLayout _teams;

        public VictoryEvaluator(ScoreBoard scoreBoard, TiebreakRule[] tiebreakOrder, TeamLayout teams = default)
        {
            _scoreBoard = scoreBoard;
            _teams = teams;
            _tiebreakOrder = tiebreakOrder != null && tiebreakOrder.Length > 0
                ? tiebreakOrder
                : new[] { TiebreakRule.FlagHoldTime, TiebreakRule.FlagCaptures, TiebreakRule.BasesCaptured, TiebreakRule.UnitKills };
        }

        /// <summary>
        /// Досрочная победа: в матче осталась одна сторона. В FFA это привычный
        /// «последний выживший», в командном — выбитая насухо вторая команда.
        /// </summary>
        public bool TryGetEarlyWinner(IReadOnlyList<int> aliveSlots, out MatchOutcome outcome)
        {
            outcome = MatchOutcome.None;

            if (aliveSlots == null || aliveSlots.Count == 0)
                return false;

            for (int i = 1; i < aliveSlots.Count; i++)
            {
                if (!_teams.SameSide(aliveSlots[0], aliveSlots[i]))
                    return false;
            }

            outcome = new MatchOutcome(BestOf(aliveSlots), MatchEndReason.LastPlayerStanding);
            return true;
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
                    if (best == PlayerSlots.None || CompareSides(slot, best, participatingSlots) > 0)
                        best = slot;
                }
            }

            return new MatchOutcome(best, MatchEndReason.TimeExpired);
        }

        /// <summary>Положительное значение — левый слот лучше правого. Сравнение личных счётов.</summary>
        public int Compare(int leftSlot, int rightSlot)
        {
            PlayerScore left = _scoreBoard.Get(leftSlot);
            PlayerScore right = _scoreBoard.Get(rightSlot);

            return Compare(in left, in right, leftSlot, rightSlot);
        }

        /// <summary>
        /// Сравнение по вкладу сторон: сначала суммарный счёт команды, и лишь при равенстве —
        /// личный. Иначе командная победа доставалась бы тому, кто просидел на флаге,
        /// пока союзник держал оборону, а сама команда могла бы проиграть более слабой.
        /// </summary>
        private int CompareSides(int leftSlot, int rightSlot, IReadOnlyList<int> participants)
        {
            if (_teams.SameSide(leftSlot, rightSlot))
                return Compare(leftSlot, rightSlot);

            PlayerScore left = SumSide(leftSlot, participants);
            PlayerScore right = SumSide(rightSlot, participants);

            int byTeam = Compare(in left, in right, leftSlot, rightSlot);
            return byTeam != 0 ? byTeam : Compare(leftSlot, rightSlot);
        }

        private PlayerScore SumSide(int slot, IReadOnlyList<int> participants)
        {
            PlayerScore total = default;

            for (int i = 0; i < participants.Count; i++)
            {
                if (!_teams.SameSide(participants[i], slot))
                    continue;

                PlayerScore score = _scoreBoard.Get(participants[i]);
                total.FlagHoldSeconds += score.FlagHoldSeconds;
                total.FlagCaptures += score.FlagCaptures;
                total.BasesCaptured += score.BasesCaptured;
                total.UnitKills += score.UnitKills;
            }

            return total;
        }

        /// <summary>Лучший слот внутри уже определившейся стороны — он и попадёт в результат матча.</summary>
        private int BestOf(IReadOnlyList<int> slots)
        {
            int best = slots[0];

            for (int i = 1; i < slots.Count; i++)
            {
                if (Compare(slots[i], best) > 0)
                    best = slots[i];
            }

            return best;
        }

        private int Compare(in PlayerScore left, in PlayerScore right, int leftSlot, int rightSlot)
        {
            for (int i = 0; i < _tiebreakOrder.Length; i++)
            {
                int result = CompareBy(_tiebreakOrder[i], in left, in right);
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
