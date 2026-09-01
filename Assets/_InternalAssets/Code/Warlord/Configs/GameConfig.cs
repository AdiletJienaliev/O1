using UnityEngine;
using Warlord.Configs.Formations;
using Warlord.Configs.Upgrades;

namespace Warlord.Configs
{
    /// <summary>
    /// Корневой реестр конфигов. Единственная ссылка, которую нужно протащить в сцену —
    /// всё остальное достаётся отсюда. Это же точка расширения: добавили подсистему,
    /// добавили сюда её конфиг, и он доступен любому коду через контекст матча.
    /// </summary>
    [CreateAssetMenu(menuName = "Warlord/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Правила")]
        [SerializeField] private GameModeConfig gameMode;
        [SerializeField] private NetworkConfig network;
        [SerializeField] private MapConfig map;

        [Header("Полководец и армия")]
        [SerializeField] private HeroConfig hero;
        [SerializeField] private UnitRosterConfig roster;
        [SerializeField] private DamageMatrixConfig damageMatrix;
        [SerializeField] private CommandConfig command;
        [SerializeField] private FormationSetConfig formations;

        [Header("Прогрессия")]
        [SerializeField] private UpgradeTreeConfig upgradeTree;

        [Header("Захват")]
        [SerializeField] private CapturePointConfig centralFlag;
        [SerializeField] private CapturePointConfig baseFlag;

        [Header("Презентация")]
        [SerializeField] private TeamColorConfig teamColors;

        public GameModeConfig GameMode => gameMode;
        public NetworkConfig Network => network;
        public MapConfig Map => map;
        public HeroConfig Hero => hero;
        public UnitRosterConfig Roster => roster;
        public DamageMatrixConfig DamageMatrix => damageMatrix;
        public CommandConfig Command => command;
        public FormationSetConfig Formations => formations;
        public UpgradeTreeConfig UpgradeTree => upgradeTree;
        public CapturePointConfig CentralFlag => centralFlag;
        public CapturePointConfig BaseFlag => baseFlag;
        public TeamColorConfig TeamColors => teamColors;

        /// <summary>Проверка целостности ассета — вызывается бутстрапом до старта сети.</summary>
        public bool Validate(out string error)
        {
            if (gameMode == null) { error = "GameConfig: не задан GameModeConfig"; return false; }
            if (network == null) { error = "GameConfig: не задан NetworkConfig"; return false; }
            if (map == null) { error = "GameConfig: не задан MapConfig"; return false; }
            if (hero == null) { error = "GameConfig: не задан HeroConfig"; return false; }
            if (roster == null || roster.Count == 0) { error = "GameConfig: пустой UnitRosterConfig"; return false; }
            if (command == null) { error = "GameConfig: не задан CommandConfig"; return false; }
            if (formations == null || formations.Count == 0) { error = "GameConfig: пустой FormationSetConfig"; return false; }
            if (upgradeTree == null) { error = "GameConfig: не задан UpgradeTreeConfig"; return false; }
            if (centralFlag == null) { error = "GameConfig: не задан CapturePointConfig центрального флага"; return false; }

            for (int i = 0; i < roster.Count; i++)
            {
                UnitConfig unit = roster.Get(i);
                if (unit == null) { error = $"GameConfig: пустая запись ростера под индексом {i}"; return false; }
                if (unit.prefab == null) { error = $"GameConfig: у юнита {unit.unitId} не задан префаб"; return false; }
            }

            error = null;
            return true;
        }
    }
}
