using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Domain.Formations;
using Warlord.Domain.Stats;
using Warlord.Gameplay.Match;
using Warlord.Presentation.Animation;
using Warlord.Presentation.Combat;

namespace Warlord.Gameplay.Units
{
    /// <summary>
    /// Сетевое представление юнита. Намеренно лёгкое (ГДД §12): здесь только владение,
    /// здоровье и тип. Позиция едет через <see cref="UnitTransformSync"/>, вся боевая логика
    /// живёт на сервере в полях, которые вообще не реплицируются.
    /// </summary>
    public sealed class UnitEntity : NetworkBehaviour, ICombatTarget, IShieldedTarget, IFormationMember, IHealthSource
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

        // Поднятый щит виден всем: по нему клиент ставит позу и по нему же игрок понимает,
        // почему его удары не проходят. Меняется только на смене приказа, поэтому надёжным
        // каналом и без интервала — трафика от него нет.
        private readonly SyncVar<bool> _shieldRaised = new();

        // Скорость бега, по которой нормируется параметр run в аниматоре. Синкается, потому что
        // прокачка живёт на сервере, а решать «это шаг или бег» приходится клиенту: без неё
        // разогнанный перком юнит показывал бы бег там, где на самом деле идёт шагом.
        private readonly SyncVar<float> _referenceSpeed = new();

        private IUnitLocomotion _locomotion;
        private CharacterAnimationDriver _animation;
        private ProjectileEmitter _projectiles;
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
        // Обе ссылки переживают такты, поэтому наружу отдаём их только через Exists():
        // цель или обидчик могли быть уничтожены после деспавна, и обычная проверка на null
        // через интерфейс этого не увидит.
        public ICombatTarget CurrentTarget => _currentTarget.OrNull();
        public ICombatTarget LastAttacker => _retaliationTimer > 0f ? _lastAttacker.OrNull() : null;
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

        #region IShieldedTarget

        /// <summary>Щит поднят. Ставится сервером в поведении приказа «Защита».</summary>
        public bool IsBlocking => _shieldRaised.Value && IsAlive;

        public Vector3 BlockFacing => transform.forward;

        public float BlockAngle => Stats.BlockAngle;

        public float BlockDamageFactor => Stats.BlockDamageFactor;

        /// <summary>
        /// Удар пришёл в щит. Обидчика запоминаем так же, как при настоящем попадании:
        /// иначе строй, который весь бой простоял под щитами, после смены приказа не знал бы,
        /// кому отвечать.
        /// </summary>
        public void NotifyBlocked(ICombatTarget attacker)
        {
            if (!IsServerInitialized || !IsAlive)
                return;

            if (attacker.Exists() && PlayerSlots.AreEnemies(attacker.OwnerSlot, OwnerSlot))
            {
                _lastAttacker = attacker;
                _retaliationTimer = _context != null ? _context.Config.Command.retaliationMemory : 3f;
            }

            ObserversPlayBlock();
        }

        /// <summary>Поднять или опустить щит. Только сервер; юнит без щита остаётся открытым.</summary>
        public void ServerSetShield(bool raised)
        {
            if (!IsServerInitialized)
                return;

            bool value = raised && Stats.HasShield;

            if (_shieldRaised.Value != value)
                _shieldRaised.Value = value;
        }

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

            _animation = new CharacterAnimationDriver(gameObject, transform);
            _animation.SetReferenceSpeed(config != null ? config.moveSpeed : 1f);

            _projectiles = GetComponentInChildren<ProjectileEmitter>(true);

            if (_projectiles != null && config != null)
                _projectiles.SetFallbackPrefab(config.projectilePrefab);

            _shieldRaised.OnChange += OnShieldChanged;
        }

        /// <summary>
        /// Анимация юнита. На сервере скорость берётся прямо у навигации — она знает её точно
        /// и меняет плавно. На клиентах агента нет, тело двигает репликация, и остаётся
        /// восстанавливать скорость из смещения трансформа.
        /// </summary>
        private void Update()
        {
            if (!_animation.IsBound)
                return;

            // До инициализации сетью здоровье ещё нулевое, и юнит успевал бы сыграть смерть
            // прямо в кадре появления, а следом воскрешение. Ждём готовности объекта.
            if (!IsSpawned)
                return;

            float delta = Time.deltaTime;

            _animation.SetReferenceSpeed(_referenceSpeed.Value > 0.01f
                ? _referenceSpeed.Value
                : config != null ? config.moveSpeed : 1f);

            _animation.SetAlive(IsAlive);
            _animation.SetGrounded(true);
            _animation.SetDefending(_shieldRaised.Value);

            if (IsServerInitialized && _locomotion != null)
                _animation.Tick(_locomotion.Velocity, delta);
            else
                _animation.TickFromTransform(delta);
        }

        private void OnDestroy() => _shieldRaised.OnChange -= OnShieldChanged;

        /// <summary>
        /// Щит подняли или опустили. Позу ставит <see cref="Update"/> каждый кадр, а здесь
        /// остаётся только событие для звука и вспышки — подписчикам знать про SyncVar незачем.
        /// </summary>
        private void OnShieldChanged(bool previous, bool current, bool asServer)
        {
            if (previous != current)
                ShieldToggled?.Invoke(current);
        }

        /// <summary>Щит подняли (true) или опустили (false). Для звука и VFX на клиентах.</summary>
        public event System.Action<bool> ShieldToggled;

        /// <summary>Удар принят на щит: искры, звон, дрогнувшая поза. Урона за этим нет.</summary>
        public event System.Action BlockPerformed;

        /// <summary>Ненадёжным каналом: потерянный звон щита — это пропущенный эффект, а не рассинхрон.</summary>
        [ObserversRpc]
        private void ObserversPlayBlock(Channel channel = Channel.Unreliable)
        {
            _animation.PlayBlockImpact();
            BlockPerformed?.Invoke();
        }

        /// <summary>Замах юнита: анимация, звук, VFX. Урон здесь не считается.</summary>
        public event System.Action AttackPerformed;

        /// <summary>
        /// Сервер сообщает наблюдателям о замахе. Отдельный вызов, а не вывод из здоровья цели:
        /// удар лучника расходится с попаданием на всё время полёта стрелы, да и промах
        /// по уже мёртвой цели анимировать всё равно нужно.
        /// </summary>
        public void ServerNotifyAttack()
        {
            if (IsServerInitialized)
                ObserversPlayAttack();
        }

        /// <summary>Ненадёжным каналом: потерянный замах — это пропущенная анимация, а не рассинхрон.</summary>
        [ObserversRpc]
        private void ObserversPlayAttack(Channel channel = Channel.Unreliable)
        {
            _animation.PlayAttack();
            AttackPerformed?.Invoke();
        }

        /// <summary>
        /// Сервер сообщает наблюдателям о выпущенном снаряде. Цель едет ссылкой на сетевой
        /// объект, а не координатой: пока стрела летит, противник продолжает бежать, и
        /// втыкаться она должна в него, а не в место, где он стоял в момент выстрела.
        /// </summary>
        public void ServerNotifyProjectile(ICombatTarget target, float flightTime)
        {
            if (!IsServerInitialized || _projectiles == null)
                return;

            NetworkObject targetObject = target is NetworkBehaviour behaviour ? behaviour.NetworkObject : null;
            ObserversLaunchProjectile(targetObject, flightTime);
        }

        /// <summary>Ненадёжным каналом: потерянная стрела — это пропущенный эффект, а не рассинхрон.</summary>
        [ObserversRpc]
        private void ObserversLaunchProjectile(NetworkObject target, float flightTime, Channel channel = Channel.Unreliable)
        {
            if (_projectiles == null)
                return;

            _projectiles.Emit(target != null ? target.transform : null, flightTime);
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
            _shieldRaised.Value = false;
            _locomotion?.SetActive(true);
            _locomotion?.Configure(_stats.MoveSpeed, _stats.WalkSpeed, _stats.TurnSpeed);
            _referenceSpeed.Value = _stats.MoveSpeed;
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

            if (source.Exists() && PlayerSlots.AreEnemies(source.OwnerSlot, OwnerSlot))
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
            if (_currentTarget != null && !_currentTarget.IsAliveTarget())
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

            _locomotion?.Configure(_stats.MoveSpeed, _stats.WalkSpeed, _stats.TurnSpeed);

            if (IsServerInitialized)
                _referenceSpeed.Value = _stats.MoveSpeed;

            // Прокачка здоровья применяется мгновенно ко всем живым (ГДД §11): добираем дельту,
            // а не масштабируем — иначе раненый юнит лечился бы покупкой перка.
            if (IsServerInitialized && previousMax > 0 && _stats.MaxHealth > previousMax && IsAlive)
                _health.Value += _stats.MaxHealth - previousMax;
        }
    }
}
