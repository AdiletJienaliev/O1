using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs.Upgrades;
using Warlord.Core;

namespace Warlord.Domain.Upgrades
{
    /// <summary>
    /// Суммарный эффект улучшений со всех аванпостов одного игрока (ГДД §2.5).
    ///
    /// Правило стакинга: эффекты разных улучшений складываются полностью, а одинаковых —
    /// с убыванием: первая точка даёт 100 %, вторая 50 %, третья и дальше 0 %. Без этого
    /// оптимальной игрой было бы «беру четыре кузницы», и выбор из трёх превращался бы
    /// в выбор один раз за матч.
    ///
    /// Чистый C# без сцены: на вход — список выбранных улучшений, на выходе — числа.
    /// </summary>
    public sealed class OutpostUpgradeStack
    {
        /// <summary>Доли, с которыми учитывается первая, вторая и последующие копии одного улучшения.</summary>
        private static readonly float[] StackWeights = { 1f, 0.5f, 0f };

        private readonly List<OutpostUpgradeConfig> _sources = new(4);
        private readonly Dictionary<OutpostUpgradeType, int> _seen = new(4);

        /// <summary>Прибавка к доходу, золота/с.</summary>
        public float GoldPerSecond { get; private set; }

        /// <summary>Прибавка к общему лимиту армии.</summary>
        public int UnitCapBonus { get; private set; }

        /// <summary>Сокращение времени постройки, доля от 0 до 1.</summary>
        public float SpawnTimeReduction { get; private set; }

        /// <summary>Растёт при каждой пересборке: потребители сравнивают её со своей копией.</summary>
        public int Version { get; private set; }

        /// <summary>Множитель времени постройки. Готовое число, чтобы очередь не считала его сама.</summary>
        public float SpawnTimeMultiplier => Mathf.Clamp(1f - SpawnTimeReduction, 0.1f, 1f);

        /// <summary>Улучшения, которые сейчас работают. В порядке захвата точек.</summary>
        public IReadOnlyList<OutpostUpgradeConfig> Sources => _sources;

        /// <summary>Открыт ли игроку этот тип юнита хотя бы одним аванпостом («Наёмники»).</summary>
        public bool UnlocksUnit(Warlord.Configs.UnitConfig unit)
        {
            if (unit == null)
                return false;

            for (int i = 0; i < _sources.Count; i++)
            {
                if (_sources[i] != null && _sources[i].unlockedUnit == unit)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Пересобрать из текущего набора улучшений. Вызывается при захвате и потере точки,
        /// а не каждый такт: набор меняется единицы раз за матч.
        /// </summary>
        public void Rebuild(IReadOnlyList<OutpostUpgradeConfig> upgrades)
        {
            _sources.Clear();
            _seen.Clear();

            GoldPerSecond = 0f;
            UnitCapBonus = 0;
            SpawnTimeReduction = 0f;

            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.Count; i++)
                    Accumulate(upgrades[i]);
            }

            Version++;
        }

        /// <summary>
        /// Цена охранника на конкретной точке. Скидка «Наёмников» привязана к месту, а не
        /// к игроку, поэтому в общую сумму не входит и не стакается: дешёвая оборона —
        /// свойство этого аванпоста, а не всего, что игрок держит.
        /// </summary>
        public static int ResolveGuardCost(int baseCost, OutpostUpgradeConfig upgrade)
        {
            if (upgrade == null)
                return baseCost;

            return Mathf.Max(0, Mathf.RoundToInt(baseCost * (1f - upgrade.guardCostReduction)));
        }

        private void Accumulate(OutpostUpgradeConfig upgrade)
        {
            if (upgrade == null)
                return;

            _sources.Add(upgrade);

            _seen.TryGetValue(upgrade.type, out int copies);
            _seen[upgrade.type] = copies + 1;

            float weight = copies < StackWeights.Length ? StackWeights[copies] : 0f;

            if (weight <= 0f)
                return;

            GoldPerSecond += upgrade.goldPerSecond * weight;
            SpawnTimeReduction += upgrade.spawnTimeReduction * weight;

            // Лимит армии — целые слоты: половина слота от второй точки никому не нужна,
            // поэтому округляем вниз, а не копим дробную часть.
            UnitCapBonus += Mathf.FloorToInt(upgrade.unitCapBonus * weight);
        }
    }
}
