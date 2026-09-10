using Warlord.Core;

namespace Warlord.Gameplay.Capture
{
    /// <summary>
    /// Что происходит при смене владельца точки. Отделено от самой точки,
    /// потому что центральный флаг даёт доход, а флаг базы выбивает игрока из матча —
    /// это разные правила поверх одной механики.
    /// </summary>
    public interface ICaptureRewardHandler
    {
        CapturePointKind Kind { get; }

        void OnCaptured(CapturePointBehaviour point, int newOwnerSlot);

        void OnOwnershipLost(CapturePointBehaviour point, int previousOwnerSlot);
    }
}
