using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Bots
{
    /// <summary>
    /// Уровень исполнения бота: насколько хорошо он играет. Стиль игры сюда не входит —
    /// он живёт в <see cref="BotPersonalityConfig"/>, и любой характер доступен на любой
    /// сложности.
    ///
    /// Почти всё здесь — ограничения, а не усиления. Слабый бот отличается от сильного
    /// тем, что думает реже, реагирует позже, видит меньше и ошибается чаще, а не тем,
    /// что ему урезали урон. Так проигрыш слабому боту остаётся честным, а победа над
    /// сильным — заслуженной. Прямые гандикапы (<see cref="incomeMultiplier"/>,
    /// <see cref="costMultiplier"/>) вынесены отдельным разделом и по умолчанию выключены:
    /// если они когда-нибудь понадобятся, это должно быть видно в ассете, а не спрятано в коде.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Боты/Сложность", fileName = "BotDifficulty")]
    public sealed class BotDifficultyConfig : ScriptableObject
    {
        [Header("Представление")]
        public string displayName = "Обычный";
        public BotDifficulty level = BotDifficulty.Normal;

        [Header("Голова")]
        [Tooltip("Как часто бот пересматривает стратегическое решение, с. Реже — заметно медленнее реагирует на карту.")]
        [Min(0.1f)] public float decisionInterval = 0.75f;

        [Tooltip("Задержка между «увидел» и «начал делать», с. Имитирует человеческое время реакции.")]
        [Min(0f)] public float reactionDelay = 0.35f;

        [Tooltip("Потолок действий в минуту: покупки, приказы, прокачка. Не даёт боту играть быстрее рук человека.")]
        [Min(10f)] public float actionsPerMinute = 90f;

        [Header("Глаза")]
        [Tooltip("Что бот знает о карте. Честный обзор — только то, что видят его юниты и точки.")]
        public BotVisionMode vision = BotVisionMode.Honest;

        [Tooltip("Прибавка к радиусу обзора юнитов, м. Ноль — видит ровно столько, сколько видно в бою.")]
        [Min(0f)] public float visionBonus;

        [Tooltip("Сколько секунд бот помнит увиденное, прежде чем данные протухнут.")]
        [Min(1f)] public float memorySeconds = 20f;

        [Header("Руки")]
        [Tooltip("Качество боя: отход на нужном здоровье, добивание, выбор цели. 1 — без ошибок.")]
        [Range(0f, 1f)] public float micro = 0.6f;

        [Tooltip("Шанс выбрать вторую по качеству цель вместо лучшей. Ровно так ошибается живой игрок.")]
        [Range(0f, 1f)] public float mistakeChance = 0.25f;

        [Tooltip("Эффективность траты золота: 1 — покупает почти оптимально, 0 — как попало.")]
        [Range(0f, 1f)] public float economySkill = 0.6f;

        [Tooltip("Насколько дисциплинированно тратит опыт на прокачку.")]
        [Range(0f, 1f)] public float upgradeSkill = 0.6f;

        [Tooltip("Доля здоровья полководца, ниже которой бот уходит из боя.")]
        [Range(0f, 1f)] public float retreatHealthFraction = 0.35f;

        [Header("Гандикапы (по умолчанию выключены)")]
        [Tooltip("Множитель дохода бота. 1 — играет на тех же деньгах, что и человек.")]
        [Range(0.5f, 2f)] public float incomeMultiplier = 1f;

        [Tooltip("Множитель цен для бота. 1 — покупает по общему прайсу.")]
        [Range(0.5f, 2f)] public float costMultiplier = 1f;

        /// <summary>Пауза между двумя действиями рук, с. Из потолка действий в минуту.</summary>
        public float ActionCooldown => 60f / Mathf.Max(10f, actionsPerMinute);

        /// <summary>
        /// Насколько бот «дрожит» при выборе цели. Оценки целей умножаются на случайное
        /// число в этих пределах: без шума два бота одного характера ходили бы синхронно,
        /// как одно существо, и это первое, что выдаёт машину.
        /// </summary>
        public float ScoreNoise => Mathf.Lerp(0.35f, 0.05f, Mathf.Clamp01(micro));
    }
}
