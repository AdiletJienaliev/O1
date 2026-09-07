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

        [Header("Гарнизон")]
        [Tooltip("Откатывается ли шкала, когда у владельца рядом с точкой нет ни одного живого юнита (ГДД §2.4). " +
                 "Ключевое отличие аванпоста от центра: центр не откатывается, аванпост — да.")]
        public bool garrisonDecay;

        [Tooltip("Скорость отката без гарнизона, доля шкалы в секунду. 0.03 = ~33 секунды до нуля.")]
        [Range(0f, 1f)] public float garrisonDecayRatePerSecond = 0.03f;

        [Tooltip("В каком радиусе ищется живой юнит владельца, чтобы откат не начался, м.")]
        [Min(1f)] public float garrisonCheckRadius = 12f;

        [Tooltip("Сколько охранников один игрок держит на этой точке. Ниже этого числа сработает и лимит из UnitConfig.")]
        [Min(0)] public int maxGuards = 6;

        [Tooltip("Радиус кольца, по которому расставляются охранники, м (ГДД §1.7).")]
        [Min(1f)] public float garrisonRingRadius = 4.5f;

        [Header("Улучшение")]
        [Tooltip("Выбирает ли владелец улучшение при захвате (ГДД §2.5). Есть только у аванпостов.")]
        public bool hasUpgradeSlot;

        [Tooltip("Можно ли назначить эту точку точкой сбора новых юнитов (ГДД §2.6).")]
        public bool allowsRallyPoint;

        [Header("Награда")]
        public CaptureRewardType rewardType = CaptureRewardType.Continuous;

        [Tooltip("Стартовать ли точку уже захваченной владельцем зоны. True для флагов баз.")]
        public bool startsOwnedByZoneOwner;
    }
}
