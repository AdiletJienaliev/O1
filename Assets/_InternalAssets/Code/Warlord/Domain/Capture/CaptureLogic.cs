using System;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;

namespace Warlord.Domain.Capture
{
    /// <summary>
    /// Модель двух шкал (ГДД §9): чтобы захватить занятую точку, надо сначала обнулить
    /// шкалу текущего владельца, и только потом набирать свою.
    /// Чистая логика без сцены — тикает сервер, результат синкается наружу.
    /// </summary>
    public sealed class CaptureLogic
    {
        private readonly CapturePointConfig _config;

        public CaptureLogic(CapturePointConfig config, int initialOwnerSlot)
        {
            _config = config;
            OwnerSlot = initialOwnerSlot;
            OwnerProgress = PlayerSlots.IsValid(initialOwnerSlot) ? 1f : 0f;
            ChallengerSlot = PlayerSlots.None;
            Status = PlayerSlots.IsValid(initialOwnerSlot) ? CaptureStatus.Owned : CaptureStatus.Neutral;
        }

        /// <summary>Владелец точки или <see cref="PlayerSlots.None"/>.</summary>
        public int OwnerSlot { get; private set; }

        /// <summary>Шкала владельца, 0..1. Ниже 1 — точку сбивают.</summary>
        public float OwnerProgress { get; private set; }

        /// <summary>Кто набирает свою шкалу на нейтральной точке.</summary>
        public int ChallengerSlot { get; private set; }

        /// <summary>Шкала претендента, 0..1.</summary>
        public float ChallengerProgress { get; private set; }

        public CaptureStatus Status { get; private set; }

        /// <summary>Точка перешла к новому владельцу. Аргумент — слот нового владельца.</summary>
        public event Action<int> Captured;

        /// <summary>Владелец потерял точку, она стала нейтральной. Аргумент — слот бывшего владельца.</summary>
        public event Action<int> OwnershipLost;

        public void Tick(float deltaTime, CaptureOccupancy occupancy)
        {
            // 1. Двое и более разных игроков в зоне — всё заморожено (ГДД §9.2).
            if (occupancy.DistinctSlots >= 2)
            {
                Status = CaptureStatus.Contested;
                return;
            }

            // 2. В зоне никого — работает только спад.
            if (occupancy.DistinctSlots == 0)
            {
                TickUnoccupied(deltaTime);
                return;
            }

            int slot = occupancy.SoleSlot;
            float stack = StackMultiplier(occupancy.CountFor(slot));

            // 3a. Чужой полководец сбивает шкалу владельца.
            if (PlayerSlots.IsValid(OwnerSlot) && OwnerSlot != slot)
            {
                Status = CaptureStatus.Losing;
                OwnerProgress -= _config.decaptureRatePerSecond * stack * deltaTime;

                if (OwnerProgress <= 0f)
                {
                    OwnerProgress = 0f;
                    int previousOwner = OwnerSlot;
                    OwnerSlot = PlayerSlots.None;
                    Status = CaptureStatus.Neutral;
                    OwnershipLost?.Invoke(previousOwner);
                }

                // Свою шкалу претендент начнёт набирать следующим тактом, уже на нейтральной точке.
                return;
            }

            // 3b. Владелец стоит на своей точке — добивает шкалу обратно до полной.
            if (OwnerSlot == slot)
            {
                OwnerProgress = Mathf.Min(1f, OwnerProgress + _config.captureRatePerSecond * stack * deltaTime);
                ResetChallenger();
                Status = CaptureStatus.Owned;
                return;
            }

            // 3c. Точка нейтральная — претендент набирает свою шкалу.
            if (ChallengerSlot != slot)
            {
                ChallengerSlot = slot;
                ChallengerProgress = 0f;
            }

            ChallengerProgress += _config.captureRatePerSecond * stack * deltaTime;
            Status = CaptureStatus.Capturing;

            if (ChallengerProgress >= 1f)
            {
                OwnerSlot = slot;
                OwnerProgress = 1f;
                ResetChallenger();
                Status = CaptureStatus.Owned;
                Captured?.Invoke(slot);
            }
        }

        /// <summary>Принудительный сброс — используется при выбывании владельца.</summary>
        public void ForceNeutral()
        {
            OwnerSlot = PlayerSlots.None;
            OwnerProgress = 0f;
            ResetChallenger();
            Status = CaptureStatus.Neutral;
        }

        private void TickUnoccupied(float deltaTime)
        {
            float decay = _config.decayRatePerSecond * deltaTime;

            if (ChallengerProgress > 0f)
            {
                ChallengerProgress -= decay;
                if (ChallengerProgress <= 0f)
                    ResetChallenger();
            }

            // Завершённая шкала (100%) не спадает: точка остаётся у владельца до перезахвата (ГДД §9.4).
            // Недобитая шкала владельца, наоборот, восстанавливается — базу должно быть легко отбить (ГДД §10).
            if (PlayerSlots.IsValid(OwnerSlot) && OwnerProgress < 1f && _config.ownerBarRecoversWhenEmpty)
                OwnerProgress = Mathf.Min(1f, OwnerProgress + decay);

            if (PlayerSlots.IsValid(OwnerSlot))
                Status = CaptureStatus.Owned;
            else
                Status = ChallengerProgress > 0f ? CaptureStatus.Capturing : CaptureStatus.Neutral;
        }

        private float StackMultiplier(int playersInZone)
        {
            int extra = Mathf.Max(0, playersInZone - 1);
            return 1f + _config.stackMultiplierPerExtraPlayer * extra;
        }

        private void ResetChallenger()
        {
            ChallengerSlot = PlayerSlots.None;
            ChallengerProgress = 0f;
        }
    }
}
