using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Timing;
using TMPro;
using UnityEngine;
using Warlord.Configs;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Очередь постройки владельца (ГДД §5.1). Прогресс не приходит по сети покадрово:
    /// сервер шлёт тик завершения, а полоса считается локально от текущего тика.
    /// </summary>
    public sealed class SpawnQueueWidget : HudWidget
    {
        [Header("Слоты")]
        [SerializeField] private RectTransform container;
        [SerializeField] private SpawnQueueSlotView slotTemplate;

        [Tooltip("Сколько слотов очереди показывать одновременно.")]
        [SerializeField] private int visibleSlots = 6;

        [Tooltip("Иконки по индексу ростера — те же, что в панели покупки.")]
        [SerializeField] private Sprite[] fallbackIcons;

        [Header("Пусто")]
        [SerializeField] private GameObject emptyHint;
        [SerializeField] private TextMeshProUGUI counterLabel;

        private readonly List<SpawnQueueSlotView> _slots = new(8);
        private int _lastCount = -1;

        protected override void OnInitialized() => BuildSlots();

        public override void Refresh(PlayerState player)
        {
            if (!HasMatch || player == null || _slots.Count == 0)
                return;

            IReadOnlyList<SpawnTicket> queue = player.SpawnQueue;
            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;

            TimeManager time = InstanceFinder.TimeManager;
            uint currentTick = time != null ? time.Tick : 0u;
            float tickDelta = time != null ? (float)time.TickDelta : 0.02f;

            int shown = Mathf.Min(queue.Count, _slots.Count);

            for (int i = 0; i < shown; i++)
            {
                SpawnTicket ticket = queue[i];
                UnitConfig unit = roster != null ? roster.Get(ticket.RosterIndex) : null;

                float progress = 0f;
                float secondsLeft = 0f;

                if (ticket.InProgress && ticket.DurationTicks > 0)
                {
                    long ticksLeft = (long)ticket.FinishTick - currentTick;
                    if (ticksLeft < 0L)
                        ticksLeft = 0L;

                    progress = 1f - ticksLeft / (float)ticket.DurationTicks;
                    secondsLeft = ticksLeft * tickDelta;
                }

                _slots[i].Show(unit, FallbackIcon(ticket.RosterIndex), progress, secondsLeft, ticket.InProgress);
            }

            for (int i = shown; i < _slots.Count; i++)
                _slots[i].Hide();

            if (emptyHint != null)
                emptyHint.SetActive(queue.Count == 0);

            if (counterLabel != null && queue.Count != _lastCount)
            {
                _lastCount = queue.Count;
                counterLabel.text = _lastCount > _slots.Count ? "+" + (_lastCount - _slots.Count) : string.Empty;
            }
        }

        private void BuildSlots()
        {
            if (_slots.Count > 0 || container == null || slotTemplate == null)
                return;

            slotTemplate.gameObject.SetActive(false);

            for (int i = 0; i < Mathf.Max(1, visibleSlots); i++)
            {
                SpawnQueueSlotView slot = Instantiate(slotTemplate, container);
                slot.name = "QueueSlot_" + i;
                slot.Hide();
                _slots.Add(slot);
            }
        }

        private Sprite FallbackIcon(int index)
        {
            return fallbackIcons != null && index >= 0 && index < fallbackIcons.Length ? fallbackIcons[index] : null;
        }
    }
}
