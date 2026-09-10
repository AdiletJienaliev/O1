using UnityEngine;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.World;

namespace Warlord.Presentation
{
    /// <summary>
    /// Круг зоны покупки вокруг своей базы. До этого граница зоны существовала только
    /// в виде гизмо в редакторе: в игре про неё узнавали по красной надписи «покупка
    /// доступна только на своей базе» — то есть уже после неудачной попытки купить.
    ///
    /// Показывается кольцо только своей базы. Чужие зоны игроку ничего не решают:
    /// базы и так на виду, а четыре круга на арене — лишний шум.
    /// </summary>
    public sealed class BuyZoneView : MonoBehaviour
    {
        [SerializeField] private PlayerBase owner;
        [SerializeField] private Renderer ring;
        [SerializeField] private Transform ringTransform;

        [Tooltip("Прозрачность обода. Ниже, чем у колец точек захвата: зона покупки — "
                 + "справочная разметка, а не цель, и перетягивать внимание не должна.")]
        [SerializeField] private float alpha = 0.42f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _block;
        private float _appliedRadius = -1f;

        private void Awake()
        {
            owner ??= GetComponentInParent<PlayerBase>();
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (ring == null || owner == null)
                return;

            HeroController hero = HeroController.Local;
            bool mine = hero != null && hero.IsAlive && hero.Slot == owner.Slot;

            if (ring.enabled != mine)
                ring.enabled = mine;

            if (!mine)
                return;

            ApplyRadius();

            Color color = TeamSlot.ColorsReady ? TeamSlot.ResolveColor(owner.Slot) : Color.white;
            color.a = alpha;

            ring.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            ring.SetPropertyBlock(_block);
        }

        private void ApplyRadius()
        {
            if (ringTransform == null || Mathf.Approximately(owner.BuyZoneRadius, _appliedRadius))
                return;

            _appliedRadius = owner.BuyZoneRadius;

            Vector3 scale = ringTransform.localScale;
            scale.x = _appliedRadius * 2f;
            scale.z = _appliedRadius * 2f;
            ringTransform.localScale = scale;
        }
    }
}
