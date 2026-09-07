using FishNet.Managing;
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
    /// Composition root серверной части матча: здесь и только здесь создаются все сервисы
    /// и связываются друг с другом. Дальше они передаются по ссылке, поэтому в игровом коде
    /// нет ни одного глобального поиска зависимостей.
    /// </summary>
    public sealed class MatchContext : IMatchContext
    {
        public MatchContext(GameConfig config, in MatchSettings settings, NetworkManager networkManager, MatchEvents events)
        {
            Config = config;
            Settings = settings;
            Events = events;

            int slotCount = settings.SlotCount;

            Scores = new ScoreBoard(slotCount);
            Players = new PlayerRegistry(slotCount);
            Targeting = new TargetingService(slotCount);
            Damage = new DamageQueue();
            Projectiles = new ProjectileSystem(Damage, Targeting);
            LagCompensation = new SnapshotLagCompensator(config.Network);
            Systems = new ServerSystemScheduler();

            Units = new UnitFactory(networkManager, config.Roster);
            Units.BindContext(this);

            CombatTickDelta = config.Network.CombatTickDelta;
            Phase = MatchPhase.Lobby;
        }

        public GameConfig Config { get; }
        public MatchSettings Settings { get; }
        public MatchPhase Phase { get; private set; }

        public MatchEvents Events { get; }
        public ScoreBoard Scores { get; }

        public PlayerRegistry Players { get; }
        public UnitFactory Units { get; }

        public TargetingService Targeting { get; }
        public DamageQueue Damage { get; }
        public ProjectileSystem Projectiles { get; }
        public ILagCompensator LagCompensation { get; }

        public ServerSystemScheduler Systems { get; }

        public float CombatTickDelta { get; }
        public float ServerTime { get; private set; }

        /// <summary>Устанавливается менеджером после создания системы захвата.</summary>
        public CaptureSystem Capture { get; private set; }

        public int CentralFlagOwner => Capture != null ? Capture.CentralFlagOwner : PlayerSlots.None;

        public void BindCapture(CaptureSystem capture) => Capture = capture;

        public void SetPhase(MatchPhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            Events.RaisePhaseChanged(phase);
        }

        public void AdvanceTime(float deltaTime) => ServerTime += deltaTime;

        public void Reset()
        {
            Systems.Clear();
            Targeting.Clear();
            Damage.Clear();
            Projectiles.Clear();
            LagCompensation.Clear();
            Scores.Reset();
            Capture?.Clear();
            ServerTime = 0f;
        }
    }
}
