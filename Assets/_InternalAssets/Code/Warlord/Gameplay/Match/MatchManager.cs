using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.Match
{
    /// <summary>
    /// Оркестратор матча. Единственная статическая точка входа во всём проекте:
    /// сетевые объекты, созданные из префабов, не могут получить зависимости иначе.
    /// Всё остальное разрешается через <see cref="IMatchContext"/>.
    /// </summary>
    public sealed partial class MatchManager : NetworkBehaviour
    {
        [Header("Конфигурация")]
        [SerializeField] private GameConfig config;

        private readonly SyncVar<byte> _phase = new();
        private readonly SyncVar<MatchSettings> _settings = new();
        private readonly SyncVar<uint> _matchEndTick = new();

        private FixedStepAccumulator _accumulator;
        private MatchClock _clock;
        private VictorySystem _victory;
        private bool _tickSubscribed;
        private float _countdownRemaining;

        /// <summary>Единственный статический доступ. На клиенте даёт конфиг и шину событий.</summary>
        public static MatchManager Instance { get; private set; }

        public GameConfig Config => config;

        /// <summary>Шина событий. Существует и на сервере, и на клиенте.</summary>
        public MatchEvents Events { get; } = new();

        /// <summary>Серверный контекст. На чистом клиенте всегда null.</summary>
        public MatchContext ServerContext { get; private set; }

        public MatchPhase Phase => (MatchPhase)_phase.Value;

        /// <summary>Оставшийся обратный отсчёт до старта, с. Значим только на сервере и хосте.</summary>
        public float CountdownRemaining => _countdownRemaining;
        public MatchSettings Settings => _settings.Value;

        /// <summary>Оставшееся время матча, с. Считается локально от тика окончания — трафика не требует.</summary>
        public float RemainingSeconds
        {
            get
            {
                if (Phase != MatchPhase.Running || _matchEndTick.Value == 0u)
                    return _settings.Value.MatchDuration;

                long ticksLeft = (long)_matchEndTick.Value - TimeManager.Tick;
                return ticksLeft <= 0L ? 0f : (float)(ticksLeft * TimeManager.TickDelta);
            }
        }

        private void Awake()
        {
            Instance = this;
            _phase.OnChange += OnPhaseSyncChanged;
        }

        private void OnDestroy()
        {
            _phase.OnChange -= OnPhaseSyncChanged;

            if (Instance == this)
                Instance = null;
        }

        private void OnPhaseSyncChanged(byte previous, byte next, bool asServer)
        {
            // Событие поднимается ровно один раз: на сервере его уже подняла смена фазы
            // в контексте, поэтому здесь обрабатываем только чистого клиента.
            if (!asServer && !IsServerInitialized)
                Events.RaisePhaseChanged((MatchPhase)next);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!config.Validate(out string error))
            {
                Debug.LogError(error, this);
                return;
            }

            _settings.Value = MatchSettings.FromConfig(config.GameMode);
            _phase.Value = (byte)MatchPhase.Lobby;

            TimeManager.OnTick += ServerOnTick;
            _tickSubscribed = true;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (_tickSubscribed)
                TimeManager.OnTick -= ServerOnTick;

            _tickSubscribed = false;
            TeardownMatch();
        }

        /// <summary>Старт матча с настройками комнаты. Вызывается лобби на сервере.</summary>
        public void ServerStartMatch(in MatchSettings requestedSettings)
        {
            if (!IsServerInitialized || Phase == MatchPhase.Running)
                return;

            MatchSettings sanitized = requestedSettings.Sanitized(config.GameMode);
            _settings.Value = sanitized;

            BuildMatch(in sanitized);
            _clock.Reset();

            // Обратный отсчёт нужен, чтобы игроки успели загрузиться и увидеть карту
            // до того, как начнут капать доход и время удержания.
            _countdownRemaining = config.GameMode.countdownDuration;

            if (_countdownRemaining > 0f)
                SetPhase(MatchPhase.Countdown);
            else
                BeginRunning();
        }

        private void BeginRunning()
        {
            _matchEndTick.Value = TimeManager.Tick + (uint)Mathf.CeilToInt(Settings.MatchDuration / (float)TimeManager.TickDelta);
            SetPhase(MatchPhase.Running);
        }

        private void ServerOnTick()
        {
            if (ServerContext == null)
                return;

            if (Phase == MatchPhase.Countdown)
            {
                _countdownRemaining -= (float)TimeManager.TickDelta;
                if (_countdownRemaining <= 0f)
                    BeginRunning();
            }

            int steps = _accumulator.Consume((float)TimeManager.TickDelta);

            for (int i = 0; i < steps; i++)
            {
                float delta = _accumulator.StepDuration;

                ServerContext.AdvanceTime(delta);

                // Снимок позиций до симуляции: лаг-компенсация должна видеть то состояние,
                // которое клиент наблюдал в момент своего удара.
                ServerContext.LagCompensation.CaptureSnapshot(ServerContext.ServerTime);

                ServerContext.Systems.Tick(delta);
            }
        }

        private void SetPhase(MatchPhase phase)
        {
            _phase.Value = (byte)phase;
            ServerContext?.SetPhase(phase);
        }
    }
}
