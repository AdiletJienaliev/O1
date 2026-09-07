using UnityEngine;
using Warlord.Core;
using Warlord.Presentation;

namespace Warlord.Gameplay.Heroes.Input
{
    /// <summary>
    /// Реализация ввода на старом Input Manager (ГДД §8: WASD, Space, Shift, ПКМ, ЛКМ).
    /// Клавиши движения читаются напрямую, а не через оси Input Manager: оси в проекте
    /// перенастраиваются, а раскладка WASD должна работать всегда.
    ///
    /// Разовые нажатия копятся между кадрами и считываются ровно один раз — иначе прыжок
    /// или приказ терялись бы, если между кадром нажатия и кадром чтения прошло больше одного шага.
    /// </summary>
    public sealed class LegacyHeroInputSource : MonoBehaviour, IHeroInputSource
    {
        [Header("Движение")]
        [Tooltip("Бег от камеры вглубь сцены.")]
        [SerializeField] private KeyCode forwardKey = KeyCode.W;

        [Tooltip("Бег на камеру.")]
        [SerializeField] private KeyCode backKey = KeyCode.S;

        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

        [Header("Команды")]
        [SerializeField] private KeyCode[] orderKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4
        };

        [Tooltip("Построения. Q/W/E не годятся: W — это бег вперёд, и смена строя срабатывала бы на каждом шаге.")]
        [SerializeField] private KeyCode[] formationKeys = { KeyCode.Z, KeyCode.X, KeyCode.C };

        [Header("Прицеливание")]
        [Tooltip("Запасная камера на случай, если орбитальной в сцене нет. Обычно пусто.")]
        [SerializeField] private Camera aimCamera;

        [Tooltip("Слои, по которым ищется точка приказа под курсором.")]
        [SerializeField] private LayerMask groundMask = ~0;

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

        private void Update()
        {
            ReadAim();

            // Открытый экран интерфейса забирает и мышь, и клавиатуру: иначе ввод адреса
            // в поле подключения заодно двигал бы полководца и раздавал приказы.
            if (!Enabled || InputFocus.UiCapturesCursor)
            {
                Move = Vector2.zero;
                Sprint = false;
                return;
            }

            // Сырые 0/1 без сглаживания: разгон и торможение считает HeroMotor по времени,
            // и делать то же самое дважды — значит получить вялое, «резиновое» управление.
            Move = new Vector2(
                Axis(rightKey, leftKey),
                Axis(forwardKey, backKey));

            Sprint = UnityEngine.Input.GetKey(sprintKey);

            if (UnityEngine.Input.GetKeyDown(jumpKey))
                _jumpBuffered = true;

            ReadMouse();
            ReadCommandKeys();
        }

        /// <summary>
        /// Направление движения задаёт камера: WASD всегда относительно взгляда.
        /// Азимут берём у орбитальной камеры напрямую — её eulerAngles искажены наклоном.
        /// </summary>
        private void ReadAim()
        {
            HeroOrbitCamera orbit = HeroOrbitCamera.Current;

            if (orbit != null)
            {
                AimYaw = orbit.Yaw;
                return;
            }

            Camera fallback = ResolveFallbackCamera();

            if (fallback != null)
                AimYaw = fallback.transform.eulerAngles.y;
        }

        /// <summary>
        /// Камера на крайний случай. Собственную камеру полководца брать нельзя: она вращается
        /// вместе с телом, и оси ввода уехали бы за поворотом — полководец бегал бы по спирали.
        /// </summary>
        private Camera ResolveFallbackCamera()
        {
            if (aimCamera != null && aimCamera.isActiveAndEnabled && !aimCamera.transform.IsChildOf(transform))
                return aimCamera;

            return Camera.main;
        }

        private void ReadCommandKeys()
        {
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

            HeroOrbitCamera orbit = HeroOrbitCamera.Current;
            Camera camera = orbit != null ? orbit.Camera : ResolveFallbackCamera();

            if (camera == null)
                return false;

            // При захваченном курсоре Input.mousePosition замирает и врать будет всегда одинаково,
            // поэтому целимся из центра экрана — туда же смотрит прицел.
            Vector3 screenPoint = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : UnityEngine.Input.mousePosition;

            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            point = hit.point;
            return true;
        }

        private static float Axis(KeyCode positive, KeyCode negative)
        {
            float value = 0f;

            if (UnityEngine.Input.GetKey(positive))
                value += 1f;

            if (UnityEngine.Input.GetKey(negative))
                value -= 1f;

            return value;
        }

        private static bool Consume(ref bool flag)
        {
            bool value = flag;
            flag = false;
            return value;
        }
    }
}
