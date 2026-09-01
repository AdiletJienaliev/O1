using UnityEngine;
using Warlord.Configs;

namespace Warlord.Gameplay.Players
{
    /// <summary>
    /// Точки базы игрока в мировых координатах. Снимок из <see cref="MapConfig"/>,
    /// чтобы код спавна и зон не лазил в конфиг за каждой мелочью.
    /// </summary>
    public readonly struct PlayerBaseAnchor
    {
        public readonly Vector3 Center;
        public readonly Vector3 UnitSpawnPoint;
        public readonly Vector3 HeroSpawnPoint;
        public readonly float YawDegrees;

        public PlayerBaseAnchor(Vector3 center, Vector3 unitSpawnPoint, Vector3 heroSpawnPoint, float yawDegrees)
        {
            Center = center;
            UnitSpawnPoint = unitSpawnPoint;
            HeroSpawnPoint = heroSpawnPoint;
            YawDegrees = yawDegrees;
        }

        public static PlayerBaseAnchor FromMap(MapConfig map, int slot)
        {
            if (map == null || !map.HasBase(slot))
                return new PlayerBaseAnchor(Vector3.zero, Vector3.zero, Vector3.zero, 0f);

            MapConfig.BaseAnchor anchor = map.GetBase(slot);
            return new PlayerBaseAnchor(anchor.position, anchor.unitSpawnPoint, anchor.heroSpawnPoint, anchor.yawDegrees);
        }
    }
}
