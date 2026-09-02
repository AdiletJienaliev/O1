using UnityEngine.EventSystems;

namespace Warlord.Core
{
    /// <summary>
    /// Кто сейчас владеет мышью — интерфейс или игровой мир. Одно место на весь проект,
    /// иначе камера, ввод полководца и HUD начинают спорить за курсор и за один и тот же клик.
    /// </summary>
    public static class InputFocus
    {
        /// <summary>
        /// Интерфейс требует свободный курсор: открыт экран меню, лобби, итогов или панель прокачки.
        /// Ставится экранами HUD, читается камерой и вводом.
        /// </summary>
        public static bool UiCapturesCursor { get; set; }

        /// <summary>
        /// Нельзя отдавать клик миру: либо открыт экран, либо указатель висит над кнопкой HUD.
        /// Проверка указателя нужна только при свободном курсоре — с захваченным он всегда в центре.
        /// </summary>
        public static bool BlocksWorldInput
        {
            get
            {
                if (UiCapturesCursor)
                    return true;

                EventSystem events = EventSystem.current;
                return events != null && events.IsPointerOverGameObject();
            }
        }

        /// <summary>Сброс при выходе из матча: статика переживает смену сцены.</summary>
        public static void Reset() => UiCapturesCursor = false;
    }
}
