using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs
{
    /// <summary>
    /// Параметры точки захвата (ГДД §9, §10). Один ассет на центральный флаг,
    /// другой — на флаги баз: механизм двух шкал общий, различаются только цифры.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Capture Point", fileName = "CapturePointConfig")]
    public sealed class CapturePointConfig : ScriptableObject
    {
        [Header("Идентификация")]
        public string pointId = "center_flag";
        public string displayName = "Центральный флаг";

        [Header("Зона")]
        [Min(0.5f)] public float captureRadius = 6f;

        [Header("Скорости, доля шкалы в секунду")]
        [Tooltip("Набор своей шкалы. 0.08 = 12.5 секунды с нуля.")]
        [Range(0.001f, 1f)] public float captureRatePerSecond = 0.08f;

        [Tooltip("Обнуление шкалы текущего владельца.")]
        [Range(0.001f, 1f)] public float decaptureRatePerSecond = 0.1f;

        [Tooltip("Спад незавершённой шкалы, когда в зоне никого. Захваченная шкала не спадает.")]
        [Range(0f, 1f)] public float decayRatePerSecond = 0.02f;

        [Header("Правила зоны")]
        [Tooltip("Ускорение от каждого дополнительного союзного полководца. 0 = не ускоряет (ГДД §17.6).")]
        [Min(0f)] public float stackMultiplierPerExtraPlayer;

        [Tooltip("Мешают ли захвату вражеские юниты. По ГДД — нет, только полководцы.")]
        public bool contestedByUnits;

        [Tooltip("Мешают ли захвату вражеские полководцы.")]
        public bool contestedByHeroes = true;

        [Tooltip("Восстанавливается ли недобитая шкала владельца, когда в зоне никого.")]
        public bool ownerBarRecoversWhenEmpty = true;

        [Header("Награда")]
        public CaptureRewardType rewardType = CaptureRewardType.Continuous;

        [Tooltip("Стартовать ли точку уже захваченной владельцем зоны. True для флагов баз.")]
        public bool startsOwnedByZoneOwner;
    }
}
