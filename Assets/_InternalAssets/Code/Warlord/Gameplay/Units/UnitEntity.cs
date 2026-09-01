using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Domain.Formations;
using Warlord.Domain.Stats;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Сетевое представление юнита. Намеренно лёгкое (ГДД §12): здесь только владение,
    /// здоровье и тип. Позиция едет через <see cref="UnitTransformSync"/>, вся боевая логика
    /// живёт на сервере в полях, которые вообще не реплицируются.
    /// </summary>
    public sealed class UnitEntity : NetworkBehaviour, ICombatTarget, IFormationMember
    {
        [Header("Данные")]
        [SerializeField] private UnitConfig config;

        [Tooltip("Радиус тела: дальность атаки считается до края, а не до центра.")]
        [SerializeField] private float bodyRadius = 0.5f;

        [Header("Компоненты")]
        [SerializeField] private MonoBehaviour locomotionSource;

        private readonly SyncVar<byte> _ownerSlot = new();
        private readonly SyncVar<byte> _rosterIndex = new();
        private readonly SyncVar<int> _health = new();

        private IUnitLocomotion _locomotion;
        private IMatchContext _context;
        private ArmyStatsCache _statsCache;
        private int _cachedStatsVersion = -1;
        private UnitStats _stats;

        // --- Серверное состояние. Никогда не реплицируется. ---
        private ICombatTarget _currentTarget;
        private ICombatTarget _lastAttacker;
        private float _attackCooldown;
        private float _targetSwitchCooldown;
        private float _retaliationTimer;
        private float _repathTimer;
        private Vector3 _formationSlot;
        private int _formationSlotIndex = -1;

        public UnitConfig Config => config;
        public IUnitLocomotion Locomotion => _locomotion;
        public Vector3 FormationSlot => _formationSlot;
        public int FormationSlotIndex => _formationSlotIndex;
        public ICombatTarget CurrentTarget => _currentTarget;
        public ICombatTarget LastAttacker => _retaliationTimer > 0f ? _lastAttacker : null;
        public float AttackCooldown => _attackCooldown;
        public float RepathTimer => _repathTimer;

        /// <summary>Актуальные статы с учётом древа прокачки. Пересчитываются лениво при смене версии кэша.</summary>
        public UnitStats Stats
        {
            get
            {
                if (_statsCache != null && _cachedStatsVersion != _statsCache.Version)
                    RefreshStats();

                return _stats;
            }
        }

        #region ICombatTarget

        public int OwnerSlot => _ownerSlot.Value;
        public CombatantKind Kind => CombatantKind.Unit;
        public int UnitTypeIndex => _rosterIndex.Value;
        public bool IsAlive => _health.Value > 0;
        public Vector3 Position => transform.position;
        public float Radius => bodyRadius;
        public int Armor => Stats.Armor;
        public int Health => _health.Value;
        public int MaxHealth => Stats.MaxHealth;

        #endregion

        #region IFormationMember

        public int FormationPriority => config != null ? config.formationSlotPriority : 0;
        public int StableId => (int)ObjectId;

        public void AssignFormationSlot(int slotIndex, Vector3 worldPosition)
        {
            _formationSlotIndex = slotIndex;
            _formationSlot = worldPosition;
        }

        #endregion

        private void Awake()
        {
            _locomotion = locomotionSource as IUnitLocomotion;
            if (_locomotion == null)
                _locomotion = GetComponent<IUnitLocomotion>();
        }

        /// <summary>
        /// Вызывается фабрикой сразу после спавна на сервере. Всё, что нужно юниту для жизни,
        /// приходит снаружи — сам он ничего не ищет глобально.
        /// </summary>
        public void ServerInitialize(IMatchContext context, int ownerSlot, int rosterIndex, ArmyStatsCache statsCache)
        {
            _context = context;
            _statsCache = statsCache;

            _ownerSlot.Value = (byte)Mathf.Clamp(ownerSlot, 0, byte.MaxValue);
            _rosterIndex.Value = (byte)Mathf.Clamp(rosterIndex, 0, byte.MaxValue);

            RefreshStats();
            _health.Value = _stats.MaxHealth;

            _formationSlot = transform.position;
            _locomotion?.SetActive(true);
            _locomotion?.Configure(_stats.MoveSpeed, _stats.TurnSpeed);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _context?.Targeting.Register(this);
            _context?.LagCompensation.Track(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _context?.Targeting.Unregister(this);
            _context?.LagCompensation.Untrack(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Навигация считается только сервером, на клиенте агент только мешал бы интерполяции.
            if (!IsServerInitialized)
                _locomotion?.SetActive(false);
        }

        public void ReceiveDamage(int amount, ICombatTarget source)
        {
            if (!IsServerInitialized || amount <= 0 || !IsAlive)
                return;

            _health.Value = Mathf.Max(0, _health.Value - amount);

            if (source != null && PlayerSlots.AreEnemies(source.OwnerSlot, OwnerSlot))
            {
                _lastAttacker = source;
                _retaliationTimer = _context != null ? _context.Config.Command.retaliationMemory : 3f;
            }
        }

        /// <summary>Лечение в радиусе своей базы (ГДД §5.1). Только сервер.</summary>
        public void ServerHeal(float amount)
        {
            if (!IsServerInitialized || !IsAlive || amount <= 0f)
                return;

            _health.Value = Mathf.Min(Stats.MaxHealth, _health.Value + Mathf.CeilToInt(amount));
        }

        /// <summary>Серверный тик таймеров. Вызывается <see cref="UnitAiSystem"/> до принятия решений.</summary>
        public void ServerTickTimers(float deltaTime)
        {
            if (_attackCooldown > 0f)
                _attackCooldown -= deltaTime;
            if (_targetSwitchCooldown > 0f)
                _targetSwitchCooldown -= deltaTime;
            if (_retaliationTimer > 0f)
                _retaliationTimer -= deltaTime;
            if (_repathTimer > 0f)
                _repathTimer -= deltaTime;
        }

        public bool CanSwitchTarget => _targetSwitchCooldown <= 0f;
        public bool CanAttack => _attackCooldown <= 0f;

        public void SetTarget(ICombatTarget target, float switchCooldown)
        {
            _currentTarget = target;
            _targetSwitchCooldown = switchCooldown;
        }

        public void ClearTargetIfDead()
        {
            if (_currentTarget != null && !_currentTarget.IsAlive)
                _currentTarget = null;
        }

        public void ConsumeAttackCooldown() => _attackCooldown = Stats.AttackInterval;

        /// <summary>Возвращает true, если пора пересчитать путь. Дросселирует SetDestination (ГДД §15, repathInterval).</summary>
        public bool TryConsumeRepath(float interval)
        {
            if (_repathTimer > 0f)
                return false;

            _repathTimer = interval;
            return true;
        }

        private void RefreshStats()
        {
            if (_statsCache == null)
            {
                _stats = default;
                return;
            }

            int previousMax = _stats.MaxHealth;
            _stats = _statsCache.Get(_rosterIndex.Value);
            _cachedStatsVersion = _statsCache.Version;

            _locomotion?.Configure(_stats.MoveSpeed, _stats.TurnSpeed);

            // Прокачка здоровья применяется мгновенно ко всем живым (ГДД §11): добираем дельту,
            // а не масштабируем — иначе раненый юнит лечился бы покупкой перка.
            if (IsServerInitialized && previousMax > 0 && _stats.MaxHealth > previousMax && IsAlive)
                _health.Value += _stats.MaxHealth - previousMax;
        }
    }
}
