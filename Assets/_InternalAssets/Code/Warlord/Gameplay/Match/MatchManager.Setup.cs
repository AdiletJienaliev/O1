using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Domain.Match;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Bases;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Combat;
using Warlord.Gameplay.Economy;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;
using Warlord.Gameplay.Units.Behaviours;

namespace Warlord.Gameplay.Match
{
    /// <summary>
    /// Сборка серверной симуляции. Вынесена отдельно, потому что это единственное место,
    /// где виден весь состав систем и их порядок — по нему читается вся механика матча.
    /// </summary>
    public sealed partial class MatchManager
    {
        private CombatResolutionSystem _combatResolution;
        private CaptureSystem _capture;

        private void BuildMatch(in MatchSettings settings)
        {
            TeardownMatch();

            ServerContext = new MatchContext(config, in settings, NetworkManager, Events);
            _accumulator = new FixedStepAccumulator(config.Network.CombatTickDelta);
            _clock = new MatchClock(settings.MatchDuration);

            _capture = BuildCaptureSystem();
            ServerContext.BindCapture(_capture);

            // Точки выбывшего игрока должны обнулиться в том же такте, что и его выбывание.
            Events.PlayerEliminated += OnPlayerEliminated;

            _combatResolution = new CombatResolutionSystem(
                ServerContext.Damage,
                new DamageCalculator(config.DamageMatrix));
            _combatResolution.TargetKilled += OnTargetKilled;

            _victory = new VictorySystem(
                ServerContext,
                _clock,
                new VictoryEvaluator(ServerContext.Scores, config.GameMode.winTiebreakOrder));
            _victory.MatchResolved += OnMatchResolved;

            ServerSystemScheduler systems = ServerContext.Systems;
            systems.Register(new MatchClockSystem(ServerContext, _clock));
            systems.Register(_capture);
            systems.Register(new EconomySystem(ServerContext));
            systems.Register(new SpawnQueueSystem(ServerContext));
            systems.Register(new ArmyEngagementSystem(ServerContext));
            systems.Register(new ArmyFormationSystem(ServerContext));
            systems.Register(new UnitAiSystem(ServerContext, UnitOrderBehaviourCatalog.CreateDefault()));
            systems.Register(ServerContext.Projectiles);
            systems.Register(_combatResolution);
            systems.Register(new HeroLifecycleSystem(ServerContext));
            systems.Register(new BaseHealSystem(ServerContext));
            systems.Register(_victory);

            systems.NotifyMatchStarted();
        }

        private CaptureSystem BuildCaptureSystem()
        {
            CaptureSystem capture = new();
            capture.RegisterHandler(new CentralFlagRewardHandler(ServerContext));
            capture.RegisterHandler(new BaseCaptureRewardHandler(ServerContext));

            // Точки лежат в сцене, поэтому собираем их один раз при старте матча:
            // так порядок OnStartServer у сценных объектов перестаёт что-либо значить.
            CapturePointBehaviour[] points = FindObjectsByType<CapturePointBehaviour>(FindObjectsInactive.Exclude);

            for (int i = 0; i < points.Length; i++)
            {
                points[i].ServerInitialize(ServerContext);
                capture.Register(points[i]);
            }

            return capture;
        }

        private void OnPlayerEliminated(int slot, EliminationReason reason) => _capture?.OnPlayerEliminated(slot);

        private void TeardownMatch()
        {
            if (_combatResolution != null)
                _combatResolution.TargetKilled -= OnTargetKilled;

            if (_victory != null)
                _victory.MatchResolved -= OnMatchResolved;

            Events.PlayerEliminated -= OnPlayerEliminated;

            ServerContext?.Reset();

            _combatResolution = null;
            _victory = null;
            _capture = null;
        }
    }
}
