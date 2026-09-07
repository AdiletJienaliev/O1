using UnityEngine;

namespace Warlord.Networking.Sync
{
    /// <summary>
    /// Квантизация позиции и поворота юнита (ГДД §12): 16 бит на ось и 1 байт на поворот вокруг Y.
    /// На арене 160 м это даёт точность около 2.5 мм — визуально неотличимо, но втрое дешевле float.
    /// </summary>
    public readonly struct TransformQuantizer
    {
        private readonly Vector3 _min;
        private readonly Vector3 _size;

        /// <param name="arenaSize">Сторона арены, м. Задаётся в сцене компонентом MatchArena.</param>
        public TransformQuantizer(float arenaSize, Vector2 heightRange)
        {
            float half = Mathf.Max(1f, arenaSize) * 0.5f;

            _min = new Vector3(-half, heightRange.x, -half);
            _size = new Vector3(half * 2f, Mathf.Max(1f, heightRange.y - heightRange.x), half * 2f);
        }

        public void Encode(Vector3 position, out ushort x, out ushort y, out ushort z)
        {
            x = EncodeAxis(position.x, _min.x, _size.x);
            y = EncodeAxis(position.y, _min.y, _size.y);
            z = EncodeAxis(position.z, _min.z, _size.z);
        }

        public Vector3 Decode(ushort x, ushort y, ushort z)
        {
            return new Vector3(
                DecodeAxis(x, _min.x, _size.x),
                DecodeAxis(y, _min.y, _size.y),
                DecodeAxis(z, _min.z, _size.z));
        }

        public static byte EncodeYaw(float yawDegrees)
        {
            float normalized = Mathf.Repeat(yawDegrees, 360f) / 360f;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(normalized * 255f), 0, 255);
        }

        public static float DecodeYaw(byte yaw) => yaw / 255f * 360f;

        private static ushort EncodeAxis(float value, float min, float size)
        {
            float normalized = Mathf.Clamp01((value - min) / size);
            return (ushort)Mathf.RoundToInt(normalized * ushort.MaxValue);
        }

        private static float DecodeAxis(ushort value, float min, float size)
        {
            return min + value / (float)ushort.MaxValue * size;
        }
    }
}
