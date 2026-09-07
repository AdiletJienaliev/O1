using UnityEngine;

namespace Warlord.Gameplay.World
{
    /// <summary>
    /// Габариты арены, заданные в сцене. Нужны в двух местах: сервер отбрасывает приказы
    /// за границами, а репликация юнитов квантует позиции в эти рамки (ГДД §12) — точность
    /// сжатия напрямую зависит от размера, поэтому цифра обязана быть честной.
    ///
    /// Компонент необязателен: без него берутся значения по умолчанию, и игра просто
    /// работает на арене 160 м. Это осознанно — забытый компонент не должен ронять матч.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchArena : MonoBehaviour
    {
        private const float DefaultSize = 160f;
        private static readonly Vector2 DefaultHeights = new(-20f, 60f);

        [Header("Идентификация")]
        [SerializeField] private string displayName = "Арена";

        [Header("Габариты")]
        [Tooltip("Сторона квадратной арены, м. По ней же считается сжатие позиций в сети.")]
        [Min(16f)] [SerializeField] private float size = 160f;

        [Tooltip("Границы по высоте для сжатия позиций, м. Должны накрывать всю играбельную геометрию.")]
        [SerializeField] private Vector2 heightRange = new(-20f, 60f);

        private static MatchArena _instance;

        public static string Title => _instance != null ? _instance.displayName : "Арена";

        public static float Size => _instance != null ? _instance.size : DefaultSize;

        public static Vector2 HeightRange => _instance != null ? _instance.heightRange : DefaultHeights;

        /// <summary>Границы арены в мировых координатах. Сервер отбрасывает приказы за их пределами.</summary>
        public static Bounds WorldBounds
        {
            get
            {
                Vector2 heights = HeightRange;
                float height = Mathf.Max(1f, heights.y - heights.x);
                Vector3 center = new(0f, heights.x + height * 0.5f, 0f);
                return new Bounds(center, new Vector3(Size, height, Size));
            }
        }

        public static bool Contains(Vector3 worldPosition)
        {
            float half = Size * 0.5f;

            return worldPosition.x >= -half && worldPosition.x <= half
                && worldPosition.z >= -half && worldPosition.z <= half;
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError($"MatchArena: в сцене уже есть арена {_instance.name} — вторая проигнорирована", this);
                return;
            }

            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);

            Vector2 heights = heightRange;
            float height = Mathf.Max(1f, heights.y - heights.x);
            Vector3 center = new(0f, heights.x + height * 0.5f, 0f);

            Gizmos.DrawWireCube(center, new Vector3(size, height, size));
        }
    }
}
