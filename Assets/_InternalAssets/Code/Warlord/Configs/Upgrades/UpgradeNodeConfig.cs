using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs.Upgrades
{
    /// <summary>Один перк древа прокачки (ГДД §11).</summary>
    [CreateAssetMenu(menuName = "Warlord/Upgrades/Node", fileName = "UpgradeNode")]
    public sealed class UpgradeNodeConfig : ScriptableObject
    {
        [Header("Идентификация")]
        public string nodeId = "damage_1";
        public UpgradeBranch branch = UpgradeBranch.Damage;

        [Tooltip("Уровень внутри ветки. Уровни линейные: второй нельзя взять без первого.")]
        [Range(1, 8)] public int level = 1;

        [Header("Стоимость")]
        [Min(0)] public int xpCost = 60;

        [Header("Эффект")]
        public ModifierType modifierType = ModifierType.Multiplicative;

        [Tooltip("Multiplicative: 0.08 = +8%. Flat: 3 = +3 к лимиту юнитов.")]
        public float value = 0.08f;

        [Header("Презентация")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
