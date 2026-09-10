using Warlord.Core;

namespace Warlord.Domain.Match
{
    /// <summary>Один слот стартового состава: кто его занимает и с какими настройками.</summary>
    public struct MatchSlotSetup
    {
        public SlotKind Kind;

        /// <summary>Соединение живого игрока. Для бота и пустого слота — -1.</summary>
        public int ClientId;

        /// <summary>Цвет из <see cref="Warlord.Configs.TeamColorConfig"/>. Личный, а не командный.</summary>
        public byte ColorId;

        /// <summary>Команда или -1, если слот сам за себя.</summary>
        public sbyte TeamId;

        /// <summary>Индекс характера в наборе ботов. Значим только для <see cref="SlotKind.Bot"/>.</summary>
        public byte Personality;

        public BotDifficulty Difficulty;

        /// <summary>Индекс имени внутри пула характера — чтобы два одинаковых бота звались по-разному.</summary>
        public byte NameIndex;

        public bool IsBot => Kind == SlotKind.Bot;
        public bool IsOccupied => Kind != SlotKind.Empty;

        public static MatchSlotSetup Empty => new() { Kind = SlotKind.Empty, ClientId = -1, TeamId = -1 };
    }

    /// <summary>
    /// Стартовый состав матча: кто в каком слоте, кто из них бот и кто с кем в команде.
    /// Собирается лобби один раз и передаётся в матч — дальше состав не меняется.
    ///
    /// Живёт отдельно от <see cref="MatchSettings"/> намеренно: настройки едут по сети
    /// каждому клиенту целиком, а состав нужен только серверу, и раздувать ради него
    /// синкающуюся структуру незачем. Наружу из состава уходит одна упакованная
    /// раскладка команд — её клиенты действительно читают.
    /// </summary>
    public sealed class MatchRoster
    {
        private readonly MatchSlotSetup[] _slots = new MatchSlotSetup[PlayerSlots.MaxSupported];

        public MatchRoster()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = MatchSlotSetup.Empty;
        }

        public int SlotCount => _slots.Length;

        public MatchSlotSetup Get(int slot)
        {
            return PlayerSlots.IsValid(slot) ? _slots[slot] : MatchSlotSetup.Empty;
        }

        public void Set(int slot, in MatchSlotSetup setup)
        {
            if (PlayerSlots.IsValid(slot))
                _slots[slot] = setup;
        }

        public void Clear(int slot)
        {
            if (PlayerSlots.IsValid(slot))
                _slots[slot] = MatchSlotSetup.Empty;
        }

        public bool IsBot(int slot) => Get(slot).IsBot;

        /// <summary>Слот, закреплённый за соединением, или <see cref="PlayerSlots.None"/>.</summary>
        public int FindSlotForClient(int clientId)
        {
            if (clientId < 0)
                return PlayerSlots.None;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Kind == SlotKind.Human && _slots[i].ClientId == clientId)
                    return i;
            }

            return PlayerSlots.None;
        }

        /// <summary>Первый свободный слот под живого игрока: пустой или занятый ботом.</summary>
        public int FindFreeHumanSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Kind == SlotKind.Empty)
                    return i;
            }

            return PlayerSlots.None;
        }

        public int CountOf(SlotKind kind)
        {
            int count = 0;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Kind == kind)
                    count++;
            }

            return count;
        }

        /// <summary>Раскладка команд для боевых систем. Слот без команды остаётся сам за себя.</summary>
        public TeamLayout BuildTeamLayout()
        {
            TeamLayout layout = TeamLayout.Ffa;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsOccupied && _slots[i].TeamId >= 0)
                    layout = layout.With(i, _slots[i].TeamId);
            }

            return layout;
        }
    }
}
