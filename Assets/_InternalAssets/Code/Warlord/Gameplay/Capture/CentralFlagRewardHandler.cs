using Warlord.Core;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Награда за центральный флаг (ГДД §9). Сам доход и XP начисляет
    /// <see cref="Warlord.Gameplay.Economy.EconomySystem"/> — здесь только смена владельца
    /// и счётчик захватов для тай-брейка.
    /// </summary>
    public sealed class CentralFlagRewardHandler : ICaptureRewardHandler
    {
        private readonly IMatchContext _context;

        public CentralFlagRewardHandler(IMatchContext context) => _context = context;

        public CapturePointKind Kind => CapturePointKind.CentralFlag;

        public void OnCaptured(CapturePointBehaviour point, int newOwnerSlot)
        {
            _context.Scores.AddFlagCapture(newOwnerSlot);
            _context.Events.RaiseCentralFlagOwnerChanged(PlayerSlots.None, newOwnerSlot);
        }

        public void OnOwnershipLost(CapturePointBehaviour point, int previousOwnerSlot)
        {
            // Владелец теряет бонус дохода немедленно (ГДД §9.3) — это происходит само,
            // потому что доход каждый такт пересчитывается от текущего владельца точки.
            _context.Events.RaiseCentralFlagOwnerChanged(previousOwnerSlot, PlayerSlots.None);
        }
    }
}
