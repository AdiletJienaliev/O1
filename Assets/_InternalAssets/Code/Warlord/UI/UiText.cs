using Warlord.Configs;
using Warlord.Core;
using Warlord.Domain.Match;

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

        public static string PlayerName(int slot) => "Игрок " + (slot + 1);
    }
}
