using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Клетка поля расстановки. Ловит нажатие и наведение с зажатой кнопкой, а не обычный
    /// клик: расставлять двадцать юнитов по одному клику неудобно, и мазок мышью здесь
    /// экономит игроку половину работы.
    /// </summary>
    public sealed class ArmyPresetCellView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;

        [Header("Цвета")]
        [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color filledColor = new(1f, 1f, 1f, 0.9f);

        private Action<ArmyPresetCellView, bool> _onPaint;

        /// <summary>Колонка относительно центра строя: 0 — по оси, минус — влево.</summary>
        public int Column { get; private set; }

        /// <summary>Шеренга: 0 — передняя.</summary>
        public int Row { get; private set; }

        /// <summary>Тип юнита в клетке или -1, если она пуста.</summary>
        public int RosterIndex { get; private set; } = -1;

        public void Bind(int column, int row, Action<ArmyPresetCellView, bool> onPaint)
        {
            Column = column;
            Row = row;
            _onPaint = onPaint;

            SetContent(-1, null, Color.white);
        }

        public void SetContent(int rosterIndex, Sprite sprite, Color tint)
        {
            RosterIndex = rosterIndex;

            bool filled = rosterIndex >= 0;

            if (background != null)
                background.color = filled ? filledColor : emptyColor;

            if (icon == null)
                return;

            icon.sprite = sprite;
            icon.color = tint;
            icon.enabled = filled;
        }

        public void OnPointerDown(PointerEventData eventData) => _onPaint?.Invoke(this, true);

        public void OnPointerEnter(PointerEventData eventData) => _onPaint?.Invoke(this, false);
    }
}
