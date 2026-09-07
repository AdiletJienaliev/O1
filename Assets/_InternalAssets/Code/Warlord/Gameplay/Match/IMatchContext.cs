using System.Collections.Generic;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Combat;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;
using Warlord.Networking.LagCompensation;

namespace Warlord.Gameplay.Match
{
    /// <summary>
    /// Всё, что серверная логика знает о матче. Один интерфейс вместо десятка синглтонов:
    /// системы и поведения получают его через конструктор или параметр, никто ничего не ищет
    /// через статические поля. Существует только на сервере.
    /// </summary>
    public interface IMatchContext
    {
        GameConfig Config { get; }
        MatchSettings Settings { get; }
        MatchPhase Phase { get; }

        MatchEvents Events { get; }
        ScoreBoard Scores { get; }

        PlayerRegistry Players { get; }
        UnitFactory Units { get; }

        TargetingService Targeting { get; }
        DamageQueue Damage { get; }
        ProjectileSystem Projectiles { get; }
        ILagCompensator LagCompensation { get; }

        ServerSystemScheduler Systems { get; }

        /// <summary>Длительность одного боевого такта, с.</summary>
        float CombatTickDelta { get; }

        /// <summary>Серверное время в секундах от старта матча. Используется лаг-компенсацией.</summary>
        float ServerTime { get; }

        /// <summary>Слот, владеющий центральным флагом, или <see cref="PlayerSlots.None"/>.</summary>
        int CentralFlagOwner { get; }

        /// <summary>Все точки захвата в сцене. Собираются один раз при старте матча.</summary>
        IReadOnlyList<CapturePointBehaviour> CapturePoints { get; }

        /// <summary>
        /// Пересобрать суммарный эффект улучшений аванпостов игрока (ГДД §2.5).
        /// Вызывается при смене владельца точки и при выборе улучшения.
        /// </summary>
        void RebuildOutpostUpgrades(int slot);
    }
}
