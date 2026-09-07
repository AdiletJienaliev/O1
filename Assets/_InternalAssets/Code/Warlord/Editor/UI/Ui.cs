using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Примитивы сборки интерфейса. Билдер описывает раскладку, а вся возня с
    /// RectTransform, якорями и компонентами живёт здесь — иначе разметку невозможно читать.
    /// </summary>
    public static class Ui
    {
        public static readonly Vector2 TopLeft = new(0f, 1f);
        public static readonly Vector2 Top = new(0.5f, 1f);
        public static readonly Vector2 TopRight = new(1f, 1f);
        public static readonly Vector2 Left = new(0f, 0.5f);
        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Vector2 Right = new(1f, 0.5f);
        public static readonly Vector2 BottomLeft = new(0f, 0f);
        public static readonly Vector2 Bottom = new(0.5f, 0f);
        public static readonly Vector2 BottomRight = new(1f, 0f);

        #region Палитра

        public static readonly Color Ink = new(0.96f, 0.97f, 1f);
        public static readonly Color InkMuted = new(0.68f, 0.72f, 0.8f);
        public static readonly Color Gold = new(0.99f, 0.85f, 0.35f);
        public static readonly Color Danger = new(0.94f, 0.42f, 0.4f);
        public static readonly Color Good = new(0.55f, 0.87f, 0.55f);
        public static readonly Color Panel = new(1f, 1f, 1f, 0.97f);
        public static readonly Color Shade = new(0f, 0f, 0f, 0.55f);

        #endregion

        #region Узлы

        public static RectTransform Node(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;

            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = Center;
            rect.anchorMax = Center;
            rect.pivot = Center;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);

            return rect;
        }

        /// <summary>Ставит узел в точку якоря. Пивот по умолчанию совпадает с якорем — так проще отступать от края.</summary>
        public static RectTransform At(this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Pivot(this RectTransform rect, Vector2 pivot)
        {
            rect.pivot = pivot;
            return rect;
        }

        /// <summary>Растягивает узел по родителю с отступами.</summary>
        public static RectTransform Stretch(this RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        #endregion

        #region Графика

        public static Image Sprite(this RectTransform rect, Sprite sprite, Color color, Image.Type type = Image.Type.Sliced)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        public static Image Panelled(string name, Transform parent, Sprite sprite, Color color)
        {
            return Node(name, parent).Sprite(sprite, color);
        }

        public static Image Icon(string name, Transform parent, Sprite sprite, Vector2 size, Color? tint = null)
        {
            RectTransform rect = Node(name, parent);
            rect.sizeDelta = size;

            Image image = rect.Sprite(sprite, tint ?? Color.white, Image.Type.Simple);
            image.preserveAspect = true;
            return image;
        }

        public static TextMeshProUGUI Label(
            string name,
            Transform parent,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            TMP_FontAsset font = null)
        {
            RectTransform rect = Node(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset resolved = font != null ? font : Kit.FontBody;
            if (resolved != null)
                label.font = resolved;

            return label;
        }

        #endregion

        #region Кнопки и полосы

        /// <summary>Кнопка со спрайтом набора: подсветка нажатия делается цветом, отдельных спрайтов состояний в наборе нет.</summary>
        public static Button Button(string name, Transform parent, Sprite sprite, Color color, out Image background)
        {
            RectTransform rect = Node(name, parent);

            background = rect.Sprite(sprite, color);
            background.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            return button;
        }

        /// <summary>
        /// Полоса прогресса на Image с заливкой. Дешевле Slider и не тянет за собой
        /// обработку ввода — для индикаторов это всё, что нужно.
        /// </summary>
        public static Image Bar(string name, Transform parent, Sprite frame, Sprite fillSprite, Color fillColor, out Image fill)
        {
            RectTransform rect = Node(name, parent);
            Image background = rect.Sprite(frame, Color.white);

            RectTransform fillRect = Node("Fill", rect).Stretch(4f, 4f, 4f, 4f);
            fill = fillRect.Sprite(fillSprite, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            return background;
        }

        /// <summary>Полноценный Slider — нужен там, где значение читает и меняет игрок (настройки лобби).</summary>
        public static Slider Slider(string name, Transform parent, Sprite frame, Sprite fillSprite, Color fillColor, out Image fill)
        {
            RectTransform rect = Node(name, parent);
            rect.Sprite(frame, Color.white).raycastTarget = true;

            RectTransform fillArea = Node("Fill_Area", rect).Stretch(4f, 4f, 4f, 4f);
            RectTransform fillRect = Node("Fill", fillArea).Stretch();

            fill = fillRect.Sprite(fillSprite, fillColor);

            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fill;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;

            return slider;
        }

        #endregion

        #region Раскладка

        public static HorizontalLayoutGroup Row(this RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft, RectOffset padding = null)
        {
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.padding = padding ?? new RectOffset();
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            return layout;
        }

        public static VerticalLayoutGroup Column(this RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.UpperLeft, RectOffset padding = null)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.padding = padding ?? new RectOffset();
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            return layout;
        }

        /// <summary>Сетка фиксированной ширины: поле расстановки армии кладётся именно так.</summary>
        public static GridLayoutGroup Grid(
            this RectTransform rect,
            Vector2 cellSize,
            Vector2 spacing,
            int columns,
            TextAnchor alignment = TextAnchor.UpperCenter)
        {
            GridLayoutGroup layout = rect.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cellSize;
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            return layout;
        }

        public static ContentSizeFitter Fit(this RectTransform rect, ContentSizeFitter.FitMode horizontal, ContentSizeFitter.FitMode vertical)
        {
            ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal;
            fitter.verticalFit = vertical;
            return fitter;
        }

        public static CanvasGroup Group(this RectTransform rect, float alpha = 1f)
        {
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            return group;
        }

        #endregion

        #region Мелочи

        /// <summary>Тёмная плашка под содержимое: спрайт набора плюс мягкая тень читаемости.</summary>
        public static Image Backdrop(string name, Transform parent, Color color)
        {
            RectTransform rect = Node(name, parent);
            Image image = rect.Sprite(Kit.PanelSmall, color);
            return image;
        }

        public static Shadow Shadow(this Graphic graphic, Color color, Vector2 distance)
        {
            Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            return shadow;
        }

        public static Outline Outline(this Graphic graphic, Color color, Vector2 distance)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            return outline;
        }

        #endregion
    }
}
