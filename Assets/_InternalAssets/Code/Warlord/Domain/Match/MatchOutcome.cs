using Warlord.Core;

namespace Warlord.Domain.Match
{
    /// <summary>Как именно закончился матч. Нужен UI для правильного экрана результатов.</summary>
    public enum MatchEndReason : byte
    {
        None = 0,
        TimeExpired = 1,
        LastPlayerStanding = 2,
        HostLeft = 3
    }

    /// <summary>Результат матча: победитель и причина завершения.</summary>
    public readonly struct MatchOutcome
    {
        public readonly int WinnerSlot;
        public readonly MatchEndReason Reason;

        public MatchOutcome(int winnerSlot, MatchEndReason reason)
        {
            WinnerSlot = winnerSlot;
            Reason = reason;
        }

        public bool HasWinner => PlayerSlots.IsValid(WinnerSlot);

        public static MatchOutcome None => new(PlayerSlots.None, MatchEndReason.None);
    }
}
