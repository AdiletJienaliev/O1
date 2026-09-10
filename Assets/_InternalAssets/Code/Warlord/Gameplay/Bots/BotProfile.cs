using Warlord.Configs.Bots;
using Warlord.Core;

namespace Warlord.Gameplay.Bots
{
    /// <summary>
    /// Готовая пара «характер + сложность» одного бота вместе с его именем.
    /// Разрешается один раз при создании бота: дальше по матчу ходит уже эта структура,
    /// а не индексы из настроек комнаты.
    ///
    /// Характер и сложность живут порознь и здесь — сочетаются они свободно:
    /// «жестокий трус» и «лёгкий берсерк» одинаково законны, и это главное, ради чего
    /// они вообще разделены.
    /// </summary>
    public readonly struct BotProfile
    {
        public readonly BotPersonalityConfig Personality;
        public readonly BotDifficultyConfig Difficulty;
        public readonly BotDifficulty Level;
        public readonly int PersonalityIndex;
        public readonly int NameIndex;
        public readonly string Name;

        public BotProfile(
            BotPersonalityConfig personality,
            BotDifficultyConfig difficulty,
            BotDifficulty level,
            int personalityIndex,
            int nameIndex)
        {
            Personality = personality;
            Difficulty = difficulty;
            Level = level;
            PersonalityIndex = personalityIndex;
            NameIndex = nameIndex;
            Name = personality != null ? personality.ResolveName(nameIndex) : "Бот";
        }

        public bool IsValid => Personality != null;

        /// <summary>Как часто бот пересматривает решение. Без настроек сложности — раз в секунду.</summary>
        public float DecisionInterval => Difficulty != null ? Difficulty.decisionInterval : 1f;

        /// <summary>Задержка реакции: «увидел» и «начал делать» — разные моменты.</summary>
        public float ReactionDelay => Difficulty != null ? Difficulty.reactionDelay : 0.4f;

        /// <summary>Пауза между действиями рук. Держит бота в пределах человеческой скорости.</summary>
        public float ActionCooldown => Difficulty != null ? Difficulty.ActionCooldown : 0.7f;

        public BotVisionMode Vision => Difficulty != null ? Difficulty.vision : BotVisionMode.Honest;

        public float MemorySeconds => Difficulty != null ? Difficulty.memorySeconds : 20f;

        public float Micro => Difficulty != null ? Difficulty.micro : 0.5f;

        public float RetreatHealthFraction => Difficulty != null ? Difficulty.retreatHealthFraction : 0.3f;

        public float IncomeMultiplier => Difficulty != null ? Difficulty.incomeMultiplier : 1f;

        public float CostMultiplier => Difficulty != null ? Difficulty.costMultiplier : 1f;

        /// <summary>Сколько секунд бот держится за цель, прежде чем всерьёз её пересмотреть.</summary>
        public float CommitmentSeconds => Personality != null ? Personality.GoalCommitmentSeconds : 8f;
    }
}
