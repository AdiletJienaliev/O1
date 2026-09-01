using UnityEngine;

namespace Warlord.Configs
{
    /// <summary>Сетевые параметры (ГДД §12, §15).</summary>
    [CreateAssetMenu(menuName = "Warlord/Network", fileName = "NetworkConfig")]
    public sealed class NetworkConfig : ScriptableObject
    {
        [Header("Симуляция")]
        [Tooltip("Частота единого боевого такта. Весь урон разрешается на этой частоте.")]
        [Range(10, 60)] public int combatTickRate = 20;

        [Header("Репликация юнитов")]
        [Tooltip("Частота отправки сжатых трансформов юнитов.")]
        [Range(5, 30)] public int unitSyncRate = 15;

        [Tooltip("Дальность видимости юнита для наблюдателя, м.")]
        [Min(10f)] public float unitObserverRange = 90f;

        [Tooltip("Задержка интерполяции на клиенте, с. Юниты не предсказываются.")]
        [Range(0.02f, 0.5f)] public float interpolationDelay = 0.1f;

        [Header("Лаг-компенсация")]
        [Tooltip("Максимальная перемотка позиций цели при валидации удара полководца, мс.")]
        [Range(0, 500)] public int lagCompensationCapMs = 200;

        [Tooltip("Глубина истории позиций для лаг-компенсации, с.")]
        [Range(0.2f, 2f)] public float lagCompensationHistorySeconds = 1f;

        [Header("Хостинг")]
        [Tooltip("Хост играет как обычный игрок (ГДД §12). false = выделенный сервер.")]
        public bool enableHostPlayer = true;

        public float CombatTickDelta => 1f / Mathf.Max(1, combatTickRate);
        public float UnitSyncInterval => 1f / Mathf.Max(1, unitSyncRate);
        public float LagCompensationCapSeconds => lagCompensationCapMs / 1000f;
    }
}
