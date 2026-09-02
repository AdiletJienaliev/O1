using UnityEngine;
using Warlord.Core;

namespace Warlord.Gameplay.Heroes.Input
{
    /// <summary>
    /// Реализация ввода на старом Input Manager (ГДД §8: WASD, Space, Shift, ПКМ, ЛКМ).
    /// Разовые нажатия копятся между боевыми тактами и считываются ровно один раз —
    /// иначе при частоте кадров выше тикрейта прыжок терялся бы.
    /// </summary>
    public sealed class LegacyHeroInputSource : MonoBehaviour, IHeroInputSource
    {
        [Header("Клавиши")]
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode[] orderKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        [SerializeField] private KeyCode[] formationKeys = { KeyCode.Q, KeyCode.W, KeyCode.E };

        [Header("Прицеливание")]
        [Tooltip("Камера, по которой считается направление движения и точка приказа.")]
        [SerializeField] private Camera aimCamera;

        [Tooltip("Слои, по которым ищется точка приказа под курсором.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Сглаживание")]
        [Tooltip("За сколько секунд ввод набирает полную величину. 0 — мгновенно, как раньше.")]
        [SerializeField] private float moveSmoothTime = 0.09f;

        private Warlord.Presentation.HeroOrbitCamera _orbit;
        private Vector2 _smoothedMove;
        private Vector2 _moveVelocity;
        private bool _jumpBuffered;
        private bool _attackBuffered;
        private int _orderRequest = -1;
        private int _formationRequest = -1;
        private bool _orderPointBuffered;
        private Vector3 _orderPoint;

        public bool Enabled { get; set; } = true;

        public Vector2 Move { get; private set; }
        public bool Sprint { get; private set; }
        public float AimYaw { get; private set; }

        private void Awake() => ResolveCamera();

        private void Update()
        {
            ResolveCamera();

            // Открытый экран интерфейса забирает и мышь, и клавиатуру: иначе ввод адреса
            // в поле подключения заодно двигал бы полководца и раздавал приказы.
            if (!Enabled || InputFocus.UiCapturesCursor)
            {
                _smoothedMove = Vector2.zero;
                _moveVelocity = Vector2.zero;
                Move = Vector2.zero;
                Sprint = false;
                return;
            }

            Vector2 raw = new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical"));

            // Разгон и торможение по времени: сырой 0/1 с клавиатуры давал рывок на каждом
            // нажатии, а сглаживание ввода безопасно для предсказания — оно едет по сети.
            _smoothedMove = moveSmoothTime > 0f
                ? Vector2.SmoothDamp(_smoothedMove, raw, ref _moveVelocity, moveSmoothTime)
                : raw;

            Move = _smoothedMove;

            Sprint = UnityEngine.Input.GetKey(sprintKey);

            // Направление движения задаёт камера: WASD всегда относительно взгляда.
            // Азимут берём у орбитальной камеры напрямую — её eulerAngles искажены наклоном.
            AimYaw = _orbit != null
                ? _orbit.Yaw
                : (aimCamera != null ? aimCamera.transform.eulerAngles.y : transform.eulerAngles.y);

            if (UnityEngine.Input.GetKeyDown(jumpKey))
                _jumpBuffered = true;

            ReadMouse();

            for (int i = 0; i < orderKeys.Length; i++)
            {
                if (UnityEngine.Input.GetKeyDown(orderKeys[i]))
                    _orderRequest = i;
            }

            for (int i = 0; i < formationKeys.Length; i++)
            {
                if (UnityEngine.Input.GetKeyDown(formationKeys[i]))
                    _formationRequest = i;
            }
        }

        private void ReadMouse()
        {
            // Клик по кнопке HUD не должен заодно уходить в мир приказом или ударом.
            if (InputFocus.BlocksWorldInput)
                return;

            // ПКМ — удар мечом, ЛКМ — точка приказа (ГДД §8).
            if (UnityEngine.Input.GetMouseButtonDown(1))
                _attackBuffered = true;

            if (UnityEngine.Input.GetMouseButtonDown(0) && TryPickGround(out Vector3 point))
            {
                _orderPoint = point;
                _orderPointBuffered = true;
            }
        }

        /// <summary>Камера появляется в сцене независимо от полководца, поэтому ищем её, пока не найдём.</summary>
        private void ResolveCamera()
        {
            if (aimCamera == null)
                aimCamera = Camera.main;

            if (_orbit == null && aimCamera != null)
                _orbit = aimCamera.GetComponent<Warlord.Presentation.HeroOrbitCamera>();
        }

        public bool ConsumeJump() => Consume(ref _jumpBuffered);

        public bool ConsumeAttack() => Consume(ref _attackBuffered);

        public int ConsumeOrderRequest()
        {
            int value = _orderRequest;
            _orderRequest = -1;
            return value;
        }

        public int ConsumeFormationRequest()
        {
            int value = _formationRequest;
            _formationRequest = -1;
            return value;
        }

        public bool ConsumeOrderPoint(out Vector3 worldPoint)
        {
            worldPoint = _orderPoint;
            return Consume(ref _orderPointBuffered);
        }

        private bool TryPickGround(out Vector3 point)
        {
            point = default;
            if (aimCamera == null)
                return false;

            // При захваченном курсоре Input.mousePosition замирает и врать будет всегда одинаково,
            // поэтому целимся из центра экрана — туда же смотрит прицел.
            Vector3 screenPoint = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : UnityEngine.Input.mousePosition;

            Ray ray = aimCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            point = hit.point;
            return true;
        }

        private static bool Consume(ref bool flag)
        {
            bool value = flag;
            flag = false;
            return value;
        }
    }
}
