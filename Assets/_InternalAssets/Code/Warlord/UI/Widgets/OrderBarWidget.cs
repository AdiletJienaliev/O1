using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs.Formations;
using Warlord.Core;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Приказы армии и построения (ГДД §6, §7). Дублирует клавиши 1/2/3 и Q/W/E,
    /// но не заменяет их: ввод с клавиатуры идёт своим путём через HeroCommandRouter.
    /// </summary>
    public sealed class OrderBarWidget : HudWidget
    {
        [Header("Приказы")]
        [Tooltip("Ровно три кнопки в порядке ArmyOrderType: Стоять, За мной, В атаку.")]
        [SerializeField] private HotkeyButtonView[] orderButtons = new HotkeyButtonView[3];
        [SerializeField] private Sprite[] orderIcons = new Sprite[3];

        [Header("Построения")]
        [SerializeField] private RectTransform formationContainer;
        [SerializeField] private HotkeyButtonView formationTemplate;

        private static readonly string[] OrderHotkeys = { "1", "2", "3" };
        private static readonly string[] FormationHotkeys = { "Q", "W", "E", "R" };

        private readonly List<HotkeyButtonView> _formationButtons = new(4);
        private bool _built;

        protected override void OnInitialized() => Build();

        public override void Refresh(PlayerState player)
        {
            if (player == null)
                return;

            Build();

            bool enabled = !player.IsEliminated && Match != null && Match.Phase == MatchPhase.Running;
            int order = (int)player.OrderType;

            for (int i = 0; i < orderButtons.Length; i++)
            {
                HotkeyButtonView button = orderButtons[i];
                if (button == null)
                    continue;

                button.SetSelected(i == order);
                button.SetInteractable(enabled);
            }

            for (int i = 0; i < _formationButtons.Count; i++)
            {
                _formationButtons[i].SetSelected(i == player.FormationIndex);
                _formationButtons[i].SetInteractable(enabled);
            }
        }

        private void Build()
        {
            if (_built || !HasMatch)
                return;

            for (int i = 0; i < orderButtons.Length; i++)
            {
                HotkeyButtonView button = orderButtons[i];
                if (button == null)
                    continue;

                ArmyOrderType type = (ArmyOrderType)i;
                Sprite sprite = orderIcons != null && i < orderIcons.Length ? orderIcons[i] : null;

                button.Bind(UiText.Order(type), OrderHotkeys[i], sprite, () => IssueOrder(type));
            }

            BuildFormations();
            _built = true;
        }

        private void BuildFormations()
        {
            if (formationContainer == null || formationTemplate == null)
                return;

            FormationSetConfig set = Match.Config != null ? Match.Config.Formations : null;
            if (set == null)
                return;

            formationTemplate.gameObject.SetActive(false);

            for (int i = 0; i < set.Count; i++)
            {
                FormationConfig formation = set.Get(i);

                HotkeyButtonView button = Instantiate(formationTemplate, formationContainer);
                button.gameObject.SetActive(true);
                button.name = "Formation_" + i;

                int index = i;
                string hotkey = i < FormationHotkeys.Length ? FormationHotkeys[i] : string.Empty;
                string title = formation != null ? formation.displayName : "Строй " + (i + 1);

                button.Bind(title, hotkey, formation != null ? formation.icon : null, () => IssueFormation(index));
                _formationButtons.Add(button);
            }
        }

        private static void IssueOrder(ArmyOrderType type)
        {
            PlayerState player = PlayerState.Local;
            if (player == null)
                return;

            // Кнопка не указывает точку на земле, поэтому якорем служит сам полководец —
            // так же, как при нажатии клавиши без клика по карте.
            HeroController hero = HeroController.Local;
            Vector3 anchor = hero != null ? hero.Position : Vector3.zero;
            float yaw = hero != null ? hero.YawDegrees : 0f;

            player.CmdSetOrder((byte)type, anchor, yaw);
        }

        private static void IssueFormation(int index)
        {
            PlayerState.Local?.CmdSetFormation((byte)Mathf.Clamp(index, 0, byte.MaxValue));
        }
    }
}
