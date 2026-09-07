using UnityEngine;
using Warlord.Core;

namespace Warlord.Presentation
{
    /// <summary>
    /// Красит модель в цвет владельца (ГДД §13). Цвет ставится через MaterialPropertyBlock,
    /// а не заменой материала: копия материала на каждого юнита ломает батчинг и при
    /// восьмидесяти юнитах это уже заметно.
    ///
    /// Слот приезжает по сети, поэтому объект в момент создания ещё ничей: компонент ждёт
    /// валидный слот, красит один раз и после этого перестаёт что-либо делать.
    /// </summary>
    public sealed class TeamColorApplier : MonoBehaviour
    {
        [Header("Что красить")]
        [Tooltip("Рендереры модели. Пусто — берутся все MeshRenderer и SkinnedMeshRenderer в потомках.")]
        [SerializeField] private Renderer[] renderers;

        [Header("Шейдер")]
        [Tooltip("Свойства цвета, которые выставляются. Лишние имена безвредны: блок их просто игнорирует.")]
        [SerializeField] private string[] colorProperties = { "_BaseColor", "_Color", "_TeamColor" };

        [Tooltip("Насколько сильно цвет команды перебивает исходный цвет материала. 1 — полностью.")]
        [Range(0f, 1f)] [SerializeField] private float strength = 1f;

        private MaterialPropertyBlock _block;
        private int _appliedSlot = PlayerSlots.None;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate()
        {
            if (!TeamSlot.ColorsReady)
                return;

            int slot = TeamSlot.Resolve(gameObject);

            if (slot == _appliedSlot || !PlayerSlots.IsValid(slot))
                return;

            Apply(slot);
            _appliedSlot = slot;
        }

        /// <summary>Перекрасить принудительно — например, после смены модели или материала.</summary>
        public void Refresh()
        {
            _appliedSlot = PlayerSlots.None;
        }

        private void Apply(int slot)
        {
            if (renderers == null || colorProperties == null)
                return;

            Color teamColor = TeamSlot.ResolveColor(slot);
            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_block);

                for (int p = 0; p < colorProperties.Length; p++)
                {
                    string property = colorProperties[p];
                    if (string.IsNullOrEmpty(property))
                        continue;

                    _block.SetColor(property, Blend(renderer, property, teamColor));
                }

                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// Смешивание с исходным цветом материала. При strength = 1 это просто цвет команды;
        /// меньшие значения оставляют текстуре её собственный оттенок и годятся для моделей,
        /// где команда должна читаться, но не закрашивать всё целиком.
        /// </summary>
        private Color Blend(Renderer renderer, string property, Color teamColor)
        {
            if (strength >= 0.999f)
                return teamColor;

            Material material = renderer.sharedMaterial;
            Color original = material != null && material.HasProperty(property)
                ? material.GetColor(property)
                : Color.white;

            return Color.Lerp(original, teamColor, strength);
        }
    }
}
