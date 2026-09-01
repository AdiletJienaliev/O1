using System;
using Warlord.Core;
using Warlord.Domain.Match;

namespace Warlord.Gameplay.Match
{
    /// <summary>
    /// Шина событий матча. Единственный канал, через который презентация (HUD, звук, VFX)
    /// узнаёт о происходящем: игровая логика не знает о существовании UI.
    /// Работает и на сервере, и на клиенте — каждый подписывается на своей стороне.
    /// </summary>
    public sealed class MatchEvents
    {
        public event Action<MatchPhase> PhaseChanged;
        public event Action<MatchOutcome> MatchFinished;

        /// <summary>Слот игрока, вошедшего в матч.</summary>
        public event Action<int> PlayerJoined;

        /// <summary>Слот выбывшего игрока и причина.</summary>
        public event Action<int, EliminationReason> PlayerEliminated;

        /// <summary>Смена владельца центрального флага: прежний слот, новый слот.</summary>
        public event Action<int, int> CentralFlagOwnerChanged;

        /// <summary>База захвачена: слот жертвы, слот захватчика.</summary>
        public event Action<int, int> BaseCaptured;

        /// <summary>Полководец погиб: слот погибшего, слот убийцы.</summary>
        public event Action<int, int> HeroKilled;

        /// <summary>Полководец возродился.</summary>
        public event Action<int> HeroRespawned;

        /// <summary>Юнит погиб: слот владельца, слот убийцы.</summary>
        public event Action<int, int> UnitKilled;

        /// <summary>Серверная валидация отклонила команду игрока. Слот и причина.</summary>
        public event Action<int, CommandRejection> CommandRejected;

        public void RaisePhaseChanged(MatchPhase phase) => PhaseChanged?.Invoke(phase);
        public void RaiseMatchFinished(in MatchOutcome outcome) => MatchFinished?.Invoke(outcome);
        public void RaisePlayerJoined(int slot) => PlayerJoined?.Invoke(slot);
        public void RaisePlayerEliminated(int slot, EliminationReason reason) => PlayerEliminated?.Invoke(slot, reason);
        public void RaiseCentralFlagOwnerChanged(int previous, int next) => CentralFlagOwnerChanged?.Invoke(previous, next);
        public void RaiseBaseCaptured(int victimSlot, int captorSlot) => BaseCaptured?.Invoke(victimSlot, captorSlot);
        public void RaiseHeroKilled(int victimSlot, int killerSlot) => HeroKilled?.Invoke(victimSlot, killerSlot);
        public void RaiseHeroRespawned(int slot) => HeroRespawned?.Invoke(slot);
        public void RaiseUnitKilled(int ownerSlot, int killerSlot) => UnitKilled?.Invoke(ownerSlot, killerSlot);
        public void RaiseCommandRejected(int slot, CommandRejection reason) => CommandRejected?.Invoke(slot, reason);
    }
}
