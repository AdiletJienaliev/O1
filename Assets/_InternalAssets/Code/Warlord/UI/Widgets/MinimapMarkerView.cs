using UnityEngine;
using UnityEngine.UI;

namespace Warlord.UI.Widgets
{
    /// <summary>Одна метка на миникарте: точка нужного цвета и необязательная иконка поверх.</summary>
    public sealed class MinimapMarkerView : MonoBehaviour
    {
        [SerializeField] private RectTransform rect;
        [SerializeField] private Image dot;
        [SerializeField] private Image icon;
        [SerializeField] private Image ring;

        public RectTransform Rect => rect != null ? rect : (RectTransform)transform;

        public void Apply(Vector2 anchoredPosition, Color color, Sprite sprite, float scale, bool highlighted)
        {
            RectTransform target = Rect;
            target.anchoredPosition = anchoredPosition;
            target.localScale = new Vector3(scale, scale, 1f);

            // Метки переиспользуются из пула, поэтому поворот сбрасывается здесь: иначе
            // повёрнутая под героя метка на следующем кадре достаётся флагу.
            target.localRotation = Quaternion.identity;

            if (dot != null)
                dot.color = color;

            if (ring != null)
                ring.enabled = highlighted;

            if (icon == null)
                return;

            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        /// <summary>Разворот метки, град. по часовой: миникарта показывает, куда смотрит игрок.</summary>
        public void SetRotation(float degrees) => Rect.localRotation = Quaternion.Euler(0f, 0f, -degrees);

        public void Show(bool value)
        {
            if (gameObject.activeSelf != value)
                gameObject.SetActive(value);
        }
    }
}
