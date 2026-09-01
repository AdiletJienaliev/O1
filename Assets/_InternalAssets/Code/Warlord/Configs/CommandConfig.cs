using UnityEngine;

namespace Warlord.Configs
{
    /// <summary>Настройки приказов и поведения армии (ГДД §6, §15).</summary>
    [CreateAssetMenu(menuName = "Warlord/Command", fileName = "CommandConfig")]
    public sealed class CommandConfig : ScriptableObject
    {
        [Header("Агро")]
        [Tooltip("Радиус поиска цели при приказе В атаку.")]
        [Min(0f)] public float attackAggroRadius = 15f;

        [Tooltip("Радиус ответа при приказе Стоять.")]
        [Min(0f)] public float defendRadius = 6f;

        [Tooltip("Насколько далеко юнит может отойти от своего слота, преследуя цель.")]
        [Min(0f)] public float leashDistance = 4f;

        [Tooltip("Юнит не меняет цель чаще, чем раз в этот интервал.")]
        [Min(0f)] public float targetSwitchCooldown = 1f;

        [Header("Построение")]
        [Tooltip("Множитель скорости при перестроении, чтобы отставшие догоняли.")]
        [Min(1f)] public float formationRebuildSpeed = 1.15f;

        [Tooltip("Как часто пересобираются слоты после потерь, с.")]
        [Min(0.1f)] public float formationReflowInterval = 3f;

        [Tooltip("Смещение якоря назад от полководца при приказе За мной, м.")]
        [Min(0f)] public float followOffset = 3f;

        [Tooltip("При приказе За мной юниты бьют только в ответ.")]
        public bool retaliateOnFollow = true;

        [Header("Навигация")]
        [Tooltip("Как часто юнит переустанавливает точку назначения, с.")]
        [Min(0.05f)] public float repathInterval = 0.4f;

        [Tooltip("Считаем, что юнит дошёл до слота, если ближе этого расстояния.")]
        [Min(0.05f)] public float slotArriveTolerance = 0.35f;

        [Tooltip("Сколько секунд юнит помнит того, кто его ударил.")]
        [Min(0f)] public float retaliationMemory = 3f;

        /// <summary>Радиус агро для конкретного типа: персональный, если задан, иначе глобальный.</summary>
        public float ResolveAggroRadius(UnitConfig unit, float fallback)
        {
            if (unit != null && unit.aggroRadiusOverride > 0f)
                return unit.aggroRadiusOverride;

            return fallback;
        }
    }
}
