using UnityEngine;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Точки базы игрока в мировых координатах. Снимок с объекта
    /// <see cref="Warlord.Gameplay.World.PlayerBase"/> из сцены, чтобы код спавна и зон
    /// не дёргал сцену за каждой мелочью и работал с обычной структурой.
    /// </summary>
    public readonly struct PlayerBaseAnchor
    {
        public readonly Vector3 Center;
        public readonly Vector3 UnitSpawnPoint;
        public readonly Vector3 HeroSpawnPoint;
        public readonly float YawDegrees;

        /// <summary>Радиус зоны покупки вокруг базы. Задаётся на самой базе, а не в конфиге полководца.</summary>
        public readonly float BuyZoneRadius;

        public PlayerBaseAnchor(
            Vector3 center,
            Vector3 unitSpawnPoint,
            Vector3 heroSpawnPoint,
            float yawDegrees,
            float buyZoneRadius)
        {
            Center = center;
            UnitSpawnPoint = unitSpawnPoint;
            HeroSpawnPoint = heroSpawnPoint;
            YawDegrees = yawDegrees;
            BuyZoneRadius = buyZoneRadius;
        }
    }
}
