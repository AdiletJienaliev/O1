using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Heroes.Input;
using Warlord.Gameplay.Match;
using Warlord.Presentation.Animation;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Полководец (ГДД §8). Движение целиком локальное: тело двигает только владелец,
    /// у себя, в Update — а остальным готовая позиция уезжает через NetworkTransform
    /// (client authoritative). Предсказания с переигрыванием тактов здесь больше нет:
    /// оно дралось с NetworkTransform за один и тот же трансформ, и каждая реконсиляция
    /// возвращала тело назад — отсюда и дёрганье.
    ///
    /// Всё, что влияет на исход, по-прежнему серверное: здоровье, смерть, респавн, урон.
    /// Ценой отказа от предсказания стал контроль сервера над позицией полководца —
    /// клиент может её подделать. Для боя это терпимо: удар всё равно валидируется
    /// сервером с лаг-компенсацией.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroController : NetworkBehaviour, ICombatTarget, IArmyLeader, IHealthSource
    {
        [Header("Компоненты")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private HeroCombat combat;

        [Tooltip("Радиус тела для расчёта дистанций боя.")]
        [SerializeField] private float bodyRadius = 0.5f;

        private readonly SyncVar<byte> _slot = new();
        private readonly SyncVar<int> _health = new();
        private readonly SyncVar<bool> _spectator = new();

        private IHeroInputSource _input;
        private HeroConfig _config;
        private IMatchContext _context;
        private HeroMotor _motor;
        private CharacterAnimationDriver _animation;

        private float _respawnTimer;
        private float _timeSinceDamage;
        private float _spawnProtectionTimer;
        private bool _botDriven;

        public int Slot => _slot.Value;
        public bool IsSpectator => _spectator.Value;
        public int Health => _health.Value;

        /// <summary>Максимум здоровья. Нужен полоске над головой — она есть и на клиентах.</summary>
        public int MaxHealth => _config != null ? _config.maxHealth : 0;

        public float RespawnTimeRemaining => _respawnTimer;
        public bool IsInvulnerable => _spawnProtectionTimer > 0f;

        /// <summary>Телом управляет бот на сервере, а не клиент-владелец.</summary>
        public bool IsBotDriven => _botDriven;

        #region ICombatTarget

        public int OwnerSlot => _slot.Value;
        public CombatantKind Kind => CombatantKind.Hero;

        /// <summary>Полководец не участвует в матрице типов юнитов (ГДД §5.3).</summary>
        public int UnitTypeIndex => -1;

        public bool IsAlive => _health.Value > 0 && !_spectator.Value;
        public Vector3 Position => transform.position;
        public float Radius => bodyRadius;
        public int Armor => 0;

        #endregion

        #region IArmyLeader

        public float YawDegrees => transform.eulerAngles.y;

        #endregion

        private void Awake()
        {
            characterController ??= GetComponent<CharacterController>();
            combat ??= GetComponent<HeroCombat>();
            _input = inputSource as IHeroInputSource;
            _input ??= GetComponent<IHeroInputSource>();

            _motor = new HeroMotor(characterController, transform);
            _animation = new CharacterAnimationDriver(gameObject, transform);
        }

        private void OnEnable()
        {
            if (combat != null)
                combat.AttackPerformed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            if (combat != null)
                combat.AttackPerformed -= OnAttackPerformed;
        }

        /// <summary>Инициализация на сервере сразу после спавна.</summary>
        public void ServerInitialize(IMatchContext context, int slot)
        {
            _context = context;
            ApplyConfig(context.Config.Hero);
            _slot.Value = (byte)slot;
            _health.Value = _config.maxHealth;
            _spectator.Value = false;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            MatchManager manager = MatchManager.Instance;
            if (manager != null && _config == null)
                ApplyConfig(manager.Config.Hero);
        }

        /// <summary>
        /// Полководец локального игрока. Ставится на клиенте при получении владения —
        /// иначе камера и HUD не смогли бы отличить свой объект от чужих. Тот же приём,
        /// что и у <see cref="Warlord.Gameplay.Players.PlayerState.Local"/>.
        /// </summary>
        public static HeroController Local { get; private set; }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
                Local = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (Local == this)
                Local = null;
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

        private void Update()
        {
            float delta = Time.deltaTime;

            // Тело двигает только владелец. У всех остальных — и на сервере тоже —
            // трансформ приходит по сети, и трогать его здесь нельзя.
            //
            // Исключение — полководец бота: владельца у него нет вовсе, и тело ведёт сервер.
            // NetworkTransform это учитывает сам: при client authoritative без владельца
            // авторитетной становится серверная копия, и позиция уезжает клиентам от неё.
            if (IsOwner || (_botDriven && IsServerInitialized))
                TickMovement(delta);

            TickAnimation(delta);
        }

        private void TickMovement(float delta)
        {
            // Прыжок считывается всегда: иначе нажатие, пойманное во время смерти
            // или в открытом меню, выстрелило бы при возвращении управления.
            bool jump = _input != null && _input.ConsumeJump();

            if (!IsAlive)
            {
                _motor.Fall(delta);
                return;
            }

            // Без источника ввода мотор всё равно надо тикать: гравитация и торможение
            // должны продолжаться, иначе полководец замирает в воздухе.
            if (_input == null)
            {
                _motor.Move(Vector2.zero, YawDegrees, false, false, delta);
                return;
            }

            _motor.Move(_input.Move, _input.AimYaw, _input.Sprint, jump, delta);
        }

        /// <summary>
        /// Анимация. У владельца скорость известна точно, остальным её приходится
        /// восстанавливать из смещения трансформа — там его двигает NetworkTransform.
        /// </summary>
        private void TickAnimation(float delta)
        {
            if (!_animation.IsBound)
                return;

            // До инициализации сетью здоровье нулевое: без этой проверки полководец
            // играл бы смерть в первом же кадре после появления.
            if (!IsSpawned)
                return;

            _animation.SetAlive(IsAlive);

            if (!IsOwner && !_botDriven)
            {
                _animation.SetGrounded(true);
                _animation.TickFromTransform(delta);
                return;
            }

            // На клиентах полководец бота — обычный чужой объект: скорость там восстанавливается
            // из смещения трансформа, а мотор пуст и врал бы нулём.
            if (_botDriven && !IsServerInitialized)
            {
                _animation.SetGrounded(true);
                _animation.TickFromTransform(delta);
                return;
            }

            if (_motor.ConsumeJumped())
                _animation.PlayJump();

            _animation.SetGrounded(_motor.IsGrounded);
            _animation.Tick(_motor.PlanarVelocity, delta);
        }

        private void OnAttackPerformed() => _animation.PlayAttack();

        private void ApplyConfig(HeroConfig config)
        {
            if (config == null)
                return;

            _config = config;
            _motor.Configure(config);
            _animation.SetReferenceSpeed(_motor.MaxSpeed);
        }

        /// <summary>
        /// Отдать управление телом боту (сервер). Источник ввода подменяется целиком:
        /// клавиатурный компонент на префабе выключается, иначе на хосте полководец бота
        /// повторял бы нажатия живого игрока — они читаются глобально, а не по владению.
        /// </summary>
        public void ServerAttachBot(IHeroInputSource botInput)
        {
            if (botInput == null)
                return;

            if (inputSource != null)
                inputSource.enabled = false;

            _input = botInput;
            _botDriven = true;

            if (combat != null)
                combat.ServerAttachBot(botInput);
        }

        public void ReceiveDamage(int amount, ICombatTarget source)
        {
            if (!IsServerInitialized || amount <= 0 || !IsAlive || IsInvulnerable)
                return;

            _health.Value = Mathf.Max(0, _health.Value - amount);
            _timeSinceDamage = 0f;

            if (_health.Value > 0)
                return;

            _respawnTimer = _config.respawnTime;
            int killerSlot = source != null ? source.OwnerSlot : PlayerSlots.None;
            _context.Events.RaiseHeroKilled(Slot, killerSlot);
        }

        /// <summary>
        /// Серверный такт полководца: регенерация вне боя и отсчёт респавна (ГДД §8).
        /// Вызывается из <see cref="HeroLifecycleSystem"/>, а не из Update — чтобы всё,
        /// что влияет на исход, шло в едином боевом такте.
        /// </summary>
        public void ServerTick(float deltaTime)
        {
            if (!IsServerInitialized)
                return;

            combat?.ServerTick(deltaTime);

            if (IsSpectator)
                return;

            if (_spawnProtectionTimer > 0f)
                _spawnProtectionTimer -= deltaTime;

            if (IsAlive)
            {
                _timeSinceDamage += deltaTime;

                if (_timeSinceDamage >= _config.outOfCombatDelay && _health.Value < _config.maxHealth)
                {
                    float healed = _config.outOfCombatRegen * deltaTime;
                    _health.Value = Mathf.Min(_config.maxHealth, _health.Value + Mathf.CeilToInt(healed));
                }

                return;
            }

            _respawnTimer -= deltaTime;
            if (_respawnTimer <= 0f)
                ServerRespawn();
        }

        /// <summary>Респавн на своей базе. Штрафов по золоту нет — цена смерти в потерянном времени.</summary>
        public void ServerRespawn()
        {
            if (!IsServerInitialized || IsSpectator)
                return;

            ServerTeleport(_context.Players.GetBaseAnchor(Slot).HeroSpawnPoint);

            _health.Value = _config.maxHealth;
            _timeSinceDamage = _config.outOfCombatDelay;
            _spawnProtectionTimer = _context.Config.GameMode.spawnProtectionDuration;

            _context.Events.RaiseHeroRespawned(Slot);
        }

        /// <summary>
        /// Перенос тела сервером. Трансформ полководца ведёт владелец, поэтому серверу
        /// недостаточно подвинуть свою копию — иначе следующий же пакет от клиента
        /// вернул бы тело на место смерти. Владельцу уходит адресный приказ телепорта.
        /// </summary>
        private void ServerTeleport(Vector3 position)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;

            // У бота тело ведёт сам сервер, поэтому сбрасывать скорости надо здесь же:
            // приказа владельцу, который это сделал бы, слать некому.
            if (_botDriven)
            {
                _motor.Teleport(position);
                _animation.ResetMotion();
                return;
            }

            if (Owner != null && Owner.IsActive)
                TargetTeleport(Owner, position);
        }

        [TargetRpc]
        private void TargetTeleport(NetworkConnection connection, Vector3 position)
        {
            _motor.Teleport(position);
            _animation.ResetMotion();
        }

        /// <summary>Игрок выбыл: полководец больше не респавнится и не участвует в бою (ГДД §10.3).</summary>
        public void ServerSetSpectator()
        {
            if (!IsServerInitialized)
                return;

            _spectator.Value = true;
            _health.Value = 0;
            _respawnTimer = float.MaxValue;

            _context.Targeting.Unregister(this);
            _context.LagCompensation.Untrack(this);
        }
    }
}
