using UnityEngine;

namespace Warlord.UI.Screens
{
    /// <summary>
    /// Экран интерфейса. Показ и скрытие идут через CanvasGroup, а не через SetActive:
    /// объекты остаются живыми, подписки на события не рвутся, и экран может
    /// продолжать слушать матч, пока не виден.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UiScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;

        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            group ??= GetComponent<CanvasGroup>();
        }

        public void Show()
        {
            if (IsVisible)
                return;

            IsVisible = true;
            ApplyVisibility(true);
            OnShown();
        }

        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            ApplyVisibility(false);
            OnHidden();
        }

        protected virtual void OnShown() { }

        protected virtual void OnHidden() { }

        private void ApplyVisibility(bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
