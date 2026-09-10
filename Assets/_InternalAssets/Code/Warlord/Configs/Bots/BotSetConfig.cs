using System.Collections.Generic;
using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Bots
{
    /// <summary>
    /// Набор ботов проекта: доступные характеры и четыре уровня сложности.
    /// Индекс характера в этом списке — его сетевой id: по сети про бота едут
    /// три байта (характер, сложность, имя), а не строки.
    ///
    /// Порядок характеров менять нельзя по той же причине, по которой нельзя менять
    /// порядок в ростере юнитов: индекс уже уехал клиентам и лежит в настройках комнаты.
    /// Добавлять новые — в конец.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Боты/Набор", fileName = "BotSetConfig")]
    public sealed class BotSetConfig : ScriptableObject
    {
        [Tooltip("Характеры. Индекс в списке — сетевой id, порядок менять нельзя.")]
        [SerializeField] private List<BotPersonalityConfig> personalities = new();

        [Tooltip("Сложности по порядку BotDifficulty: лёгкий, обычный, сложный, жестокий.")]
        [SerializeField] private List<BotDifficultyConfig> difficulties = new();

        [Tooltip("Характер, который лобби предлагает по умолчанию.")]
        [SerializeField] private int defaultPersonality;

        public IReadOnlyList<BotPersonalityConfig> Personalities => personalities;
        public int Count => personalities.Count;

        public bool IsValidIndex(int index) => index >= 0 && index < personalities.Count;

        public BotPersonalityConfig Get(int index) => IsValidIndex(index) ? personalities[index] : null;

        public int DefaultPersonality => Mathf.Clamp(defaultPersonality, 0, Mathf.Max(0, Count - 1));

        /// <summary>
        /// Настройки сложности. Возвращает ближайшую доступную, а не null: матч не должен
        /// падать из-за незаполненного ассета — он должен идти с ботом попроще.
        /// </summary>
        public BotDifficultyConfig GetDifficulty(BotDifficulty level)
        {
            if (difficulties == null || difficulties.Count == 0)
                return null;

            for (int i = 0; i < difficulties.Count; i++)
            {
                if (difficulties[i] != null && difficulties[i].level == level)
                    return difficulties[i];
            }

            int index = Mathf.Clamp((int)level, 0, difficulties.Count - 1);
            return difficulties[index];
        }

        /// <summary>Следующий характер по кругу. Кнопка в лобби ходит по списку этим методом.</summary>
        public int NextPersonality(int index)
        {
            return Count <= 0 ? 0 : (index + 1) % Count;
        }

        /// <summary>Имя бота по паре «характер + номер имени».</summary>
        public string ResolveName(int personalityIndex, int nameIndex)
        {
            BotPersonalityConfig personality = Get(personalityIndex);
            return personality != null ? personality.ResolveName(nameIndex) : "Бот";
        }

        /// <summary>Проверка целостности набора — зовётся из валидации GameConfig.</summary>
        public bool Validate(out string error)
        {
            if (personalities.Count == 0)
            {
                error = "BotSetConfig: нет ни одного характера";
                return false;
            }

            for (int i = 0; i < personalities.Count; i++)
            {
                if (personalities[i] == null)
                {
                    error = $"BotSetConfig: пустая запись характера под индексом {i}";
                    return false;
                }
            }

            if (difficulties.Count == 0)
            {
                error = "BotSetConfig: не заданы уровни сложности";
                return false;
            }

            error = null;
            return true;
        }
    }
}
