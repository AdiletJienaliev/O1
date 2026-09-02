using UnityEngine;
using UnityEngine.UI;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Screens
{
    /// <summary>
    /// Боевой HUD. Единственное место с Update во всём интерфейсе матча: виджеты обновляются
    /// отсюда, поэтому за кадр они видят одно и то же состояние игрока и не расходятся.
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

        private MatchManager _match;
        private bool _initialized;

        /// <summary>Открыта панель поверх боя — курсор нужно отпустить, камера должна замереть.</summary>
        public bool WantsCursor => upgradePanel != null && upgradePanel.activeSelf;

        protected override void Awake()
        {
            base.Awake();

            if (upgradeToggleButton != null)
                upgradeToggleButton.onClick.AddListener(ToggleUpgradePanel);

            if (upgradeCloseButton != null)
                upgradeCloseButton.onClick.AddListener(CloseUpgradePanel);

            if (upgradePanel != null)
                upgradePanel.SetActive(false);
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
                ToggleUpgradePanel();

            PlayerState player = PlayerState.Local;

            for (int i = 0; i < widgets.Length; i++)
                widgets[i]?.Refresh(player);
        }

        protected override void OnHidden() => CloseUpgradePanel();

        private void ToggleUpgradePanel()
        {
            if (upgradePanel != null)
                upgradePanel.SetActive(!upgradePanel.activeSelf);
        }

        private void CloseUpgradePanel()
        {
            if (upgradePanel != null)
                upgradePanel.SetActive(false);
        }
    }
}
