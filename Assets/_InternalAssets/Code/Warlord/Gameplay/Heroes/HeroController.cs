using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Army;
using Warlord.Gameplay.Heroes.Input;
using Warlord.Gameplay.Match;

namespace Warlord.Gameplay.Heroes
{
    /// <summary>
    /// Полководец (ГДД §8). Движение предсказывается на клиенте и выправляется сервером
    /// (FishNet Prediction v2), поэтому управление отзывчиво; всё остальное — здоровье,
    /// смерть, респавн — только серверное.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HeroController : TickNetworkBehaviour, ICombatTarget, IArmyLeader
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

        private float _verticalVelocity;
        private float _respawnTimer;
        private float _timeSinceDamage;
        private float _spawnProtectionTimer;
        private HeroReplicateData _lastTickedInput;

        public int Slot => _slot.Value;
        public bool IsSpectator => _spectator.Value;
        public int Health => _health.Value;
        public float RespawnTimeRemaining => _respawnTimer;
        public bool IsInvulnerable => _spawnProtectionTimer > 0f;

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

            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        /// <summary>Инициализация на сервере сразу после спавна.</summary>
        public void ServerInitialize(IMatchContext context, int slot)
        {
            _context = context;
            _config = context.Config.Hero;
            _slot.Value = (byte)slot;
            _health.Value = _config.maxHealth;
            _spectator.Value = false;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            MatchManager manager = MatchManager.Instance;
            if (manager != null)
                _config = manager.Config.Hero;
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

        protected override void TimeManager_OnTick() => PerformReplicate(BuildInput());

        protected override void TimeManager_OnPostTick() => CreateReconcile();

        public override void CreateReconcile()
        {
            PerformReconcile(new HeroReconcileData(transform.position, transform.eulerAngles.y, _verticalVelocity, IsAlive));
        }

        private HeroReplicateData BuildInput()
        {
            // Данные ввода строит только владелец объекта; сервер получит их по сети.
            if (!IsOwner || _input == null || !IsAlive)
                return default;

            return new HeroReplicateData(_input.Move, _input.AimYaw, _input.Sprint, _input.ConsumeJump());
        }

        [Replicate]
        private void PerformReplicate(HeroReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;

            if (!IsAlive)
            {
                // Мёртвый полководец не двигается, но CharacterController всё равно надо шевелить,
                // иначе его коллайдер зависает в устаревшем состоянии.
                characterController.Move(new Vector3(0f, -1f, 0f) * delta);
                return;
            }

            // Наблюдатели предсказывают чужой ввод на такт вперёд: это скрывает
            // сетевую задержку без заметного риска рассинхрона.
            if (!IsServerStarted && !IsOwner)
            {
                if (state.ContainsTicked())
                {
                    _lastTickedInput.Dispose();
                    _lastTickedInput = data;
                }
                else if (state.IsFuture() && data.GetTick() - _lastTickedInput.GetTick() <= 1)
                {
                    data = _lastTickedInput;
                    data.Jump = false;
                }
            }

            ApplyMovement(in data, delta);
        }

        private void ApplyMovement(in HeroReplicateData data, float delta)
        {
            HeroConfig config = _config;
            if (config == null)
                return;

            _verticalVelocity += Physics.gravity.y * config.gravityScale * delta;
            if (_verticalVelocity < -config.terminalVelocity)
                _verticalVelocity = -config.terminalVelocity;

            if (characterController.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (data.Jump && characterController.isGrounded)
            {
                // v = sqrt(2 * g * h) — прыжок задаётся высотой, а не силой: так проще балансировать.
                float gravity = Mathf.Abs(Physics.gravity.y) * config.gravityScale;
                _verticalVelocity = Mathf.Sqrt(2f * gravity * config.jumpHeight);
            }

            // Ввод нормализуем только когда он длиннее единицы: по диагонали скорость
            // не должна расти, но короткие отклонения стика обязаны сохраняться.
            Vector2 move = data.Move;
            float amount = move.magnitude;

            if (amount > 1f)
            {
                move /= amount;
                amount = 1f;
            }

            // Оси камеры: W — от игрока вглубь экрана, S — на игрока, A и D — строго вбок.
            Quaternion cameraYaw = Quaternion.Euler(0f, data.AimYaw, 0f);
            Vector3 direction = cameraYaw * new Vector3(move.x, 0f, move.y);

            float speed = data.Sprint ? config.sprintSpeed : config.moveSpeed;

            Vector3 motion = direction * speed;
            motion.y = _verticalVelocity;
            characterController.Move(motion * delta);

            ApplyRotation(config, direction, amount, data.AimYaw, delta);
        }

        /// <summary>
        /// Доворот тела. Отделён от перемещения намеренно: направление шага и направление
        /// взгляда — разные вещи, и режим их связи задаётся конфигом (ГДД §8).
        /// </summary>
        private void ApplyRotation(HeroConfig config, Vector3 direction, float inputAmount, float aimYaw, float delta)
        {
            Vector3 facing;

            if (config.rotationMode == HeroRotationMode.FaceCamera)
            {
                facing = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
            }
            else
            {
                // Клавиши отпущены — сохраняем текущий разворот. Иначе полководец
                // дёргался бы к направлению последнего кадра при каждой остановке.
                if (inputAmount < 0.01f)
                    return;

                facing = direction;
            }

            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                return;

            Quaternion target = Quaternion.LookRotation(facing);

            transform.rotation = config.turnSpeed > 0f
                ? Quaternion.RotateTowards(transform.rotation, target, config.turnSpeed * delta)
                : target;
        }

        [Reconcile]
        private void PerformReconcile(HeroReconcileData data, Channel channel = Channel.Unreliable)
        {
            _verticalVelocity = data.VerticalVelocity;

            // CharacterController обязательно выключить перед переносом, иначе физика
            // останется в старой позиции до следующего Move.
            characterController.enabled = false;
            transform.position = data.Position;
            transform.rotation = Quaternion.Euler(0f, data.Yaw, 0f);
            characterController.enabled = true;
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

            Vector3 spawnPoint = _context.Players.GetBaseAnchor(Slot).HeroSpawnPoint;

            characterController.enabled = false;
            transform.position = spawnPoint;
            characterController.enabled = true;

            _verticalVelocity = 0f;
            _health.Value = _config.maxHealth;
            _timeSinceDamage = _config.outOfCombatDelay;
            _spawnProtectionTimer = _context.Config.GameMode.spawnProtectionDuration;

            _context.Events.RaiseHeroRespawned(Slot);
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
