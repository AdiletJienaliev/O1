using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Панель покупки юнитов (ГДД §5.1). Карточки строятся из ростера, поэтому добавление
    /// нового типа юнита в конфиг не требует правок ни префаба, ни кода.
    /// </summary>
    public sealed class UnitShopWidget : HudWidget
    {
        [Header("Карточки")]
        [SerializeField] private RectTransform container;
        [SerializeField] private UnitCardView cardTemplate;

        [Tooltip("Иконки по индексу ростера. Используются, если у UnitConfig не задана своя.")]
        [SerializeField] private Sprite[] fallbackIcons;

        [Header("Подсказка")]
        [SerializeField] private GameObject buyZoneHint;
        [SerializeField] private TextMeshProUGUI buyZoneHintLabel;

        private readonly List<UnitCardView> _cards = new(8);
        private bool _built;

        protected override void OnInitialized()
        {
            BuildCards();
        }

        public override void Refresh(PlayerState player)
        {
            if (!HasMatch || player == null)
                return;

            BuildCards();

            GameConfig config = Match.Config;
            UnitRosterConfig roster = config != null ? config.Roster : null;
            if (roster == null)
                return;

            HeroController hero = HeroController.Local;
            bool inZone = MatchQuery.IsHeroInsideBuyZone(config, hero, player.Slot);
            bool running = Match.Phase == MatchPhase.Running && !player.IsEliminated;
            // Гарнизон занимает те же слоты лимита, что и армия (ГДД §1.1), поэтому в проверку
            // «есть ли место» он входит наравне с полевыми юнитами и очередью постройки.
            bool hasRoom = player.ArmyCount + player.GarrisonCount + player.SpawnQueue.Count < player.UnitCap;

            if (buyZoneHint != null)
                buyZoneHint.SetActive(running && !inZone);

            if (buyZoneHintLabel != null && running && !inZone)
                buyZoneHintLabel.text = "Покупка доступна только на своей базе";

            for (int i = 0; i < _cards.Count; i++)
            {
                UnitCardView card = _cards[i];
                UnitConfig unit = roster.Get(card.RosterIndex);
                int cost = Match.Settings.ResolveUnitCost(unit, config.GameMode);

                bool affordable = player.Gold >= cost;
                card.SetCost(cost);
                card.SetState(running && inZone && hasRoom && affordable, affordable);
            }
        }

        private void BuildCards()
        {
            if (_built || !HasMatch || container == null || cardTemplate == null)
                return;

            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;
            if (roster == null)
                return;

            cardTemplate.gameObject.SetActive(false);

            for (int i = 0; i < roster.Count; i++)
            {
                // Охранник в эту панель не попадает: его покупают вместе с точкой,
                // в панели гарнизона (ГДД §1.6). Карточка без выбора точки была бы кнопкой,
                // которую сервер отклоняет всегда.
                UnitConfig entry = roster.Get(i);
                if (entry != null && entry.isGarrison)
                    continue;

                UnitCardView card = Instantiate(cardTemplate, container);
                card.gameObject.SetActive(true);
                card.name = "UnitCard_" + i;
                card.Bind(i, roster.Get(i), FallbackIcon(i), Purchase);
                _cards.Add(card);
            }

            _built = true;
        }

        private Sprite FallbackIcon(int index)
        {
            return fallbackIcons != null && index >= 0 && index < fallbackIcons.Length ? fallbackIcons[index] : null;
        }

        private static void Purchase(int rosterIndex)
        {
            // Команда уходит напрямую владельцу состояния: сервер всё равно всё проверит заново.
            PlayerState.Local?.CmdPurchaseUnit((byte)Mathf.Clamp(rosterIndex, 0, byte.MaxValue));
        }
    }
}
