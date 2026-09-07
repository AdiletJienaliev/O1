using UnityEngine;
using Warlord.Core;
using Warlord.Gameplay.Players;

namespace Warlord.Gameplay.World
{
    /// <summary>
    /// База игрока, расставленная прямо в сцене. Раньше эти точки жили в MapConfig,
    /// и любая правка карты означала синхронную правку ассета вслепую: в ассете были
    /// координаты, а в сцене — стены и ворота, и совпадали они только на честном слове.
    /// Теперь источник один — сам объект в сцене.
    ///
    /// Базы регистрируются статически по слоту: серверу они нужны до того, как появится
    /// хоть один игрок, и искать их поиском по сцене каждый раз незачем.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerBase : MonoBehaviour
    {
        private static readonly PlayerBase[] BySlot = new PlayerBase[PlayerSlots.MaxSupported];

        [Header("Владелец")]
        [Tooltip("Слот игрока, которому принадлежит база. 0..3, у каждой базы свой.")]
        [Range(0, PlayerSlots.MaxSupported - 1)]
        [SerializeField] private int slot;

        [Header("Точки появления")]
        [Tooltip("Где появляется и куда респавнится полководец. Пусто — центр самой базы.")]
        [SerializeField] private Transform heroSpawnPoint;

        [Tooltip("Где появляются купленные юниты. Пусто — центр самой базы.")]
        [SerializeField] private Transform unitSpawnPoint;

        [Header("Зона")]
        [Tooltip("Радиус зоны покупки вокруг центра базы, м.")]
        [Min(1f)] [SerializeField] private float buyZoneRadius = 12f;

        public int Slot => slot;

        /// <summary>Центр крепости: он же центр зоны покупки и лечения.</summary>
        public Vector3 Center => transform.position;

        /// <summary>
        /// Куда развёрнута база. Поворачивается сам объект в сцене — по этому же углу
        /// разворачивается строй армии на старте, поэтому базу стоит повернуть к центру карты.
        /// </summary>
        public float YawDegrees => transform.eulerAngles.y;

        public Vector3 HeroSpawnPoint => heroSpawnPoint != null ? heroSpawnPoint.position : Center;

        public Vector3 UnitSpawnPoint => unitSpawnPoint != null ? unitSpawnPoint.position : Center;

        public float BuyZoneRadius => buyZoneRadius;

        public PlayerBaseAnchor ToAnchor()
        {
            return new PlayerBaseAnchor(Center, UnitSpawnPoint, HeroSpawnPoint, YawDegrees, buyZoneRadius);
        }

        /// <summary>База слота или null. Возвращает null и когда база просто не расставлена в сцене.</summary>
        public static PlayerBase Get(int slot)
        {
            return PlayerSlots.IsValid(slot) ? BySlot[slot] : null;
        }

        /// <summary>Точки базы слота. Без базы в сцене — нули: спавн уедет в начало координат, и это видно сразу.</summary>
        public static PlayerBaseAnchor GetAnchor(int slot)
        {
            PlayerBase found = Get(slot);
            return found != null ? found.ToAnchor() : default;
        }

        private void OnEnable()
        {
            if (!PlayerSlots.IsValid(slot))
            {
                Debug.LogError($"PlayerBase: слот {slot} вне диапазона 0..{PlayerSlots.MaxSupported - 1}", this);
                return;
            }

            PlayerBase existing = BySlot[slot];

            if (existing != null && existing != this)
            {
                Debug.LogError($"PlayerBase: слот {slot} уже занят базой {existing.name} — вторая база проигнорирована", this);
                return;
            }

            BySlot[slot] = this;
        }

        private void OnDisable()
        {
            if (PlayerSlots.IsValid(slot) && BySlot[slot] == this)
                BySlot[slot] = null;
        }

        private void OnDrawGizmos()
        {
            Color slotColor = SlotGizmoColor(slot);

            Gizmos.color = new Color(slotColor.r, slotColor.g, slotColor.b, 0.35f);
            DrawCircle(Center, buyZoneRadius);

            Gizmos.color = slotColor;
            Gizmos.DrawLine(Center, Center + transform.forward * 4f);

            DrawPoint(HeroSpawnPoint, 1.1f, slotColor);
            DrawPoint(UnitSpawnPoint, 0.7f, Color.white);
        }

        private static void DrawPoint(Vector3 position, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(position, radius);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            const int Segments = 32;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        /// <summary>Цвет слота для гизмо. Повторяет порядок TeamColorConfig, но без зависимости от ассета.</summary>
        private static Color SlotGizmoColor(int slot)
        {
            switch (slot)
            {
                case 0: return new Color(0.16f, 0.45f, 0.95f);
                case 1: return new Color(0.9f, 0.22f, 0.2f);
                case 2: return new Color(0.25f, 0.78f, 0.35f);
                case 3: return new Color(0.95f, 0.82f, 0.2f);
                default: return Color.gray;
            }
        }
    }
}
