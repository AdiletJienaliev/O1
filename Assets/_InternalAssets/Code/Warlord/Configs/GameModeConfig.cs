using UnityEngine;
using Warlord.Core;

namespace Warlord.Configs
{
    /// <summary>
    /// Правила режима (ГДД §15). Часть значений перекрывается настройками комнаты
    /// через <see cref="Warlord.Domain.Match.MatchSettings"/> — сам ассет никогда не мутируется в рантайме.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Game Mode", fileName = "GameModeConfig")]
    public sealed class GameModeConfig : ScriptableObject
    {
        [Header("Матч")]
        [Tooltip("Длительность матча в секундах.")]
        [Min(60f)] public float matchDuration = 900f;

        [Range(2, PlayerSlots.MaxSupported)] public int maxPlayers = 4;

        [Tooltip("Обратный отсчёт от старта матча до разблокировки управления.")]
        [Min(0f)] public float countdownDuration = 3f;

        [Header("Экономика")]
        [Min(0)] public int startingGold = 300;
        [Min(0f)] public float goldPerSecond = 8f;
        [Min(0f)] public float goldPerSecondPerFlag = 6f;
        [Min(0f)] public float goldPerSecondPerCapturedBase = 4f;
        [Min(0f)] public float xpPerSecondHoldingFlag = 3f;

        [Tooltip("Награда за убийство вражеского юнита. По умолчанию выключено (ГДД §4).")]
        [Min(0)] public int goldPerUnitKill;

        [Tooltip("Доля золота жертвы, уходящая захватчику базы. 1.0 = 100%.")]
        [Range(0f, 1f)] public float baseCaptureGoldSteal = 1f;

        [Header("Армия")]
        [Min(1)] public int maxUnits = 20;

        [Tooltip("Сколько юнитов строится параллельно из общей очереди.")]
        [Min(1)] public int parallelSpawnSlots = 1;

        [Min(0.1f)] public float unitSpawnGlobalMultiplier = 1f;
        [Min(0.1f)] public float unitCostGlobalMultiplier = 1f;

        [Header("База")]
        [Min(0f)] public float baseHealRadius = 10f;
        [Min(0f)] public float baseHealPerSecond = 5f;
        [Min(0f)] public float heroRespawnTime = 8f;

        [Tooltip("Неуязвимость полководца после респавна. 0 = выключено (открытый вопрос ГДД §17.5).")]
        [Min(0f)] public float spawnProtectionDuration;

        [Header("Победа")]
        [Tooltip("Порядок тай-брейков сверху вниз.")]
        public TiebreakRule[] winTiebreakOrder =
        {
            TiebreakRule.FlagHoldTime,
            TiebreakRule.FlagCaptures,
            TiebreakRule.BasesCaptured,
            TiebreakRule.UnitKills
        };
    }
}
