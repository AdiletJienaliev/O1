using UnityEngine;
using Warlord.Core;

namespace Warlord.Gameplay.Army
{
    /// <summary>
    /// Приказ всей армии (ГДД §6): вся армия игрока — один отряд, приказ один на всех.
    /// Структура иммутабельна, чтобы её можно было безопасно копировать в поведения.
    /// </summary>
    public readonly struct ArmyOrder
    {
        public readonly ArmyOrderType Type;

        /// <summary>Точка, где приказ был отдан. Для «За мной» игнорируется — якорь берётся от полководца.</summary>
        public readonly Vector3 AnchorPosition;

        /// <summary>Куда развёрнут строй, град.</summary>
        public readonly float AnchorYaw;

        public ArmyOrder(ArmyOrderType type, Vector3 anchorPosition, float anchorYaw)
        {
            Type = type;
            AnchorPosition = anchorPosition;
            AnchorYaw = anchorYaw;
        }

        public static ArmyOrder HoldAt(Vector3 position, float yaw) => new(ArmyOrderType.HoldGround, position, yaw);
    }

    /// <summary>Источник якоря построения при приказе «За мной».</summary>
    public interface IArmyLeader
    {
        bool IsAlive { get; }
        Vector3 Position { get; }
        float YawDegrees { get; }
    }
}
