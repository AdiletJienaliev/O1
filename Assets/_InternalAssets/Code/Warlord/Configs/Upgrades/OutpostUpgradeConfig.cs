using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Upgrades
{
    /// <summary>
    /// Улучшение аванпоста (ГДД §2.5). Владелец выбирает одно из трёх в момент захвата,
    /// и выбор держится, пока точка у него.
    ///
    /// Почему это ассет, а не три ветки в коде: эффекты разнородные — деньги, лимит армии,
    /// скорость постройки, лечение, цена охранников, — но правило стакинга у них общее,
    /// и держать его в одном месте можно только если сами улучшения одинаковы по форме.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Outpost Upgrade", fileName = "OutpostUpgrade")]
    public sealed class OutpostUpgradeConfig : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Тип улучшения. По нему считается стакинг: два одинаковых типа с разных точек складываются вполсилы.")]
        public OutpostUpgradeType type = OutpostUpgradeType.Supply;

        public string displayName = "Снабжение";

        [TextArea]
        public string description = "+5 золота/с, +3 к лимиту армии";

        public Sprite icon;

        [Header("Экономика")]
        [Tooltip("Прибавка к доходу владельца, золота/с.")]
        [Min(0f)] public float goldPerSecond;

        [Tooltip("Прибавка к общему лимиту армии. Именно она даёт место под гарнизон.")]
        [Min(0)] public int unitCapBonus;

        [Header("Темп")]
        [Tooltip("Насколько сокращается время постройки всех юнитов. 0.25 = на четверть быстрее.")]
        [Range(0f, 0.9f)] public float spawnTimeReduction;

        [Header("Лечение")]
        [Tooltip("Лечение своих юнитов вокруг точки, HP/с. 0 — аура выключена.")]
        [Min(0f)] public float healPerSecond;

        [Tooltip("Радиус лечащей ауры вокруг точки, м.")]
        [Min(0f)] public float healRadius = 15f;

        [Header("Охранники")]
        [Tooltip("Скидка на охранников, покупаемых на эту точку. 0.3 = на 30% дешевле.")]
        [Range(0f, 0.9f)] public float guardCostReduction;

        [Tooltip("Прибавка к здоровью охранников на этой точке. 0.3 = +30%.")]
        [Range(0f, 2f)] public float guardHealthBonus;

        [Header("Ростер")]
        [Tooltip("Юнит, который улучшение открывает в панели покупки. Пусто — ничего не открывает.")]
        public UnitConfig unlockedUnit;
    }
}
