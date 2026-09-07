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

        /// <summary>
        /// Точка откатывается из-за пустого гарнизона. Отдельно от <see cref="Status"/>:
        /// владелец у неё прежний и статус остаётся Owned, но кольцо в мире обязано
        /// пульсировать и убывать (ГДД §2.7) — иначе игрок узнаёт о проблеме постфактум.
        /// </summary>
        public bool Decaying { get; private set; }

        /// <summary>Точка перешла к новому владельцу. Аргумент — слот нового владельца.</summary>
        public event Action<int> Captured;

        /// <summary>Владелец потерял точку, она стала нейтральной. Аргумент — слот бывшего владельца.</summary>
        public event Action<int> OwnershipLost;

        /// <param name="ownerHasGarrison">
        /// Есть ли у владельца живой юнит рядом с точкой. Значим только для аванпостов
        /// с <see cref="CapturePointConfig.garrisonDecay"/>: без гарнизона такая точка
        /// медленно откатывается сама (ГДД §2.4).
        /// </param>
        public void Tick(float deltaTime, CaptureOccupancy occupancy, bool ownerHasGarrison = true)
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
                TickUnoccupied(deltaTime, ownerHasGarrison);
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
            Decaying = false;
        }

        private void TickUnoccupied(float deltaTime, bool ownerHasGarrison)
        {
            float decay = _config.decayRatePerSecond * deltaTime;

            if (ChallengerProgress > 0f)
            {
                ChallengerProgress -= decay;
                if (ChallengerProgress <= 0f)
                    ResetChallenger();
            }

            if (!PlayerSlots.IsValid(OwnerSlot))
            {
                Status = ChallengerProgress > 0f ? CaptureStatus.Capturing : CaptureStatus.Neutral;
                return;
            }

            // Аванпост без гарнизона откатывается сам (ГДД §2.4). Это и есть весь механизм
            // против снежного кома: удержание четырёх точек требует четырёх гарнизонов,
            // а они съедают тот же лимит, из которого собирается полевая армия.
            //
            // Откат считается только здесь, в пустой зоне: пока точку сбивает чужой полководец,
            // работает decapture, и складывать с ним ещё и откат значило бы наказывать дважды.
            if (_config.garrisonDecay && !ownerHasGarrison)
            {
                Decaying = true;
                OwnerProgress -= _config.garrisonDecayRatePerSecond * deltaTime;

                if (OwnerProgress <= 0f)
                {
                    OwnerProgress = 0f;
                    int previousOwner = OwnerSlot;
                    OwnerSlot = PlayerSlots.None;
                    ResetChallenger();
                    Status = CaptureStatus.Neutral;
                    Decaying = false;
                    OwnershipLost?.Invoke(previousOwner);
                    return;
                }

                Status = CaptureStatus.Owned;
                return;
            }

            Decaying = false;

            // Завершённая шкала (100%) не спадает: точка остаётся у владельца до перезахвата (ГДД §9.4).
            // Недобитая шкала владельца, наоборот, восстанавливается — базу должно быть легко отбить (ГДД §10).
            if (OwnerProgress < 1f && _config.ownerBarRecoversWhenEmpty)
                OwnerProgress = Mathf.Min(1f, OwnerProgress + decay);

            Status = CaptureStatus.Owned;
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
