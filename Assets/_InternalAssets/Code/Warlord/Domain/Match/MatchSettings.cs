using System;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;

namespace Warlord.Domain.Match
{
    /// <summary>
    /// Настройки конкретной комнаты (ГДД §2). Перекрывают дефолты <see cref="GameModeConfig"/>,
    /// сам ассет остаётся неизменным. Структура ходит по сети целиком при старте матча.
    /// </summary>
    [Serializable]
    public struct MatchSettings
    {
        public float MatchDuration;
        public byte SlotCount;
        public int StartingGold;
        public float IncomeMultiplier;
        public float CostMultiplier;
        public byte MapIndex;

        /// <summary>
        /// Раскладка команд, упакованная в четыре байта. Полем, а не структурой:
        /// сериализатор сети берёт публичные поля, и прятать раскладку внутрь
        /// значило бы оставить клиента без неё. Читать — через <see cref="Teams"/>.
        /// </summary>
        public uint TeamPacking;

        /// <summary>Кто с кем в команде. Пустая упаковка — обычный FFA (ГДД §2).</summary>
        public TeamLayout Teams => new(TeamPacking);

        public static MatchSettings FromConfig(GameModeConfig mode)
        {
            return new MatchSettings
            {
                MatchDuration = mode != null ? mode.matchDuration : 900f,
                SlotCount = (byte)(mode != null ? mode.maxPlayers : 4),
                StartingGold = mode != null ? mode.startingGold : 300,
                IncomeMultiplier = 1f,
                CostMultiplier = mode != null ? mode.unitCostGlobalMultiplier : 1f,
                MapIndex = 0
            };
        }

        /// <summary>Защита от мусора, пришедшего из лобби: значения зажимаются в разумные рамки.</summary>
        public MatchSettings Sanitized(GameModeConfig mode)
        {
            MatchSettings copy = this;
            copy.MatchDuration = Mathf.Clamp(MatchDuration, 60f, 3600f);
            copy.SlotCount = (byte)Mathf.Clamp(SlotCount, 2, mode != null ? mode.maxPlayers : 4);
            copy.StartingGold = Mathf.Clamp(StartingGold, 0, 100000);
            copy.IncomeMultiplier = Mathf.Clamp(IncomeMultiplier, 0.1f, 10f);
            copy.CostMultiplier = Mathf.Clamp(CostMultiplier, 0.1f, 10f);
            return copy;
        }

        /// <summary>Итоговая цена юнита с учётом глобального множителя режима и множителя комнаты.</summary>
        public int ResolveUnitCost(UnitConfig unit, GameModeConfig mode)
        {
            if (unit == null)
                return int.MaxValue;

            float global = mode != null ? mode.unitCostGlobalMultiplier : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(unit.cost * global * CostMultiplier));
        }

        /// <summary>Итоговое время постройки юнита.</summary>
        public float ResolveSpawnTime(UnitConfig unit, GameModeConfig mode)
        {
            if (unit == null)
                return 0f;

            float global = mode != null ? mode.unitSpawnGlobalMultiplier : 1f;
            return Mathf.Max(0.05f, unit.spawnTime * global);
        }
    }
}
