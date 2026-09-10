using Warlord.Configs;
using Warlord.Configs.Bots;
using Warlord.Core;
using Warlord.Domain.Match;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.UI
{
    /// <summary>
    /// Все строки интерфейса в одном месте. Игровой код оперирует перечислениями,
    /// а человекочитаемый текст живёт здесь — так его можно перевести, не трогая логику.
    /// </summary>
    public static class UiText
    {
        public static string Rejection(CommandRejection reason)
        {
            switch (reason)
            {
                case CommandRejection.MatchNotRunning: return "Матч ещё не начался";
                case CommandRejection.PlayerEliminated: return "Вы выбыли из матча";
                case CommandRejection.NotInBuyZone: return "Вернитесь на свою базу";
                case CommandRejection.NotEnoughGold: return "Не хватает золота";
                case CommandRejection.ArmyLimitReached: return "Лимит армии достигнут";
                case CommandRejection.NotEnoughXp: return "Не хватает опыта";
                case CommandRejection.UpgradeUnavailable: return "Ветка выкачана до конца";
                case CommandRejection.UnknownUnit: return "Неизвестный юнит";
                case CommandRejection.UnknownFormation: return "Неизвестное построение";
                case CommandRejection.OutOfBounds: return "Точка вне карты";
                default: return string.Empty;
            }
        }

        /// <summary>Название сложности бота. Берётся из ассета, если он есть, иначе запасное.</summary>
        public static string Difficulty(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy: return "Лёгкий";
                case BotDifficulty.Normal: return "Обычный";
                case BotDifficulty.Hard: return "Сложный";
                case BotDifficulty.Brutal: return "Жестокий";
                default: return difficulty.ToString();
            }
        }

        /// <summary>Подпись команды. Отрицательный id означает «сам за себя» — обычный FFA.</summary>
        public static string Team(int teamId)
        {
            if (teamId < 0)
                return "—";

            const string Letters = "АБВГ";
            return teamId < Letters.Length ? Letters[teamId].ToString() : (teamId + 1).ToString();
        }

        /// <summary>Кто сидит в слоте, для списка комнаты и таблицы итогов.</summary>
        public static string SlotOccupant(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.Human: return "Игрок";
                case SlotKind.Bot: return "Бот";
                default: return "Свободно";
            }
        }

        public static string Branch(UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Damage: return "Урон";
                case UpgradeBranch.Health: return "Живучесть";
                case UpgradeBranch.AttackRange: return "Дальность";
                case UpgradeBranch.UnitCap: return "Лимит армии";
                case UpgradeBranch.MoveSpeed: return "Скорость";
                case UpgradeBranch.Hero: return "Полководец";
                default: return branch.ToString();
            }
        }

        public static string Order(ArmyOrderType order)
        {
            switch (order)
            {
                case ArmyOrderType.HoldGround: return "Стоять";
                case ArmyOrderType.FollowLeader: return "За мной";
                case ArmyOrderType.AttackMove: return "В атаку";
                case ArmyOrderType.Defend: return "Защита";
                default: return order.ToString();
            }
        }

        public static string Phase(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Lobby: return "Лобби";
                case MatchPhase.Countdown: return "Подготовка";
                case MatchPhase.Running: return "Бой";
                case MatchPhase.Finished: return "Матч окончен";
                default: return string.Empty;
            }
        }

        public static string EndReason(MatchEndReason reason)
        {
            switch (reason)
            {
                case MatchEndReason.TimeExpired: return "Время вышло";
                case MatchEndReason.LastPlayerStanding: return "Последний выживший";
                case MatchEndReason.HostLeft: return "Хост покинул матч";
                default: return string.Empty;
            }
        }

        public static string Elimination(EliminationReason reason)
        {
            switch (reason)
            {
                case EliminationReason.BaseCaptured: return "база захвачена";
                case EliminationReason.Disconnected: return "отключился";
                default: return string.Empty;
            }
        }

        /// <summary>Формат часов матча: мм:сс. Отрицательное время показываем как 00:00.</summary>
        public static string Clock(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;

            int total = UnityEngine.Mathf.CeilToInt(seconds);
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        /// <summary>Короткая запись больших чисел: 1240 -> 1.2K. Ширина чипа ресурса ограничена.</summary>
        public static string Compact(int value)
        {
            if (value < 10000)
                return value.ToString();

            return value < 1000000
                ? (value / 1000f).ToString("0.#") + "K"
                : (value / 1000000f).ToString("0.#") + "M";
        }

        public static string UnitName(UnitConfig unit, int fallbackIndex)
        {
            if (unit == null)
                return "Юнит " + (fallbackIndex + 1);

            return string.IsNullOrEmpty(unit.displayName) ? unit.unitId : unit.displayName;
        }

        /// <summary>
        /// Как зовут игрока в этом слоте. У бота — его собственное имя из набора характеров:
        /// «Игрок 3» в ленте событий и в таблице итогов стирает единственное, чем боты
        /// отличаются друг от друга, а именно они и должны читаться как разные противники.
        /// </summary>
        public static string PlayerName(int slot)
        {
            PlayerState player = PlayerState.Find(slot);

            if (player == null || !player.IsBot)
                return "Игрок " + (slot + 1);

            MatchManager match = MatchManager.Instance;
            BotSetConfig bots = match != null && match.Config != null ? match.Config.Bots : null;

            return bots != null
                ? bots.ResolveName(player.BotPersonalityIndex, player.BotNameIndex)
                : "Бот " + (slot + 1);
        }
    }
}
