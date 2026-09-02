using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Лента событий матча: захваты, смерти полководцев, выбывания. Читает шину
    /// <see cref="Warlord.Gameplay.Match.MatchEvents"/> — игровой код о ленте не знает.
    /// </summary>
    public sealed class EventFeedWidget : HudWidget
    {
        [Header("Строки")]
        [SerializeField] private RectTransform container;
        [SerializeField] private TextMeshProUGUI lineTemplate;

        [Tooltip("Сколько строк держать на экране.")]
        [SerializeField] private int capacity = 4;

        [Tooltip("Сколько секунд живёт строка.")]
        [SerializeField] private float lifetime = 6f;

        private readonly List<TextMeshProUGUI> _lines = new(4);
        private readonly List<float> _deadlines = new(4);

        protected override void OnInitialized()
        {
            BuildLines();

            Match.Events.BaseCaptured += OnBaseCaptured;
            Match.Events.HeroKilled += OnHeroKilled;
            Match.Events.PlayerEliminated += OnPlayerEliminated;
            Match.Events.CentralFlagOwnerChanged += OnFlagOwnerChanged;
        }

        protected override void OnShutdown()
        {
            if (!HasMatch)
                return;

            Match.Events.BaseCaptured -= OnBaseCaptured;
            Match.Events.HeroKilled -= OnHeroKilled;
            Match.Events.PlayerEliminated -= OnPlayerEliminated;
            Match.Events.CentralFlagOwnerChanged -= OnFlagOwnerChanged;
        }

        public override void Refresh(PlayerState player)
        {
            float now = Time.time;

            for (int i = 0; i < _lines.Count; i++)
            {
                if (!_lines[i].gameObject.activeSelf || now < _deadlines[i])
                    continue;

                _lines[i].gameObject.SetActive(false);
            }
        }

        private void OnBaseCaptured(int victimSlot, int captorSlot)
        {
            Push(Colored(captorSlot) + " захватил базу " + Colored(victimSlot));
        }

        private void OnHeroKilled(int victimSlot, int killerSlot)
        {
            Push(PlayerSlots.IsValid(killerSlot)
                ? Colored(killerSlot) + " сразил " + Colored(victimSlot)
                : Colored(victimSlot) + " пал");
        }

        private void OnPlayerEliminated(int slot, EliminationReason reason)
        {
            Push(Colored(slot) + " выбыл: " + UiText.Elimination(reason));
        }

        private void OnFlagOwnerChanged(int previous, int next)
        {
            if (!PlayerSlots.IsValid(next))
            {
                Push("Центральный флаг снова свободен");
                return;
            }

            Push(Colored(next) + " держит центральный флаг");
        }

        /// <summary>Имя игрока в его цвете: в ленте цвет читается быстрее текста.</summary>
        private string Colored(int slot)
        {
            TeamColorConfig colors = Match.Config != null ? Match.Config.TeamColors : null;
            Color color = TeamPalette.Primary(colors, slot);

            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + UiText.PlayerName(slot) + "</color>";
        }

        private void Push(string message)
        {
            if (_lines.Count == 0)
                return;

            // Сдвигаем содержимое вверх: свежая строка всегда снизу, порядок событий сохраняется.
            for (int i = 0; i < _lines.Count - 1; i++)
            {
                _lines[i].text = _lines[i + 1].text;
                _lines[i].gameObject.SetActive(_lines[i + 1].gameObject.activeSelf);
                _deadlines[i] = _deadlines[i + 1];
            }

            int last = _lines.Count - 1;
            _lines[last].text = message;
            _lines[last].gameObject.SetActive(true);
            _deadlines[last] = Time.time + lifetime;
        }

        private void BuildLines()
        {
            if (_lines.Count > 0 || container == null || lineTemplate == null)
                return;

            lineTemplate.gameObject.SetActive(false);

            for (int i = 0; i < Mathf.Max(1, capacity); i++)
            {
                TextMeshProUGUI line = Instantiate(lineTemplate, container);
                line.name = "FeedLine_" + i;
                line.gameObject.SetActive(false);

                _lines.Add(line);
                _deadlines.Add(0f);
            }
        }
    }
}
