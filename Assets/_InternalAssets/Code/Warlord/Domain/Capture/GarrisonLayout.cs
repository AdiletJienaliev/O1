using UnityEngine;

namespace Warlord.Domain.Capture
{
    /// <summary>
    /// Раскладка слотов гарнизона по кольцу вокруг точки (ГДД §1.7).
    ///
    /// Слоты считаются формулой, а не решателем построений, и это принципиально:
    /// гарнизон не перестраивается при смерти соседа. Слот погибшего просто остаётся
    /// пустым до нового спавна, и в обороне видна брешь — именно так игрок понимает,
    /// что точку пора подкрепить, не открывая ни одной панели.
    /// </summary>
    public static class GarrisonLayout
    {
        /// <summary>Позиция слота на кольце. Слоты равномерно разнесены по окружности.</summary>
        public static Vector3 SlotPosition(Vector3 center, float ringRadius, int slotIndex, int slotCount)
        {
            if (slotCount <= 0)
                return center;

            float angle = slotIndex / (float)slotCount * Mathf.PI * 2f;

            return center + new Vector3(Mathf.Sin(angle) * ringRadius, 0f, Mathf.Cos(angle) * ringRadius);
        }

        /// <summary>
        /// Куда смотрит охранник в слоте: наружу от центра точки. Врага он встречает лицом,
        /// а не спиной, ещё до того, как тот вошёл в радиус агро.
        /// </summary>
        public static Vector3 SlotFacing(Vector3 center, Vector3 slotPosition)
        {
            Vector3 outward = slotPosition - center;
            outward.y = 0f;

            return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
        }

        /// <summary>Угол разворота слота в градусах — им же разворачивается охранник при спавне.</summary>
        public static float SlotYaw(Vector3 center, Vector3 slotPosition)
        {
            Vector3 facing = SlotFacing(center, slotPosition);
            return Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        }
    }
}
