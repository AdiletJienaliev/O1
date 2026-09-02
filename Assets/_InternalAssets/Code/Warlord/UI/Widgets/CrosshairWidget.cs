using UnityEngine;
using Warlord.Gameplay.Players;
using Warlord.Presentation;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Прицел в центре экрана. Нужен ровно тогда, когда курсор захвачен камерой:
    /// приказ по ЛКМ уходит именно в эту точку, и игрок должен её видеть.
    /// </summary>
    public sealed class CrosshairWidget : HudWidget
    {
        [SerializeField] private GameObject root;

        private HeroOrbitCamera _camera;

        public override void Refresh(PlayerState player)
        {
            if (root == null)
                return;

            // Камера — объект сцены, живёт дольше матча: ищем её, пока не найдём, и запоминаем.
            if (_camera == null)
                _camera = Object.FindAnyObjectByType<HeroOrbitCamera>();

            bool visible = _camera != null && _camera.CursorLocked;

            if (root.activeSelf != visible)
                root.SetActive(visible);
        }
    }
}
