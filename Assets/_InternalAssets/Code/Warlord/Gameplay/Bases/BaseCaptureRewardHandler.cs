using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Bases
{
    /// <summary>
    /// Захват базы (ГДД §10): золото жертвы уходит захватчику, жертва выбывает,
    /// крепость становится нейтральной руиной и не даёт захватчику точку спавна.
    /// </summary>
    public sealed class BaseCaptureRewardHandler : ICaptureRewardHandler
    {
        private readonly IMatchContext _context;

        public BaseCaptureRewardHandler(IMatchContext context) => _context = context;

        public CapturePointKind Kind => CapturePointKind.BaseFlag;

        public void OnCaptured(CapturePointBehaviour point, int newOwnerSlot)
        {
            int victimSlot = point.ZoneOwnerSlot;

            // Владелец добил свою же шкалу обратно — это не захват.
            if (victimSlot == newOwnerSlot || !PlayerSlots.IsValid(victimSlot))
                return;

            PlayerState victim = _context.Players.Get(victimSlot);
            PlayerState captor = _context.Players.Get(newOwnerSlot);

            if (victim == null || captor == null)
                return;

            // 1. Всё золото жертвы уходит захватчику.
            float fraction = Mathf.Clamp01(_context.Config.GameMode.baseCaptureGoldSteal);
            int stolen = victim.Wallet.DrainGold(fraction);
            captor.Wallet.AddGold(stolen);

            // 2. Постоянная прибавка к доходу за каждую захваченную базу.
            captor.ServerAddCapturedBase();
            _context.Scores.AddBaseCapture(newOwnerSlot);

            // 3. Жертва выбывает, её счёт остаётся в таблице.
            victim.ServerEliminate(EliminationReason.BaseCaptured);

            // 4. Крепость становится руиной: точка больше никому не принадлежит.
            point.ServerForceNeutral();

            _context.Events.RaiseBaseCaptured(victimSlot, newOwnerSlot);
        }

        public void OnOwnershipLost(CapturePointBehaviour point, int previousOwnerSlot)
        {
            // Промежуточное состояние: шкалу хозяина сбили, но базу ещё не взяли.
            // Отдельной награды здесь нет — важен только момент полного захвата.
        }
    }
}
