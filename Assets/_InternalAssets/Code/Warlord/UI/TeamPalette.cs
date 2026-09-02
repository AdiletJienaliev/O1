using UnityEngine;
using Warlord.Configs;
using Warlord.Core;

namespace Warlord.UI
{
    /// <summary>
    /// Цвет слота для интерфейса. Берётся из <see cref="TeamColorConfig"/>, но UI обязан
    /// оставаться читаемым и когда ассет не заполнен — поэтому есть запасная палитра.
    /// </summary>
    public static class TeamPalette
    {
        private static readonly Color[] Fallback =
        {
            new(0.29f, 0.62f, 0.96f), // синий
            new(0.94f, 0.35f, 0.35f), // красный
            new(0.42f, 0.82f, 0.44f), // зелёный
            new(0.98f, 0.76f, 0.28f)  // жёлтый
        };

        private static readonly Color Neutral = new(0.63f, 0.66f, 0.72f);

        public static Color Primary(TeamColorConfig config, int slot)
        {
            if (!PlayerSlots.IsValid(slot))
                return Neutral;

            if (config != null && config.IsValidSlot(slot))
            {
                Color color = config.GetPrimary(slot);

                // Незаполненная запись ассета приходит прозрачно-чёрной: на такой цвет
                // ориентироваться нельзя, иначе половина HUD станет невидимой.
                if (color.a > 0.01f && (color.r + color.g + color.b) > 0.01f)
                    return color;
            }

            return Fallback[slot % Fallback.Length];
        }

        public static Sprite Sigil(TeamColorConfig config, int slot)
        {
            return config != null && config.IsValidSlot(slot) ? config.Get(slot).sigilIcon : null;
        }

        /// <summary>Затемнённый вариант — для фона строки под цвет игрока.</summary>
        public static Color Dim(Color color, float alpha = 0.35f)
        {
            return new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, alpha);
        }
    }
}
