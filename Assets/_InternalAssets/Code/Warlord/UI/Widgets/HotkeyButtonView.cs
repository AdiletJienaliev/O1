using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Кнопка с горячей клавишей и состоянием «выбрано». Используется приказами армии
    /// и построениями: у них одинаковое поведение и разный набор данных.
    /// </summary>
    public sealed class HotkeyButtonView : MonoBehaviour
    {
        [Header("Содержимое")]
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private TextMeshProUGUI hotkeyLabel;

        [Header("Состояние")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private GameObject selectedFrame;

        [Header("Цвета фона")]
        [SerializeField] private Color idleColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color selectedColor = new(0.99f, 0.85f, 0.35f);

        private bool _selected;
        private bool _stateApplied;

        public void Bind(string text, string hotkey, Sprite sprite, Action onClick)
        {
            if (label != null)
                label.text = text;

            if (hotkeyLabel != null)
            {
                hotkeyLabel.text = hotkey;
                hotkeyLabel.gameObject.SetActive(!string.IsNullOrEmpty(hotkey));
            }

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        public void SetSelected(bool selected)
        {
            if (_stateApplied && selected == _selected)
                return;

            _selected = selected;
            _stateApplied = true;

            if (background != null)
                background.color = selected ? selectedColor : idleColor;

            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        public void SetInteractable(bool value)
        {
            if (button != null)
                button.interactable = value;
        }
    }
}
