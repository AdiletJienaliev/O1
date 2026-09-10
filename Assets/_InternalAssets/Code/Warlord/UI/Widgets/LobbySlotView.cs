using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Core;
using Warlord.Networking.Lobby;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Строка списка игроков в лобби (ГДД §14): кто занял слот, его цвет, готовность,
    /// команда и — для бота — характер и сложность.
    ///
    /// Управляющие кнопки живут прямо в строке, а не в отдельной панели: состав комнаты
    /// читается сверху вниз одним взглядом, и настройка бота должна быть там же, где он сам.
    /// Строка их только показывает — нажатия обрабатывает экран, он же знает, кто хост.
    /// </summary>
    public sealed class LobbySlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI indexLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private Image background;
        [SerializeField] private GameObject readyBadge;
        [SerializeField] private GameObject hostBadge;
        [SerializeField] private GameObject emptyHint;

        [Header("Состав")]
        [SerializeField] private Button addBotButton;
        [SerializeField] private Button removeBotButton;
        [SerializeField] private Button personalityButton;
        [SerializeField] private TextMeshProUGUI personalityLabel;
        [SerializeField] private Button difficultyButton;
        [SerializeField] private TextMeshProUGUI difficultyLabel;
        [SerializeField] private Button teamButton;
        [SerializeField] private TextMeshProUGUI teamLabel;

        [Header("Цвета строки")]
        [SerializeField] private Color occupiedColor = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color localHighlight = new(0.99f, 0.85f, 0.35f);
        [SerializeField] private Color botTint = new(0.72f, 0.86f, 1f);

        public Button AddBotButton => addBotButton;
        public Button RemoveBotButton => removeBotButton;
        public Button PersonalityButton => personalityButton;
        public Button DifficultyButton => difficultyButton;
        public Button TeamButton => teamButton;

        /// <summary>Всё, что строке нужно показать. Структура вместо десятка аргументов.</summary>
        public struct Data
        {
            public int Index;
            public LobbySlotInfo Info;
            public bool IsLocal;
            public bool IsHostSlot;

            /// <summary>Смотрит ли на комнату хост: только он меняет состав.</summary>
            public bool LocalIsHost;

            /// <summary>Доступны ли боты в этой сборке контента.</summary>
            public bool BotsAvailable;

            public Color TeamColor;
            public string DisplayName;
            public string PersonalityName;
        }

        public void Apply(in Data data)
        {
            LobbySlotInfo info = data.Info;
            bool occupied = info.Occupied;
            bool bot = info.IsBot;

            if (indexLabel != null)
                indexLabel.text = (data.Index + 1).ToString();

            if (nameLabel != null)
            {
                nameLabel.text = data.DisplayName;
                nameLabel.color = data.IsLocal ? localHighlight : (bot ? botTint : Color.white);
            }

            if (colorSwatch != null)
            {
                colorSwatch.color = data.TeamColor;
                colorSwatch.enabled = occupied;
            }

            if (background != null)
                background.color = occupied ? occupiedColor : emptyColor;

            if (readyBadge != null)
                readyBadge.SetActive(info.IsHuman && info.Ready);

            if (hostBadge != null)
                hostBadge.SetActive(data.IsHostSlot);

            // Подсказка «ждём игрока» и кнопка «плюс бот» занимают одно место: у хоста
            // слот — это приглашение действовать, у остальных — просто ожидание.
            bool canAddBot = !occupied && data.LocalIsHost && data.BotsAvailable;

            Show(addBotButton, canAddBot);

            if (emptyHint != null)
                emptyHint.SetActive(!occupied && !canAddBot);

            Show(removeBotButton, bot && data.LocalIsHost);
            Show(personalityButton, bot);
            Show(difficultyButton, bot);
            Show(teamButton, occupied);

            if (personalityButton != null)
                personalityButton.interactable = data.LocalIsHost;

            if (difficultyButton != null)
                difficultyButton.interactable = data.LocalIsHost;

            if (teamButton != null)
                teamButton.interactable = data.LocalIsHost;

            if (personalityLabel != null)
                personalityLabel.text = data.PersonalityName;

            if (difficultyLabel != null)
                difficultyLabel.text = UiText.Difficulty((BotDifficulty)info.BotDifficulty);

            if (teamLabel != null)
                teamLabel.text = UiText.Team(info.TeamId);
        }

        private static void Show(Component component, bool visible)
        {
            if (component != null && component.gameObject.activeSelf != visible)
                component.gameObject.SetActive(visible);
        }
    }
}
