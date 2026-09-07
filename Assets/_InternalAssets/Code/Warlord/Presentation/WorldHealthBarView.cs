using UnityEngine;
using UnityEngine.UI;
using Warlord.Core;
using Warlord.Domain.Combat;
using Warlord.Gameplay.Heroes;

namespace Warlord.Presentation
{
    /// <summary>
    /// Полоска здоровья над головой. Работает и для юнита, и для полководца: источник —
    /// <see cref="IHealthSource"/>, который реализуют оба.
    ///
    /// На своём полководце полоска выключается: игрок смотрит на своё здоровье в HUD,
    /// а над собственной головой она только загораживает обзор.
    /// </summary>
    public sealed class WorldHealthBarView : MonoBehaviour
    {
        [Header("Виджет")]
        [Tooltip("Image с типом Filled. Заполнение выставляется от 0 до 1.")]
        [SerializeField] private Image fill;

        [Tooltip("Что прятать целиком: обычно объект Canvas полоски. Пусто — прячется сам этот объект.")]
        [SerializeField] private GameObject root;

        [Header("Источник")]
        [Tooltip("Кому принадлежит здоровье. Пусто — ищется на этом объекте и родителях.")]
        [SerializeField] private MonoBehaviour source;

        [Header("Поведение")]
        [Tooltip("Прятать полоску над полководцем локального игрока.")]
        [SerializeField] private bool hideOnLocalHero = true;

        [Tooltip("Прятать, пока здоровье полное. Полоски появляются только у раненых.")]
        [SerializeField] private bool hideWhenFull;

        [Tooltip("Разворачивать полоску к камере каждый кадр.")]
        [SerializeField] private bool billboard = true;

        [Header("Цвет")]
        [Tooltip("Красить заливку в цвет владельца. Так чужие полоски сразу отличаются от своих.")]
        [SerializeField] private bool useTeamColor = true;

        private IHealthSource _health;
        private HeroController _hero;
        private Transform _billboardTarget;
        private int _tintedSlot = PlayerSlots.None;

        private void Awake()
        {
            ResolveRoot();

            _health = source as IHealthSource;
            _health ??= GetComponentInParent<IHealthSource>();

            _hero = GetComponentInParent<HeroController>();

            if (fill != null)
            {
                // Тип заливки задаём кодом: иначе достаточно один раз пересобрать префаб
                // из набора UI, и полоска молча превращается в обычную картинку.
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        /// <summary>
        /// Что именно прятать. Собственный объект гасить нельзя: с выключенным LateUpdate
        /// полоска уже не вернётся, и первый же полный запас здоровья спрятал бы её навсегда.
        /// </summary>
        private void ResolveRoot()
        {
            if (root == null)
            {
                Canvas canvas = GetComponentInChildren<Canvas>(true);
                root = canvas != null ? canvas.gameObject : null;
            }

            if (root == gameObject)
                root = null;
        }

        private void LateUpdate()
        {
            if (_health == null)
            {
                Toggle(false);
                return;
            }

            if (hideOnLocalHero && _hero != null && HeroController.Local == _hero)
            {
                Toggle(false);
                return;
            }

            int max = _health.MaxHealth;

            if (!_health.IsAlive || max <= 0)
            {
                Toggle(false);
                return;
            }

            float ratio = Mathf.Clamp01(_health.Health / (float)max);

            if (hideWhenFull && ratio >= 0.999f)
            {
                Toggle(false);
                return;
            }

            Toggle(true);

            if (fill != null)
            {
                fill.fillAmount = ratio;
                ApplyTint();
            }

            if (billboard)
                FaceCamera();
        }

        private void ApplyTint()
        {
            if (!useTeamColor || !TeamSlot.ColorsReady)
                return;

            int slot = TeamSlot.Resolve(_hero != null ? _hero.gameObject : ResolveOwnerObject());

            if (slot == _tintedSlot || !PlayerSlots.IsValid(slot))
                return;

            fill.color = TeamSlot.ResolveColor(slot);
            _tintedSlot = slot;
        }

        private GameObject ResolveOwnerObject()
        {
            return _health is MonoBehaviour behaviour ? behaviour.gameObject : gameObject;
        }

        /// <summary>
        /// Разворот к камере. Берётся именно боевая камера, а не Camera.main: в сцене может
        /// висеть вторая камера под рендер-текстуру, и полоски развернулись бы к ней.
        /// </summary>
        private void FaceCamera()
        {
            if (_billboardTarget == null)
            {
                HeroOrbitCamera orbit = HeroOrbitCamera.Current;
                Camera camera = orbit != null ? orbit.Camera : Camera.main;

                if (camera == null)
                    return;

                _billboardTarget = camera.transform;
            }

            Vector3 forward = _billboardTarget.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(forward);
        }

        private void Toggle(bool visible)
        {
            if (root != null)
            {
                if (root.activeSelf != visible)
                    root.SetActive(visible);

                return;
            }

            // Прятать нечего кроме самой заливки — гасим её, объект остаётся живым.
            if (fill != null && fill.enabled != visible)
                fill.enabled = visible;
        }
    }
}
