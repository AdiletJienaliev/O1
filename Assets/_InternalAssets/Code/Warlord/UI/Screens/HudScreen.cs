using UnityEngine;
using UnityEngine.UI;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Screens
{
    /// <summary>
    /// Боевой HUD. Единственное место с Update во всём интерфейсе матча: виджеты обновляются
    /// отсюда, поэтому за кадр они видят одно и то же состояние игрока и не расходятся.
    ///
    /// Панели поверх боя взаимно исключают друг друга: две открытые панели забирают
    /// пол-экрана и обе требуют курсор, а бой в это время продолжается.
    /// </summary>
    public sealed class HudScreen : UiScreen
    {
        [Header("Виджеты")]
        [SerializeField] private HudWidget[] widgets = System.Array.Empty<HudWidget>();

        [Header("Панель прокачки")]
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private Button upgradeToggleButton;
        [SerializeField] private Button upgradeCloseButton;
        [SerializeField] private KeyCode upgradeHotkey = KeyCode.Tab;

        [Header("Панель расстановки армии")]
        [SerializeField] private GameObject presetPanel;
        [SerializeField] private Button presetToggleButton;
        [SerializeField] private Button presetCloseButton;
        [SerializeField] private KeyCode presetHotkey = KeyCode.B;

        [Header("Панель гарнизона")]
        [SerializeField] private GameObject garrisonPanel;
        [SerializeField] private Button garrisonToggleButton;
        [SerializeField] private Button garrisonCloseButton;
        [SerializeField] private KeyCode garrisonHotkey = KeyCode.G;

        private MatchManager _match;
        private bool _initialized;

        /// <summary>Открыта панель поверх боя — курсор нужно отпустить, камера должна замереть.</summary>
        public bool WantsCursor => IsOpen(upgradePanel) || IsOpen(presetPanel) || IsOpen(garrisonPanel);

        protected override void Awake()
        {
            base.Awake();

            if (upgradeToggleButton != null)
                upgradeToggleButton.onClick.AddListener(() => Toggle(upgradePanel));

            if (upgradeCloseButton != null)
                upgradeCloseButton.onClick.AddListener(() => Close(upgradePanel));

            if (presetToggleButton != null)
                presetToggleButton.onClick.AddListener(() => Toggle(presetPanel));

            if (presetCloseButton != null)
                presetCloseButton.onClick.AddListener(() => Close(presetPanel));

            if (garrisonToggleButton != null)
                garrisonToggleButton.onClick.AddListener(() => Toggle(garrisonPanel));

            if (garrisonCloseButton != null)
                garrisonCloseButton.onClick.AddListener(() => Close(garrisonPanel));

            CloseAll();
        }

        /// <summary>Прокидывает матч во все виджеты. Повторные вызовы безопасны.</summary>
        public void Initialize(MatchManager match)
        {
            if (_initialized && _match == match)
                return;

            if (_initialized)
                Shutdown();

            _match = match;

            if (_match == null)
                return;

            for (int i = 0; i < widgets.Length; i++)
                widgets[i]?.Initialize(_match);

            _initialized = true;
        }

        public void Shutdown()
        {
            if (!_initialized)
                return;

            for (int i = 0; i < widgets.Length; i++)
                widgets[i]?.Shutdown();

            _initialized = false;
            _match = null;
        }

        private void OnDestroy() => Shutdown();

        private void Update()
        {
            if (!IsVisible || !_initialized)
                return;

            if (Input.GetKeyDown(upgradeHotkey))
                Toggle(upgradePanel);

            if (Input.GetKeyDown(presetHotkey))
                Toggle(presetPanel);

            if (Input.GetKeyDown(garrisonHotkey))
                Toggle(garrisonPanel);

            PlayerState player = PlayerState.Local;

            for (int i = 0; i < widgets.Length; i++)
                widgets[i]?.Refresh(player);
        }

        protected override void OnHidden() => CloseAll();

        private void Toggle(GameObject panel)
        {
            if (panel == null)
                return;

            bool open = !panel.activeSelf;

            CloseAll();
            panel.SetActive(open);
        }

        private void CloseAll()
        {
            Close(upgradePanel);
            Close(presetPanel);
            Close(garrisonPanel);
        }

        private static void Close(GameObject panel)
        {
            if (panel != null && panel.activeSelf)
                panel.SetActive(false);
        }

        private static bool IsOpen(GameObject panel) => panel != null && panel.activeSelf;
    }
}
