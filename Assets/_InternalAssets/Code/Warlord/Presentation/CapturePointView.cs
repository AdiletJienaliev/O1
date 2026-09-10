using UnityEngine;
using Warlord.Core;
using Warlord.Domain.Capture;
using Warlord.Gameplay.Capture;

namespace Warlord.Presentation
{
    /// <summary>
    /// Как точка захвата выглядит в мире (ГДД §2.7): флаг цвета владельца и кольцо по
    /// радиусу захвата. До этого у флагов в сцене не было ни одного рендерера — игрок
    /// подходил к точке и не видел ни её границы, ни того, чья она.
    ///
    /// Цвет и размеры ставятся через MaterialPropertyBlock и scale, а не заменой материалов:
    /// точек на арене девять, и каждая копия материала — лишний батч.
    /// </summary>
    public sealed class CapturePointView : MonoBehaviour
    {
        [Header("Источник")]
        [SerializeField] private CapturePointBehaviour point;

        [Header("Части")]
        [Tooltip("Полотнище флага: красится в цвет владельца.")]
        [SerializeField] private Renderer banner;

        [Tooltip("Кольцо по земле: показывает радиус захвата и пульсирует при откате.")]
        [SerializeField] private Renderer ring;

        [SerializeField] private Transform ringTransform;

        [Tooltip("Заливка внутри кольца: растёт по мере захвата точки.")]
        [SerializeField] private Renderer progress;

        [SerializeField] private Transform progressTransform;

        [Header("Вид")]
        [Tooltip("Цвет ничейной точки. Серый, а не чёрный: на тёмной земле чёрное кольцо не видно.")]
        [SerializeField] private Color neutralColor = new(0.62f, 0.64f, 0.68f);

        [SerializeField] private float ringAlpha = 0.45f;

        [Tooltip("Прозрачность заливки прогресса. Ниже кольца: заливка занимает всю площадь "
                 + "точки, и на равной с кольцом яркости она забивала бы юнитов на ней.")]
        [SerializeField] private float progressAlpha = 0.22f;

        [Tooltip("Насколько кольцо разгорается на пике пульсации при откате точки.")]
        [SerializeField] private float decayPulse = 0.35f;

        [SerializeField] private float decayPulseSpeed = 3.4f;

        [Tooltip("Запасной радиус, пока конфиг точки ещё не разрешён.")]
        [SerializeField] private float fallbackRadius = 6f;

        [Header("Эффекты")]
        [Tooltip("Библиотека эффектов: вспышка в момент смены владельца.")]
        [SerializeField] private Warlord.Presentation.Vfx.VfxLibrary vfx;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _block;
        private float _appliedRadius = -1f;
        private int _shownOwner = PlayerSlots.None;

        private void Awake()
        {
            point ??= GetComponentInParent<CapturePointBehaviour>();
            _block = new MaterialPropertyBlock();
            _shownOwner = point != null ? point.OwnerSlot : PlayerSlots.None;
        }

        private void LateUpdate()
        {
            if (point == null)
                return;

            ApplyRadius();
            ApplyColors();
            CheckCapture();
        }

        /// <summary>
        /// Вспышка на смене владельца. Смотрим на реплицированный слот, а не на серверное
        /// событие точки: событие поднимается только на сервере, и на клиентах захват
        /// проходил бы вообще без единого следа.
        /// </summary>
        private void CheckCapture()
        {
            int owner = point.OwnerSlot;

            if (owner == _shownOwner)
                return;

            _shownOwner = owner;

            if (vfx == null || vfx.captureBurst == null || !PlayerSlots.IsValid(owner))
                return;

            Warlord.Presentation.Vfx.VfxPool.Play(
                vfx.captureBurst,
                transform.position + Vector3.up * 0.6f,
                Quaternion.identity,
                vfx.lifetime,
                vfx.maxConcurrent);
        }

        /// <summary>Кольцо совпадает с настоящим радиусом захвата, иначе оно врёт игроку.</summary>
        private void ApplyRadius()
        {
            if (ringTransform == null)
                return;

            float radius = point.CaptureRadius > 0.01f ? point.CaptureRadius : fallbackRadius;

            if (Mathf.Approximately(radius, _appliedRadius))
                return;

            _appliedRadius = radius;

            // Цилиндр Unity высотой 2 и радиусом 0.5: диаметр даёт scale по X и Z.
            Vector3 scale = ringTransform.localScale;
            scale.x = radius * 2f;
            scale.z = radius * 2f;
            ringTransform.localScale = scale;
        }

        private void ApplyColors()
        {
            Color owner = ResolveOwnerColor();

            Tint(banner, owner);

            if (ring == null)
                return;

            float alpha = ringAlpha;

            // Откат точки читается именно пульсацией: владелец при этом не меняется,
            // и по одному цвету игрок бы не понял, что гарнизон пуст.
            if (point.IsDecaying)
                alpha += Mathf.Abs(Mathf.Sin(Time.time * decayPulseSpeed)) * decayPulse;

            Color ringColor = owner;
            ringColor.a = Mathf.Clamp01(alpha);
            Tint(ring, ringColor);

            ApplyProgress();
        }

        /// <summary>
        /// Заливка внутри кольца по доле захвата. Шкала есть и в HUD, но там она про точку,
        /// на которую игрок смотрит; на арене точек девять, и видеть, какая из них сейчас
        /// перетягивается, нужно не отводя глаз от боя.
        /// </summary>
        private void ApplyProgress()
        {
            if (progress == null || progressTransform == null)
                return;

            bool contested = point.Status == CaptureStatus.Contested
                             && PlayerSlots.IsValid(point.ChallengerSlot);

            float filled = contested ? point.ChallengerProgress : point.OwnerProgress;
            int slot = contested ? point.ChallengerSlot : point.OwnerSlot;

            float radius = (point.CaptureRadius > 0.01f ? point.CaptureRadius : fallbackRadius)
                           * Mathf.Clamp01(filled);

            Vector3 scale = progressTransform.localScale;
            scale.x = radius * 2f;
            scale.z = radius * 2f;
            progressTransform.localScale = scale;

            Color color = PlayerSlots.IsValid(slot) && TeamSlot.ColorsReady
                ? TeamSlot.ResolveColor(slot)
                : neutralColor;

            color.a = filled <= 0.001f ? 0f : progressAlpha;
            Tint(progress, color);
        }

        /// <summary>
        /// Цвет владельца, а пока точку перехватывают — подмешанный цвет претендента:
        /// шкала прогресса живёт в HUD, но у самой точки борьба тоже должна быть видна.
        /// </summary>
        private Color ResolveOwnerColor()
        {
            Color owner = PlayerSlots.IsValid(point.OwnerSlot) && TeamSlot.ColorsReady
                ? TeamSlot.ResolveColor(point.OwnerSlot)
                : neutralColor;

            if (point.Status != CaptureStatus.Contested || !PlayerSlots.IsValid(point.ChallengerSlot))
                return owner;

            Color challenger = TeamSlot.ColorsReady
                ? TeamSlot.ResolveColor(point.ChallengerSlot)
                : neutralColor;

            return Color.Lerp(owner, challenger, Mathf.Clamp01(point.ChallengerProgress));
        }

        private void Tint(Renderer target, Color color)
        {
            if (target == null)
                return;

            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
