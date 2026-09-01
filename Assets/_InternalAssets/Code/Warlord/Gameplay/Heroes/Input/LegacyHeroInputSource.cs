using UnityEngine;

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

        private void Awake() => aimCamera ??= Camera.main;

        private void Update()
        {
            if (!Enabled)
            {
                Move = Vector2.zero;
                Sprint = false;
                return;
            }

            Move = new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical"));

            Sprint = UnityEngine.Input.GetKey(sprintKey);
            AimYaw = aimCamera != null ? aimCamera.transform.eulerAngles.y : transform.eulerAngles.y;

            if (UnityEngine.Input.GetKeyDown(jumpKey))
                _jumpBuffered = true;

            // ПКМ — удар мечом, ЛКМ — точка приказа (ГДД §8).
            if (UnityEngine.Input.GetMouseButtonDown(1))
                _attackBuffered = true;

            if (UnityEngine.Input.GetMouseButtonDown(0) && TryPickGround(out Vector3 point))
            {
                _orderPoint = point;
                _orderPointBuffered = true;
            }

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

            Ray ray = aimCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
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
