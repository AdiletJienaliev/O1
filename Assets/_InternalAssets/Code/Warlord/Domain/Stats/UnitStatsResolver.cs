using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Upgrades;

namespace Warlord.Domain.Stats
{
    /// <summary>
    /// Единственное место, где базовые статы из <see cref="UnitConfig"/> превращаются в боевые
    /// с учётом древа прокачки (ГДД §11). Один класс — одно место расчёта: это принципиально
    /// и для честности боя, и для того, чтобы сервер с клиентом считали одинаково.
    /// </summary>
    public sealed class UnitStatsResolver
    {
        private readonly UpgradeTreeConfig _tree;
        private readonly CommandConfig _command;

        public UnitStatsResolver(UpgradeTreeConfig tree, CommandConfig command)
        {
            _tree = tree;
            _command = command;
        }

        public UnitStats Resolve(UnitConfig unit, in UpgradeLevels levels)
        {
            if (unit == null)
                return default;

            float damageMultiplier = Multiplier(UpgradeBranch.Damage, levels);
            float healthMultiplier = Multiplier(UpgradeBranch.Health, levels);
            float rangeMultiplier = Multiplier(UpgradeBranch.AttackRange, levels);
            float speedMultiplier = Multiplier(UpgradeBranch.MoveSpeed, levels);

            float baseAggro = _command != null ? _command.attackAggroRadius : 15f;
            float aggroRadius = unit.aggroRadiusOverride > 0f ? unit.aggroRadiusOverride : baseAggro;

            return new UnitStats(
                maxHealth: Mathf.Max(1, Mathf.RoundToInt(unit.maxHealth * healthMultiplier)),
                damagePerHit: Mathf.Max(1, Mathf.RoundToInt(unit.damagePerHit * damageMultiplier)),
                attackInterval: unit.attackInterval,
                attackRange: unit.attackRange * rangeMultiplier,
                moveSpeed: unit.moveSpeed * speedMultiplier,

                // Шаг растёт вместе с бегом: иначе прокачка скорости ломала бы походку —
                // юнит носился бы по полю, а у слота всё так же еле переставлял ноги.
                walkSpeed: unit.moveSpeed * speedMultiplier * unit.walkSpeedFactor,
                runDistance: unit.runDistance,
                turnSpeed: unit.turnSpeed,
                armor: unit.armor,
                aggroRadius: aggroRadius,
                isRanged: unit.isRanged,
                projectileSpeed: unit.projectileSpeed,

                // Радиус взрыва прокачкой дальности не растёт: иначе ветка дальнобойности
                // превращалась бы для мага в двойной множитель урона.
                splashRadius: unit.splashRadius,
                splashDamageFactor: unit.splashDamageFactor,

                // Щит прокачкой не трогается: это свойство снаряжения, а не характеристика.
                hasShield: unit.hasShield,
                blockAngle: unit.blockAngle,
                blockDamageFactor: unit.blockDamageFactor);
        }

        /// <summary>Лимит живых юнитов: база из режима плюс плоский бонус ветки UnitCap.</summary>
        public int ResolveUnitCap(int baseCap, in UpgradeLevels levels)
        {
            if (_tree == null)
                return baseCap;

            float flatBonus = _tree.GetAccumulatedValue(UpgradeBranch.UnitCap, levels.Get(UpgradeBranch.UnitCap));
            return Mathf.Max(1, baseCap + Mathf.RoundToInt(flatBonus));
        }

        private float Multiplier(UpgradeBranch branch, in UpgradeLevels levels)
        {
            if (_tree == null)
                return 1f;

            int level = levels.Get(branch);
            if (level <= 0)
                return 1f;

            return Mathf.Max(0.01f, _tree.GetAccumulatedValue(branch, level));
        }
    }
}
