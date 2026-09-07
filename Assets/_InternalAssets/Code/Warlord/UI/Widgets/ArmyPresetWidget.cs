using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.Configs;
using Warlord.Configs.Formations;
using Warlord.Domain.Formations;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Панель расстановки армии: игрок выбирает тип юнита слева и рисует им по сетке справа.
    /// Получившийся пресет — это ещё одно построение в общем списке, поэтому дальше он
    /// переключается той же кнопкой, что линия и квадрат.
    ///
    /// Пресет живёт на клиенте и хранится в PlayerPrefs между матчами: это настройка игрока,
    /// а не состояние боя. На сервер он уходит целиком — при нажатии «Применить» и один раз
    /// автоматически при входе в матч, чтобы сохранённая расстановка работала без открытия панели.
    /// </summary>
    public sealed class ArmyPresetWidget : HudWidget
    {
        private const string StorageKey = "warlord.army.preset";

        [Header("Палитра типов")]
        [SerializeField] private RectTransform paletteContainer;
        [SerializeField] private HotkeyButtonView paletteTemplate;

        [Header("Поле расстановки")]
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private ArmyPresetCellView cellTemplate;

        [Tooltip("Ширина поля в клетках. Нечётное значение даёт ровный центр строя.")]
        [Range(3, 21)] [SerializeField] private int columns = 11;

        [Tooltip("Глубина поля в шеренгах.")]
        [Range(2, 12)] [SerializeField] private int rows = 6;

        [Header("Радиусы атаки")]
        [Tooltip("Слой колец под сеткой. В иерархии должен идти раньше сетки, иначе перекроет иконки.")]
        [SerializeField] private RectTransform rangeContainer;

        [Tooltip("Кольцо радиуса. Копируется по одному на каждого расставленного юнита.")]
        [SerializeField] private Image rangeTemplate;

        [Tooltip("Прозрачность колец у типов, не выбранных сейчас в палитре.")]
        [Range(0f, 1f)] [SerializeField] private float dimRangeAlpha = 0.18f;

        [Tooltip("Прозрачность колец у выбранного типа.")]
        [Range(0f, 1f)] [SerializeField] private float activeRangeAlpha = 0.5f;

        [Header("Кнопки")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private TextMeshProUGUI hintLabel;

        [Header("Иконки")]
        [Tooltip("Запасная иконка для типов, у которых своя не задана.")]
        [SerializeField] private Sprite fallbackIcon;

        private readonly List<ArmyPresetCellView> _cells = new(96);
        private readonly List<HotkeyButtonView> _palette = new(8);
        private readonly List<Image> _rings = new(64);

        private GridLayoutGroup _gridLayout;
        private float _metersPerCell = 1.8f;

        private int _selectedType;
        private bool _painting;
        private bool _erasing;
        private bool _built;
        private bool _pushedOnce;

        protected override void OnInitialized()
        {
            Build();
            Load();
        }

        public override void Refresh(PlayerState player)
        {
            Build();

            // Мазок заканчивается вместе с кнопкой мыши. Ловим это здесь, а не в клетке:
            // отпустить кнопку игрок может и за пределами сетки.
            if (_painting && !Input.GetMouseButton(0))
                _painting = false;

            // Кольца пересчитываются покадрово, а не по событию: клетки расставляет
            // GridLayoutGroup, и итоговые позиции известны только после его прохода.
            if (gameObject.activeInHierarchy)
                RefreshRanges();

            if (player == null || _pushedOnce)
                return;

            // Сохранённая расстановка должна работать сразу, без открытия панели.
            Push(player);
            _pushedOnce = true;
        }

        private void Build()
        {
            if (_built || !HasMatch)
                return;

            _built = true;

            BuildPalette();
            BuildGrid();

            if (applyButton != null)
                applyButton.onClick.AddListener(Apply);

            if (clearButton != null)
                clearButton.onClick.AddListener(ClearAll);

            UpdateHint();
        }

        private void BuildPalette()
        {
            if (paletteContainer == null || paletteTemplate == null)
                return;

            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;
            if (roster == null)
                return;

            paletteTemplate.gameObject.SetActive(false);

            for (int i = 0; i < roster.Count; i++)
            {
                UnitConfig unit = roster.Get(i);

                HotkeyButtonView button = Instantiate(paletteTemplate, paletteContainer);
                button.gameObject.SetActive(true);
                button.name = "Palette_" + i;

                int index = i;
                Sprite sprite = unit != null && unit.icon != null ? unit.icon : fallbackIcon;

                button.Bind(UiText.UnitName(unit, i), string.Empty, sprite, () => SelectType(index));
                _palette.Add(button);
            }

            SelectType(0);
        }

        private void BuildGrid()
        {
            if (gridContainer == null || cellTemplate == null)
                return;

            cellTemplate.gameObject.SetActive(false);

            ResolveGridSize();

            _gridLayout = gridContainer.GetComponent<GridLayoutGroup>();

            int center = (columns - 1) / 2;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    ArmyPresetCellView cell = Instantiate(cellTemplate, gridContainer);
                    cell.gameObject.SetActive(true);
                    cell.name = $"Cell_{column}_{row}";

                    cell.Bind(column - center, row, Paint);
                    _cells.Add(cell);
                }
            }
        }

        /// <summary>
        /// Размер поля берётся у самого построения-пресета, если оно есть в наборе:
        /// иначе игрок расставил бы армию шире, чем построение умеет разложить.
        /// </summary>
        private void ResolveGridSize()
        {
            FormationSetConfig set = Match.Config != null ? Match.Config.Formations : null;
            if (set == null)
                return;

            for (int i = 0; i < set.Count; i++)
            {
                if (set.Get(i) is not PresetFormationConfig preset)
                    continue;

                columns = preset.gridColumns;
                rows = preset.gridRows;

                // Шаг строя в метрах: он же переводит радиус атаки из метров в пиксели поля.
                _metersPerCell = preset.slotSpacing;
                return;
            }
        }

        private void SelectType(int rosterIndex)
        {
            _selectedType = rosterIndex;

            for (int i = 0; i < _palette.Count; i++)
                _palette[i].SetSelected(i == rosterIndex);

            UpdateHint();
        }

        #region Радиусы атаки

        /// <summary>
        /// Кольцо радиуса атаки под каждым расставленным юнитом. Это единственное место,
        /// где дальность типов можно сравнить до боя: в бою радиусы не показываются,
        /// а по цифрам в карточке покупки строй не спланируешь.
        ///
        /// Кольца выбранного типа ярче остальных: два десятка одинаково заметных окружностей
        /// сливаются в кашу и перестают что-либо объяснять.
        /// </summary>
        private void RefreshRanges()
        {
            if (rangeContainer == null || rangeTemplate == null)
                return;

            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;
            if (roster == null)
                return;

            float pixelsPerMeter = ResolvePixelsPerMeter();
            int used = 0;

            for (int i = 0; i < _cells.Count; i++)
            {
                ArmyPresetCellView cell = _cells[i];
                if (cell.RosterIndex < 0)
                    continue;

                UnitConfig unit = roster.Get(cell.RosterIndex);
                if (unit == null || unit.attackRange <= 0f)
                    continue;

                Image ring = RentRing(used++);

                float diameter = unit.attackRange * pixelsPerMeter * 2f;
                ring.rectTransform.sizeDelta = new Vector2(diameter, diameter);
                ring.rectTransform.position = cell.transform.position;
                ring.color = RangeColor(cell.RosterIndex);
            }

            for (int i = used; i < _rings.Count; i++)
            {
                if (_rings[i].gameObject.activeSelf)
                    _rings[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Сколько пикселей поля приходится на метр строя. Считается по шагу сетки.</summary>
        private float ResolvePixelsPerMeter()
        {
            float pitch = _gridLayout != null
                ? _gridLayout.cellSize.x + _gridLayout.spacing.x
                : 68f;

            return pitch / Mathf.Max(0.01f, _metersPerCell);
        }

        private Image RentRing(int index)
        {
            while (_rings.Count <= index)
            {
                Image ring = Instantiate(rangeTemplate, rangeContainer);
                ring.name = "Range_" + _rings.Count;
                _rings.Add(ring);
            }

            Image rented = _rings[index];

            if (!rented.gameObject.activeSelf)
                rented.gameObject.SetActive(true);

            return rented;
        }

        /// <summary>
        /// Цвет кольца по типу. Оттенок разводится золотым сечением — так пять типов
        /// получают заведомо различимые цвета, и шестой ничего не ломает.
        /// </summary>
        private Color RangeColor(int rosterIndex)
        {
            float hue = Mathf.Repeat(rosterIndex * 0.618034f + 0.08f, 1f);
            Color color = Color.HSVToRGB(hue, 0.7f, 1f);

            color.a = rosterIndex == _selectedType ? activeRangeAlpha : dimRangeAlpha;
            return color;
        }

        #endregion

        /// <summary>
        /// Рисование по сетке. Режим — ставить или стирать — определяется первым нажатием
        /// и держится до конца мазка: иначе проведённая по строю линия то ставила бы юнитов,
        /// то убирала их обратно.
        /// </summary>
        private void Paint(ArmyPresetCellView cell, bool started)
        {
            if (started)
            {
                _painting = true;
                _erasing = cell.RosterIndex == _selectedType;
            }
            else if (!_painting)
            {
                return;
            }

            if (_erasing)
                cell.SetContent(-1, null, Color.white);
            else
                cell.SetContent(_selectedType, ResolveIcon(_selectedType), Color.white);

            UpdateHint();
        }

        private Sprite ResolveIcon(int rosterIndex)
        {
            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;
            UnitConfig unit = roster != null ? roster.Get(rosterIndex) : null;

            return unit != null && unit.icon != null ? unit.icon : fallbackIcon;
        }

        private void ClearAll()
        {
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].SetContent(-1, null, Color.white);

            UpdateHint();
        }

        private void Apply()
        {
            Save();
            Push(PlayerState.Local);
        }

        private void Push(PlayerState player)
        {
            if (player == null || _cells.Count == 0)
                return;

            player.CmdSetArmyPreset(BuildPreset().Pack());
        }

        private ArmyPreset BuildPreset()
        {
            ArmyPreset preset = new();

            for (int i = 0; i < _cells.Count; i++)
            {
                ArmyPresetCellView cell = _cells[i];
                if (cell.RosterIndex < 0)
                    continue;

                preset.Add(new ArmyPresetSlot(
                    (byte)Mathf.Clamp(cell.RosterIndex, 0, byte.MaxValue),
                    (sbyte)Mathf.Clamp(cell.Column, sbyte.MinValue, sbyte.MaxValue),
                    (sbyte)Mathf.Clamp(cell.Row, sbyte.MinValue, sbyte.MaxValue)));
            }

            return preset;
        }

        private void UpdateHint()
        {
            if (hintLabel == null)
                return;

            int placed = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].RosterIndex >= 0)
                    placed++;
            }

            if (placed == 0)
            {
                hintLabel.text = "Выберите тип слева и проведите мышью по полю";
                return;
            }

            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;
            UnitConfig selected = roster != null ? roster.Get(_selectedType) : null;

            hintLabel.text = selected != null
                ? $"Расставлено мест: {placed}   ·   {UiText.UnitName(selected, _selectedType)} — радиус атаки {selected.attackRange:0.#} м"
                : $"Расставлено мест: {placed}";
        }

        #region Хранение между матчами

        private void Save()
        {
            byte[] packed = BuildPreset().Pack();
            PlayerPrefs.SetString(StorageKey, System.Convert.ToBase64String(packed));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string stored = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (string.IsNullOrEmpty(stored) || _cells.Count == 0)
                return;

            byte[] packed;

            try
            {
                packed = System.Convert.FromBase64String(stored);
            }
            catch (System.FormatException)
            {
                // Испорченная запись в настройках не должна мешать игроку зайти в бой.
                PlayerPrefs.DeleteKey(StorageKey);
                return;
            }

            UnitRosterConfig roster = Match.Config != null ? Match.Config.Roster : null;

            ArmyPreset preset = new();
            if (!preset.Unpack(packed, roster != null ? roster.Count : 0))
                return;

            ApplyToGrid(preset);
            UpdateHint();
        }

        private void ApplyToGrid(ArmyPreset preset)
        {
            ClearAll();

            IReadOnlyList<ArmyPresetSlot> slots = preset.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                ArmyPresetSlot slot = slots[i];
                ArmyPresetCellView cell = FindCell(slot.Column, slot.Row);

                if (cell != null)
                    cell.SetContent(slot.RosterIndex, ResolveIcon(slot.RosterIndex), Color.white);
            }
        }

        private ArmyPresetCellView FindCell(int column, int row)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].Column == column && _cells[i].Row == row)
                    return _cells[i];
            }

            return null;
        }

        #endregion
    }
}
