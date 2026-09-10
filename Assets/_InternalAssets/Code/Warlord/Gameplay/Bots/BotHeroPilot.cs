using UnityEngine;
using UnityEngine.AI;
using Warlord.Configs;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Heroes.Input;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Ноги и руки полководца-бота. Реализует тот же <see cref="IHeroInputSource"/>,
    /// что и клавиатура: для <see cref="Warlord.Gameplay.Heroes.HeroController"/> бот
    /// неотличим от игрока, который очень уверенно жмёт WASD.
    ///
    /// Так сделано намеренно. Второй путь движения — «сервер телепортирует полководца
    /// бота по своим правилам» — разошёлся бы с игроком в первый же день: у бота не было бы
    /// ни разгона, ни инерции, ни падений, ни застреваний в геометрии, и любая правка
    /// физики полководца требовала бы второй правки для ботов. Здесь править нечего:
    /// движение одно на всех, разный только источник нажатий.
    ///
    /// Путь считается по тому же NavMesh, по которому ходят юниты. Углы пути
    /// превращаются в направление, направление — в «нажатие вперёд» и азимут взгляда.
    /// </summary>
    public sealed class BotHeroPilot : MonoBehaviour, IHeroInputSource
    {
        // Углы пути свои у каждого пилота: общий статический буфер затирался бы
        // соседним ботом между пересчётом пути и чтением следующей точки.
        private readonly Vector3[] _corners = new Vector3[16];

        private NavMeshPath _path;
        private HeroConfig _config;
        private System.Random _random;

        private Vector3 _destination;
        private bool _hasDestination;
        private float _stopDistance = 1.5f;
        private bool _sprint;

        private ICombatTarget _target;

        private float _repathTimer;
        private int _cornerCount;
        private int _cornerIndex;

        private Vector3 _lastPosition;
        private float _stuckTimer;
        private float _unstuckYaw;
        private float _unstuckTimer;

        private bool _attackRequested;
        private bool _jumpRequested;

        private Vector2 _move;
        private float _aimYaw;

        /// <summary>Как часто пересчитывается путь, с. Чаще незачем: цель бота меняется реже.</summary>
        private const float RepathInterval = 0.5f;

        /// <summary>
        /// Сколько «нажатия вперёд» бот держит, стоя на месте у цели. Ноль означал бы,
        /// что корпус перестаёт доворачиваться (см. HeroMotor: без ввода поворот сохраняется),
        /// и полководец бил бы мимо, стоя спиной к противнику.
        /// </summary>
        private const float FacingCreep = 0.12f;

        #region IHeroInputSource

        public Vector2 Move => _move;
        public bool Sprint => _sprint;
        public float AimYaw => _aimYaw;

        public bool ConsumeJump()
        {
            bool value = _jumpRequested;
            _jumpRequested = false;
            return value;
        }

        public bool ConsumeAttack()
        {
            bool value = _attackRequested;
            _attackRequested = false;
            return value;
        }

        /// <summary>Приказы бот отдаёт напрямую серверными командами, а не через ввод.</summary>
        public int ConsumeOrderRequest() => -1;

        public int ConsumeFormationRequest() => -1;

        public bool ConsumeOrderPoint(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            return false;
        }

        #endregion

        public void Configure(HeroConfig config, System.Random random)
        {
            _config = config;
            _random = random;
            _path = new NavMeshPath();
            _lastPosition = transform.position;
            _aimYaw = transform.eulerAngles.y;
        }

        /// <summary>Куда идти. Повторный вызов с той же точкой путь не пересчитывает.</summary>
        public void MoveTo(Vector3 destination, float stopDistance, bool sprint)
        {
            _stopDistance = Mathf.Max(0.5f, stopDistance);
            _sprint = sprint;

            if (_hasDestination && (destination - _destination).sqrMagnitude < 1f)
                return;

            _destination = destination;
            _hasDestination = true;
            _repathTimer = 0f;
        }

        /// <summary>Стоять на месте. Взгляд при этом сохраняется — бот не крутится вхолостую.</summary>
        public void Halt()
        {
            _hasDestination = false;
            _cornerCount = 0;
            _move = Vector2.zero;
        }

        /// <summary>Кого бить, подойдя вплотную. Null — никого.</summary>
        public void SetCombatTarget(ICombatTarget target) => _target = target;

        /// <summary>Дошёл ли бот до места назначения.</summary>
        public bool AtDestination
        {
            get
            {
                if (!_hasDestination)
                    return true;

                Vector3 delta = _destination - transform.position;
                delta.y = 0f;

                return delta.sqrMagnitude <= _stopDistance * _stopDistance;
            }
        }

        public Vector3 Destination => _destination;

        private void Update()
        {
            float delta = Time.deltaTime;

            // Порядок важен: движение задаёт азимут по дороге, бой перебивает его прицелом.
            UpdateMovement(delta);
            UpdateCombat();
            UpdateStuck(delta);
        }

        #region Бой

        /// <summary>
        /// Удар вблизи. Бот не бьёт по площади вслепую: сначала доворачивается,
        /// а нажатие ставит только когда цель действительно в секторе — иначе серверная
        /// валидация удара всё равно отказала бы, а замах уже съел бы кулдаун.
        /// </summary>
        private void UpdateCombat()
        {
            if (_config == null || !_target.IsAliveTarget())
                return;

            Vector3 delta = _target.Position - transform.position;
            delta.y = 0f;

            float distance = delta.magnitude - _target.Radius;

            if (distance > _config.attackRange)
                return;

            _aimYaw = YawOf(delta);

            float facing = Vector3.Angle(transform.forward, delta.normalized);
            if (facing <= _config.attackHalfAngle)
                _attackRequested = true;
        }

        #endregion

        #region Движение

        private void UpdateMovement(float delta)
        {
            if (!_hasDestination || AtDestination)
            {
                // У цели бот не замирает столбом: лёгкое «нажатие вперёд» держит корпус
                // развёрнутым туда, куда он смотрит, и позволяет добить того, кто рядом.
                _move = _target.IsAliveTarget() ? new Vector2(0f, FacingCreep) : Vector2.zero;
                return;
            }

            _repathTimer -= delta;

            if (_repathTimer <= 0f)
            {
                _repathTimer = RepathInterval;
                RebuildPath();
            }

            Vector3 waypoint = NextWaypoint();
            Vector3 direction = waypoint - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
            {
                _move = Vector2.zero;
                return;
            }

            // Обход застревания: пока идёт откат, бот держит смещённый азимут и уходит вбок.
            float yaw = _unstuckTimer > 0f ? _unstuckYaw : YawOf(direction);

            _aimYaw = yaw;
            _move = new Vector2(0f, 1f);
        }

        /// <summary>
        /// Пересчёт пути по NavMesh. Недостижимая точка не считается ошибкой: бот идёт
        /// в её сторону настолько, насколько получится — так же, как игрок, упёршийся в стену.
        /// </summary>
        private void RebuildPath()
        {
            _cornerCount = 0;
            _cornerIndex = 0;

            if (_path == null)
                _path = new NavMeshPath();

            Vector3 target = _destination;

            if (NavMesh.SamplePosition(_destination, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                target = hit.position;

            if (!NavMesh.CalculatePath(transform.position, target, NavMesh.AllAreas, _path))
                return;

            _cornerCount = _path.GetCornersNonAlloc(_corners);
            _cornerIndex = _cornerCount > 1 ? 1 : 0;
        }

        private Vector3 NextWaypoint()
        {
            if (_cornerCount <= 0)
                return _destination;

            while (_cornerIndex < _cornerCount)
            {
                Vector3 corner = _corners[_cornerIndex];
                Vector3 delta = corner - transform.position;
                delta.y = 0f;

                if (delta.sqrMagnitude > 1.5f * 1.5f)
                    return corner;

                _cornerIndex++;
            }

            return _destination;
        }

        /// <summary>
        /// Застревание. Полководец ходит CharacterController-ом и цепляется за геометрию,
        /// которую NavMesh считает проходимой. Сначала пробуем прыжок, потом уход вбок —
        /// ровно то, что делает живой игрок, когда упёрся в угол.
        /// </summary>
        private void UpdateStuck(float delta)
        {
            if (_unstuckTimer > 0f)
                _unstuckTimer -= delta;

            if (!_hasDestination || AtDestination)
            {
                _stuckTimer = 0f;
                _lastPosition = transform.position;
                return;
            }

            Vector3 moved = transform.position - _lastPosition;
            moved.y = 0f;

            if (moved.sqrMagnitude > 0.04f)
            {
                _stuckTimer = 0f;
                _lastPosition = transform.position;
                return;
            }

            _stuckTimer += delta;

            if (_stuckTimer < 0.8f)
                return;

            _stuckTimer = 0f;
            _lastPosition = transform.position;

            if (_unstuckTimer <= 0f)
            {
                _jumpRequested = true;
                _unstuckTimer = 0.9f;
                _unstuckYaw = _aimYaw + (NextSign() * 70f);
                return;
            }

            // Прыжок не помог — разворачиваемся сильнее и заодно просим новый путь.
            _unstuckYaw = _aimYaw + (NextSign() * 130f);
            _unstuckTimer = 1.4f;
            _repathTimer = 0f;
        }

        private float NextSign()
        {
            if (_random == null)
                return 1f;

            return _random.NextDouble() < 0.5 ? -1f : 1f;
        }

        private static float YawOf(Vector3 direction)
        {
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        #endregion
    }
}
