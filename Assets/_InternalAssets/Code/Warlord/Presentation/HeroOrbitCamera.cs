using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;

namespace Warlord.Presentation
{
    /// <summary>
    /// Орбитальная камера от третьего лица. Живёт в сцене в одном экземпляре и следит
    /// за полководцем локального игрока — поэтому она клиентская по построению: у каждого
    /// клиента своя, и чужие объекты на неё не влияют.
    ///
    /// Камера намеренно не является дочерней полководцу: поворот тела не должен утаскивать
    /// обзор. Позиция и поворот считаются в мировых координатах каждый LateUpdate,
    /// уже после того как предсказание FishNet подвинуло полководца.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HeroOrbitCamera : MonoBehaviour
    {
        /// <summary>Когда мышь вращает камеру.</summary>
        public enum OrbitTrigger
        {
            /// <summary>Всегда: курсор захвачен, прицел в центре экрана.</summary>
            Always = 0,
            /// <summary>Пока зажата средняя кнопка. Курсор остаётся свободным.</summary>
            HoldMiddleMouse = 1,
            /// <summary>Пока зажата правая кнопка. Конфликтует с ударом мечом (ГДД §8).</summary>
            HoldRightMouse = 2
        }

        [Header("Цель")]
        [Tooltip("Кого снимать, если полководца ещё нет. Нужен для прогулки по сцене без сети.")]
        [SerializeField] private Transform fallbackTarget;

        [Tooltip("Смещение точки обзора от корня полководца: примерно уровень плеч.")]
        [SerializeField] private Vector3 pivotOffset = new(0f, 1.55f, 0f);

        [Header("Орбита")]
        [SerializeField] private OrbitTrigger trigger = OrbitTrigger.Always;
        [SerializeField] private float sensitivityX = 3.2f;
        [SerializeField] private float sensitivityY = 2.4f;
        [SerializeField] private bool invertY;

        [Tooltip("Ограничение наклона: ниже горизонта и почти сверху.")]
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 72f;
        [SerializeField] private float startPitch = 20f;

        [Header("Дистанция")]
        [SerializeField] private float distance = 7f;
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 14f;
        [SerializeField] private float zoomStep = 1.2f;
        [SerializeField] private float zoomSmoothTime = 0.1f;

        [Header("Сглаживание")]
        [Tooltip("Насколько мягко камера догоняет полководца. Больше — плавнее и вязче.")]
        [SerializeField] private float followSmoothTime = 0.055f;

        [Header("Столкновения")]
        [Tooltip("По каким слоям камера не пролезает сквозь геометрию.")]
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Tooltip("Радиус пробы: камера не должна впритык касаться стены.")]
        [SerializeField] private float probeRadius = 0.3f;
        [SerializeField] private float collisionPadding = 0.2f;

        [Tooltip("Как быстро камера возвращается назад, когда препятствие ушло.")]
        [SerializeField] private float returnSpeed = 8f;

        [Header("Курсор")]
        [Tooltip("Держать курсор в центре и скрывать его. Перекрывается GameFlowConfig.")]
        [SerializeField] private bool lockCursor;

        [Tooltip("Пока клавиша зажата, курсор свободен: можно ткнуть в HUD, камера стоит.")]
        [SerializeField] private KeyCode freeCursorKey = KeyCode.LeftAlt;

        private Transform _target;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;
        private float _yaw;
        private float _pitch;
        private float _desiredDistance;
        private float _smoothedDistance;
        private float _distanceVelocity;
        private float _occludedDistance;
        private bool _hasPivot;

        /// <summary>Азимут камеры, град. Ввод полководца строит по нему направление WASD.</summary>
        public float Yaw => _yaw;

        /// <summary>Захвачен ли сейчас курсор. Прицел рисуется только в этом состоянии.</summary>
        public bool CursorLocked { get; private set; }

        private void Awake()
        {
            _pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
            _yaw = transform.eulerAngles.y;

            _desiredDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            _smoothedDistance = _desiredDistance;
            _occludedDistance = _desiredDistance;
        }

        private void OnEnable() => ApplyCursor(false);

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CursorLocked = false;
        }

        private void LateUpdate()
        {
            ResolveTarget();

            bool wantsLock = WantsCursorLock();
            ApplyCursor(wantsLock);

            if (AcceptsMouse(wantsLock))
                ReadMouse();

            ReadZoom();
            MovePivot();
            PlaceCamera();
        }

        private void ResolveTarget()
        {
            // Полководец пересоздаётся между матчами, поэтому цель проверяем каждый кадр,
            // а не кэшируем один раз при старте.
            Transform next = ResolveHeroView(HeroController.Local);

            if (next == null)
                next = fallbackTarget;

            if (next == _target)
                return;

            _target = next;

            if (_target == null)
                return;

            // Первый захват цели — без сглаживания, иначе камера летит через всю карту.
            _pivot = _target.position + pivotOffset;
            _pivotVelocity = Vector3.zero;
            _hasPivot = true;
        }

        /// <summary>
        /// За чем именно следить. Предсказание сглаживает не корень объекта, а графику:
        /// корень реконсиляция дёргает каждый такт, и камера на нём тряслась бы.
        /// </summary>
        private static Transform ResolveHeroView(HeroController hero)
        {
            if (hero == null)
                return null;

            Transform graphical = hero.NetworkObject != null ? hero.NetworkObject.GetGraphicalObject() : null;
            return graphical != null ? graphical : hero.transform;
        }

        private bool WantsCursorLock()
        {
            if (trigger != OrbitTrigger.Always)
                return false;

            if (!ResolveLockSetting() || InputFocus.UiCapturesCursor)
                return false;

            if (Input.GetKey(freeCursorKey))
                return false;

            return Application.isFocused;
        }

        /// <summary>Настройка курсора из потока игры, если он задан; иначе — своё поле.</summary>
        private bool ResolveLockSetting()
        {
            MatchManager match = MatchManager.Instance;
            GameFlowConfig flow = match != null && match.Config != null ? match.Config.Flow : null;

            return flow != null ? flow.lockCursorInMatch : lockCursor;
        }

        private bool AcceptsMouse(bool cursorLocked)
        {
            if (InputFocus.UiCapturesCursor)
                return false;

            switch (trigger)
            {
                case OrbitTrigger.HoldMiddleMouse: return Input.GetMouseButton(2);
                case OrbitTrigger.HoldRightMouse: return Input.GetMouseButton(1);
                // В режиме Always камера вращается и со свободным курсором: иначе,
                // выключив захват мыши, игрок остался бы вообще без обзора.
                default: return true;
            }
        }

        private void ReadMouse()
        {
            float deltaX = Input.GetAxisRaw("Mouse X") * sensitivityX;
            float deltaY = Input.GetAxisRaw("Mouse Y") * sensitivityY;

            _yaw = Mathf.Repeat(_yaw + deltaX, 360f);
            _pitch = Mathf.Clamp(_pitch + (invertY ? deltaY : -deltaY), minPitch, maxPitch);
        }

        private void ReadZoom()
        {
            if (!InputFocus.BlocksWorldInput)
            {
                float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
                if (!Mathf.Approximately(scroll, 0f))
                    _desiredDistance = Mathf.Clamp(_desiredDistance - scroll * zoomStep * 10f, minDistance, maxDistance);
            }

            _smoothedDistance = Mathf.SmoothDamp(_smoothedDistance, _desiredDistance, ref _distanceVelocity, zoomSmoothTime);
        }

        private void MovePivot()
        {
            if (_target == null)
                return;

            Vector3 desired = _target.position + pivotOffset;

            if (!_hasPivot)
            {
                _pivot = desired;
                _hasPivot = true;
                return;
            }

            _pivot = Vector3.SmoothDamp(_pivot, desired, ref _pivotVelocity, followSmoothTime);
        }

        private void PlaceCamera()
        {
            if (!_hasPivot)
                return;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float allowed = ResolveOcclusion(back);

            transform.SetPositionAndRotation(_pivot + back * allowed, rotation);
        }

        /// <summary>
        /// Дистанция с учётом препятствий. Внутрь камера прыгает мгновенно — иначе она
        /// на кадр окажется в стене; наружу выезжает плавно, чтобы не дёргаться на краях.
        /// </summary>
        private float ResolveOcclusion(Vector3 back)
        {
            float wanted = _smoothedDistance;

            bool blocked = Physics.SphereCast(
                _pivot,
                probeRadius,
                back,
                out RaycastHit hit,
                wanted,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            float limit = blocked ? Mathf.Max(minDistance * 0.5f, hit.distance - collisionPadding) : wanted;

            _occludedDistance = limit < _occludedDistance
                ? limit
                : Mathf.MoveTowards(_occludedDistance, limit, returnSpeed * Time.deltaTime);

            return _occludedDistance;
        }

        private void ApplyCursor(bool locked)
        {
            if (locked == CursorLocked)
                return;

            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
