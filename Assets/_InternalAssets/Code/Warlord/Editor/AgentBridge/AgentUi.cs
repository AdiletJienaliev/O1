using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warlord.EditorTools.Agent
{
    /// <summary>Чтение и нажатие UGUI-интерфейса из моста: агент «видит» экран и кликает по нему.</summary>
    internal static class AgentUi
    {
        static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        /// <summary>Подпись элемента: собственный текст или первый текст среди детей.</summary>
        static string LabelOf(Component c)
        {
            var own = c.GetComponent<TMP_Text>();
            if (own != null && !string.IsNullOrWhiteSpace(own.text)) return own.text;

            foreach (var text in c.GetComponentsInChildren<TMP_Text>(false))
                if (!string.IsNullOrWhiteSpace(text.text)) return text.text;

            foreach (var text in c.GetComponentsInChildren<Text>(false))
                if (!string.IsNullOrWhiteSpace(text.text)) return text.text;

            return null;
        }

        static bool OnScreen(RectTransform rect)
        {
            if (rect == null) return false;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Vector3.Distance(corners[0], corners[2]) > 0.5f;
        }

        public static void Dump(AgentJson j, bool includeTexts)
        {
            var selectables = UnityEngine.Object
                .FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(s => s.gameObject.activeInHierarchy)
                .ToList();

            j.BeginArr("controls");
            foreach (var selectable in selectables)
            {
                var rect = selectable.transform as RectTransform;
                if (!OnScreen(rect)) continue;

                j.BeginObj();
                j.Str("path", PathOf(selectable.transform));
                j.Str("kind", selectable.GetType().Name);
                j.Str("label", LabelOf(selectable));
                j.Bool("interactable", selectable.IsInteractable());

                if (rect != null)
                {
                    var center = rect.TransformPoint(rect.rect.center);
                    j.Num("x", center.x).Num("y", center.y);
                }
                j.EndObj();
            }
            j.EndArr();

            if (!includeTexts) return;

            j.BeginArr("texts");
            foreach (var text in UnityEngine.Object
                         .FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                         .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text)))
            {
                if (!OnScreen(text.transform as RectTransform)) continue;
                j.BeginObj().Str("path", PathOf(text.transform)).Str("text", text.text).EndObj();
            }
            j.EndArr();
        }

        /// <summary>Возвращает null при успехе, иначе текст ошибки.</summary>
        public static string Click(string path, string label, out string clicked)
        {
            clicked = null;

            var candidates = UnityEngine.Object
                .FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(s => s.gameObject.activeInHierarchy)
                .ToList();

            Selectable target = null;

            if (!string.IsNullOrEmpty(path))
            {
                target = candidates.FirstOrDefault(s => PathOf(s.transform) == path)
                         ?? candidates.FirstOrDefault(s => s.name == path);
            }
            else if (!string.IsNullOrEmpty(label))
            {
                target = candidates.FirstOrDefault(s =>
                {
                    string text = LabelOf(s);
                    return text != null && text.Trim().Equals(label.Trim(), StringComparison.OrdinalIgnoreCase);
                }) ?? candidates.FirstOrDefault(s =>
                {
                    string text = LabelOf(s);
                    return text != null && text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }
            else
            {
                return "нужен path= или label=";
            }

            if (target == null)
                return $"элемент не найден: {path ?? label}";

            if (!target.IsInteractable())
                return $"элемент выключен (interactable=false): {PathOf(target.transform)}";

            clicked = PathOf(target.transform);

            var go = target.gameObject;
            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
            };

            // Полный цикл указателя: часть виджетов слушает down/up, а не только click.
            ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerExitHandler);

            return null;
        }
    }
}
