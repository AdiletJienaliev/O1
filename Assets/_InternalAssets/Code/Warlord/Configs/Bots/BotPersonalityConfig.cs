using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Bots
{
    /// <summary>
    /// Характер бота: как он играет. Здесь нет ни одного параметра, отвечающего за то,
    /// насколько хорошо он играет — это дело <see cref="BotDifficultyConfig"/>.
    ///
    /// Разделение принципиальное. Смешай их — и «трусливый» бот автоматически станет
    /// слабым, а «агрессивный» сильным, характеры превратятся в переименованные уровни
    /// сложности, и обещание «сложный, но осторожный противник» станет невыразимым.
    /// Здесь — только вкусы: куда тянет, чего избегает, чем воюет.
    ///
    /// Веса — множители к оценке цели, а не жёсткие правила. Бот с нулевым
    /// <see cref="raidWeight"/> не «никогда не рейдит», он просто не выберет рейд,
    /// пока тот не окажется единственным осмысленным ходом. Правил вида «если A, то B»
    /// здесь нет намеренно: они ломаются от новой карты, а веса — нет.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Боты/Характер", fileName = "BotPersonality")]
    public sealed class BotPersonalityConfig : ScriptableObject
    {
        [Header("Представление")]
        public string displayName = "Характер";

        [Tooltip("Короткое описание для лобби: чего ждать от такого противника.")]
        [TextArea] public string blurb;

        [Tooltip("Имена, которыми зовутся боты этого характера. Пусто — берётся displayName с номером.")]
        public string[] namePool;

        public Sprite icon;

        [Header("Куда тянет (множители к оценке цели)")]
        [Tooltip("Центральный флаг — главный источник очков победы (ГДД §2).")]
        [Range(0f, 2f)] public float centerWeight = 1f;

        [Tooltip("Аванпосты: доход, улучшения и место под гарнизон (ГДД §2.4).")]
        [Range(0f, 2f)] public float outpostWeight = 1f;

        [Tooltip("Оборона своих точек, к которым идёт враг.")]
        [Range(0f, 2f)] public float defenseWeight = 1f;

        [Tooltip("Рейд на чужую базу в обход центра. Высокое значение — тот самый «подлый» бот.")]
        [Range(0f, 2f)] public float raidWeight = 0.5f;

        [Tooltip("Охота на чужих полководцев.")]
        [Range(0f, 2f)] public float huntWeight = 0.6f;

        [Tooltip("Помощь союзнику в командном матче.")]
        [Range(0f, 2f)] public float supportWeight = 0.8f;

        [Tooltip("Сидеть дома и копить армию вместо вылазки.")]
        [Range(0f, 2f)] public float economyWeight = 1f;

        [Header("Повадки")]
        [Tooltip("Осторожность: при каком перевесе врага бот считает бой проигранным и уходит. 0 — лезет всегда.")]
        [Range(0f, 1f)] public float caution = 0.5f;

        [Tooltip("Упрямство: насколько долго держится за выбранную цель, не переобуваясь на каждую новость.")]
        [Range(0f, 1f)] public float persistence = 0.5f;

        [Tooltip("Хищность: насколько сильнее тянет к слабому и незащищённому противнику.")]
        [Range(0f, 1f)] public float opportunism = 0.5f;

        [Tooltip("Злопамятность: насколько предпочитает того, кто его недавно бил.")]
        [Range(0f, 1f)] public float vengeance = 0.3f;

        [Tooltip("Какую долю лимита армии копит на базе, прежде чем выйти в поле.")]
        [Range(0f, 1f)] public float massBeforePush = 0.5f;

        [Header("Армия")]
        [Tooltip("Желаемая доля стрелков в армии. Ростер читается из данных, конкретные юниты здесь не названы.")]
        [Range(0f, 1f)] public float rangedShare = 0.35f;

        [Tooltip("Тяга к живучим юнитам: больше — охотнее берёт танков в первую шеренгу.")]
        [Range(0f, 2f)] public float tankPreference = 1f;

        [Tooltip("Тяга к урону по площади.")]
        [Range(0f, 2f)] public float splashPreference = 1f;

        [Tooltip("Какую долю золота бот готов держать в гарнизонах вместо полевой армии (ГДД §1.1).")]
        [Range(0f, 1f)] public float garrisonShare = 0.3f;

        [Tooltip("Насколько охотно тратит опыт сразу, а не копит его на дорогие уровни.")]
        [Range(0f, 1f)] public float upgradeEagerness = 0.6f;

        [Tooltip("Приоритеты веток прокачки по порядку UpgradeBranch: урон, живучесть, дальность, лимит, скорость, полководец.")]
        public float[] branchPriority = { 1f, 1f, 0.8f, 1.2f, 0.7f, 0.5f };

        [Header("Рейд")]
        [Tooltip("Какая доля лимита армии нужна, чтобы бот пошёл на чужую базу.")]
        [Range(0f, 1f)] public float raidMinArmyShare = 0.6f;

        [Tooltip("Берёт армию с собой. Выключено — бегает на чужую базу один, надеясь на пустой гарнизон.")]
        public bool raidWithArmy = true;

        [Tooltip("Обходит занятые врагом точки по краю карты вместо прямой дороги через центр.")]
        public bool flanks = true;

        [Header("Приказы")]
        [Tooltip("Приказ, которым бот ведёт армию по карте. «В атаку» агрессивнее, «За мной» — собраннее (ГДД §6).")]
        public ArmyOrderType travelOrder = ArmyOrderType.FollowLeader;

        [Tooltip("Приказ при удержании своей точки.")]
        public ArmyOrderType holdOrder = ArmyOrderType.HoldGround;

        /// <summary>Вес цели по её виду. Одно место, где характер превращается в число.</summary>
        public float GoalWeight(BotGoalKind kind)
        {
            switch (kind)
            {
                case BotGoalKind.Capture: return centerWeight;
                case BotGoalKind.Defend: return defenseWeight;
                case BotGoalKind.Raid: return raidWeight;
                case BotGoalKind.Hunt: return huntWeight;
                case BotGoalKind.Support: return supportWeight;
                case BotGoalKind.Economy: return economyWeight;
                default: return 1f;
            }
        }

        /// <summary>Приоритет ветки прокачки. Незаполненный массив не должен ронять решение.</summary>
        public float BranchPriority(UpgradeBranch branch)
        {
            int index = (int)branch;
            return branchPriority != null && index >= 0 && index < branchPriority.Length
                ? Mathf.Max(0f, branchPriority[index])
                : 1f;
        }

        /// <summary>Имя конкретного бота. Индекс приходит из состава матча, чтобы двойников не было.</summary>
        public string ResolveName(int nameIndex)
        {
            if (namePool == null || namePool.Length == 0)
                return string.IsNullOrEmpty(displayName) ? "Бот" : displayName;

            int index = ((nameIndex % namePool.Length) + namePool.Length) % namePool.Length;
            string picked = namePool[index];

            return string.IsNullOrWhiteSpace(picked) ? displayName : picked;
        }

        /// <summary>Сколько секунд бот держится за выбранную цель, прежде чем всерьёз пересмотреть решение.</summary>
        public float GoalCommitmentSeconds => Mathf.Lerp(3f, 20f, Mathf.Clamp01(persistence));
    }
}
