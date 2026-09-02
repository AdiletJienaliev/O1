using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Состояние полководца: здоровье, смерть и признак нахождения в зоне покупки (ГДД §8).
    /// Полководца ищем сами — <see cref="PlayerState.Hero"/> существует только на сервере.
    /// </summary>
    public sealed class HeroVitalsWidget : HudWidget
    {
        [Header("Здоровье")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private Image healthFill;
        [SerializeField] private TextMeshProUGUI healthLabel;

        [Header("Смерть")]
        [SerializeField] private GameObject deathRoot;
        [SerializeField] private TextMeshProUGUI deathLabel;

        [Header("Зона покупки")]
        [SerializeField] private GameObject buyZoneBadge;

        [Header("Портрет")]
        [SerializeField] private Image portraitFrame;
        [SerializeField] private TextMeshProUGUI nameLabel;

        [Header("Цвета шкалы")]
        [SerializeField] private Color healthyColor = new(0.42f, 0.82f, 0.44f);
        [SerializeField] private Color woundedColor = new(0.98f, 0.76f, 0.28f);
        [SerializeField] private Color criticalColor = new(0.94f, 0.35f, 0.35f);

        private int _lastSlot = -1;

        public override void Refresh(PlayerState player)
        {
            if (player == null || !HasMatch)
                return;

            ApplySlotIdentity(player.Slot);

            HeroController hero = HeroController.Local;
            HeroConfig config = Match.Config != null ? Match.Config.Hero : null;

            int maxHealth = config != null ? config.maxHealth : 1;
            int health = hero != null ? hero.Health : 0;
            bool alive = hero != null && hero.IsAlive;

            ApplyHealth(health, maxHealth);
            ApplyDeath(player, hero, alive);

            if (buyZoneBadge != null)
                buyZoneBadge.SetActive(MatchQuery.IsHeroInsideBuyZone(Match.Config, hero, player.Slot));
        }

        private void ApplySlotIdentity(int slot)
        {
            if (slot == _lastSlot)
                return;

            _lastSlot = slot;

            TeamColorConfig colors = Match.Config != null ? Match.Config.TeamColors : null;
            Color color = TeamPalette.Primary(colors, slot);

            if (portraitFrame != null)
                portraitFrame.color = color;

            if (nameLabel != null)
            {
                nameLabel.text = UiText.PlayerName(slot);
                nameLabel.color = color;
            }
        }

        private void ApplyHealth(int health, int maxHealth)
        {
            float normalized = maxHealth > 0 ? Mathf.Clamp01(health / (float)maxHealth) : 0f;

            if (healthBar != null)
                healthBar.value = normalized;

            if (healthFill != null)
                healthFill.color = normalized > 0.5f ? healthyColor : normalized > 0.25f ? woundedColor : criticalColor;

            if (healthLabel != null)
                healthLabel.text = health + " / " + maxHealth;
        }

        private void ApplyDeath(PlayerState player, HeroController hero, bool alive)
        {
            if (deathRoot == null)
                return;

            bool eliminated = player.IsEliminated;
            bool dead = !alive && hero != null;

            deathRoot.SetActive(eliminated || dead);

            if (deathLabel == null)
                return;

            if (eliminated)
            {
                deathLabel.text = "ВЫ ВЫБЫЛИ";
                return;
            }

            if (!dead)
                return;

            // Таймер респавна живёт только на сервере: у хоста он настоящий,
            // у чистого клиента показываем факт смерти без секунд.
            float remaining = hero.RespawnTimeRemaining;
            deathLabel.text = remaining > 0.05f && remaining < 999f
                ? "Возрождение через " + Mathf.CeilToInt(remaining)
                : "Полководец пал";
        }
    }
}
