using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs
{
    /// <summary>
    /// Геометрия карты (ГДД §3, §15). Хранит только логические точки — расстановка визуала
    /// делается в сцене, а сервер использует эти данные для спавна и для квантизации позиций.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Map", fileName = "MapConfig")]
    public sealed class MapConfig : ScriptableObject
    {
        [System.Serializable]
        public struct BaseAnchor
        {
            [Tooltip("Центр крепости: он же центр зоны покупки и флага базы.")]
            public Vector3 position;

            [Tooltip("Направление, куда смотрит база (в сторону центра карты).")]
            public float yawDegrees;

            [Tooltip("Точка появления купленных юнитов.")]
            public Vector3 unitSpawnPoint;

            [Tooltip("Точка респавна полководца.")]
            public Vector3 heroSpawnPoint;
        }

        [Header("Идентификация")]
        public string mapId = "arena_160";
        public string displayName = "Арена";

        [Header("Размеры")]
        [Tooltip("Сторона квадратной арены, м. Используется для квантизации позиций в сети.")]
        [Min(16f)] public float size = 160f;

        [Tooltip("Границы по высоте для квантизации, м.")]
        public Vector2 heightRange = new(-20f, 60f);

        [Header("Точки")]
        public Vector3 centerFlagPosition = Vector3.zero;

        [SerializeField] private BaseAnchor[] baseAnchors = new BaseAnchor[PlayerSlots.MaxSupported];

        public int BaseCount => baseAnchors != null ? baseAnchors.Length : 0;

        public bool HasBase(int slot) => baseAnchors != null && slot >= 0 && slot < baseAnchors.Length;

        public BaseAnchor GetBase(int slot) => HasBase(slot) ? baseAnchors[slot] : default;

        /// <summary>Границы арены в мировых координатах. Сервер отбрасывает приказы за их пределами.</summary>
        public Bounds WorldBounds
        {
            get
            {
                float height = Mathf.Max(1f, heightRange.y - heightRange.x);
                Vector3 center = new(0f, heightRange.x + height * 0.5f, 0f);
                return new Bounds(center, new Vector3(size, height, size));
            }
        }

        public bool Contains(Vector3 worldPosition)
        {
            float half = size * 0.5f;
            return worldPosition.x >= -half && worldPosition.x <= half
                && worldPosition.z >= -half && worldPosition.z <= half;
        }
    }
}
