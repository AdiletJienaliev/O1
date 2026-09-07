namespace Warlord.Core
{
    /// <summary>
    /// Серверная система, исполняемая в едином боевом такте (ГДД §12).
    /// Системы не знают друг о друге: порядок задаётся <see cref="Order"/>,
    /// зависимости приходят через конструктор из composition root.
    /// </summary>
    public interface IServerSystem
    {
        /// <summary>Порядок исполнения внутри такта. Меньше — раньше. См. <see cref="ServerSystemOrder"/>.</summary>
        int Order { get; }

        /// <param name="deltaTime">Всегда равен длительности боевого такта (1 / combatTickRate).</param>
        void Tick(float deltaTime);
    }

    /// <summary>Опциональный жизненный цикл системы. Реализуется только теми, кому он нужен.</summary>
    public interface IServerSystemLifecycle
    {
        void OnMatchStarted();
        void OnMatchFinished();
    }

    /// <summary>
    /// Единая таблица порядка систем. Держим все значения в одном месте, чтобы
    /// «кто раньше кого» читалось без раскопок по проекту.
    /// </summary>
    public static class ServerSystemOrder
    {
        public const int MatchClock = 0;
        public const int Capture = 100;
        public const int Economy = 200;
        public const int SpawnQueue = 300;

        /// <summary>Кого бьёт армия целиком. Считается до строя: приказ может смениться сам.</summary>
        public const int ArmyEngagement = 350;

        public const int Formation = 400;
        public const int UnitAi = 500;

        /// <summary>
        /// Охранники. Отдельно от <see cref="UnitAi"/>, потому что они не в армии и приказов
        /// не получают: у них своя точка, свой слот и свой поводок (ГДД §1.4).
        /// </summary>
        public const int Garrison = 520;
        public const int HeroCombat = 550;
        public const int Projectiles = 600;

        /// <summary>Урон применяется в самом конце такта — это даёт взаимную смерть без преимущества по пингу.</summary>
        public const int CombatResolution = 700;

        public const int Regeneration = 800;
        public const int Victory = 900;
    }
}
