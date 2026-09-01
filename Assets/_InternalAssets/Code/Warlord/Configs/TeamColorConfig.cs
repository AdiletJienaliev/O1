using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs
{
    /// <summary>
    /// Цвета и символы игроков (ГДД §13). Цвет — не единственный различитель:
    /// у каждого слота есть свой символ (sigil) для дальтоников.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Team Colors", fileName = "TeamColorConfig")]
    public sealed class TeamColorConfig : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string colorId;
            public string displayName;
            public Color primaryColor;
            public Color outlineColor;
            public Sprite sigilIcon;
            public Sprite minimapIcon;
        }

        [SerializeField]
        private Entry[] entries = new Entry[PlayerSlots.MaxSupported];

        [Tooltip("Имя свойства цвета в шейдере. Красим через MaterialPropertyBlock, а не копией материала.")]
        public string shaderColorProperty = "_TeamColor";

        public int Count => entries != null ? entries.Length : 0;

        public bool IsValidSlot(int slot) => entries != null && slot >= 0 && slot < entries.Length;

        public Entry Get(int slot) => IsValidSlot(slot) ? entries[slot] : default;

        public Color GetPrimary(int slot) => IsValidSlot(slot) ? entries[slot].primaryColor : Color.gray;
    }
}
