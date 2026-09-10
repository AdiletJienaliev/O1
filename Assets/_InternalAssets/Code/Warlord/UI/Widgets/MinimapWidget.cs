using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;
using Warlord.Gameplay.Units;
using Warlord.Gameplay.World;
using Warlord.Presentation;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Миникарта арены. Проекция плоская: арена квадратная (<see cref="MatchArena.Size"/>),
    /// поэтому мировые X и Z линейно ложатся на прямоугольник виджета без камеры и рендер-текстур.
    /// Без арены в сцене миникарта прячется — рисовать наугад хуже, чем не рисовать.
    /// </summary>
    public sealed class MinimapWidget : HudWidget
    {
        [Header("Поле")]
        [SerializeField] private RectTransform field;
        [SerializeField] private MinimapMarkerView markerTemplate;
        [SerializeField] private GameObject root;

        [Header("Размеры меток")]
        [SerializeField] private float heroScale = 1f;
        [SerializeField] private float flagScale = 0.85f;

        [Tooltip("Метка юнита мельче полководца: их до двадцати, и в общем размере они "
                 + "залепили бы карту.")]
        [SerializeField] private float unitScale = 0.42f;

        [Tooltip("Сколько своих юнитов показывать. Больше двадцати на карте такого размера "
                 + "уже неразличимо, а пул меток не бесконечен.")]
        [SerializeField] private int maxUnitMarkers = 24;

        [Header("Иконки")]
        [SerializeField] private Sprite heroIcon;

        [Tooltip("Иконка своего полководца: стрелка, развёрнутая по направлению обзора.")]
        [SerializeField] private Sprite localHeroIcon;

        [SerializeField] private Sprite flagIcon;
        [SerializeField] private Sprite baseIcon;

        [Tooltip("Как часто пересобирать список объектов на карте, с.")]
        [SerializeField] private float rescanInterval = 2f;

        private readonly List<MinimapMarkerView> _markers = new(48);
        private readonly List<HeroController> _heroes = new(4);
        private readonly List<CapturePointBehaviour> _points = new(8);
        private readonly List<UnitEntity> _units = new(64);

        private float _rescanTimer;
        private int _used;

        protected override void OnInitialized()
        {
            BuildPool();
            Rescan();
        }

        public override void Refresh(PlayerState player)
        {
            if (!HasMatch || field == null)
                return;

            float arenaSize = MatchArena.Size;
            bool usable = arenaSize > 1f;

            if (root != null && root.activeSelf != usable)
                root.SetActive(usable);

            if (!usable)
                return;

            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
                Rescan();

            _used = 0;
            int localSlot = player != null ? player.Slot : PlayerSlots.None;

            // Порядок важен: юниты рисуются первыми и оказываются под флагами и
            // полководцами, иначе своя же армия закрывает точку, к которой идёт.
            DrawUnits(arenaSize, localSlot);
            DrawPoints(arenaSize);
            DrawHeroes(arenaSize, localSlot);

            for (int i = _used; i < _markers.Count; i++)
                _markers[i].Show(false);
        }

        private void DrawPoints(float arenaSize)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                CapturePointBehaviour point = _points[i];
                if (point == null)
                    continue;

                bool central = point.Kind == CapturePointKind.CentralFlag;

                Push(
                    Project(point.transform.position, arenaSize),
                    SlotColor(point.OwnerSlot),
                    central ? flagIcon : baseIcon,
                    flagScale,
                    central);
            }
        }

        /// <summary>
        /// Своя армия и армия союзников. Чужие юниты не показываем намеренно: миникарта
        /// без разведки, и полный состав противника на ней сделал бы обходы бессмысленными.
        /// Союзник — исключение: без его армии на карте командная игра слепая.
        /// </summary>
        private void DrawUnits(float arenaSize, int localSlot)
        {
            if (!PlayerSlots.IsValid(localSlot))
                return;

            TeamLayout teams = Teams();
            int drawn = 0;

            for (int i = 0; i < _units.Count && drawn < maxUnitMarkers; i++)
            {
                UnitEntity unit = _units[i];

                if (unit == null || !unit.IsAlive || !teams.SameSide(unit.OwnerSlot, localSlot))
                    continue;

                Push(Project(unit.Position, arenaSize), SlotColor(unit.OwnerSlot), null, unitScale, false);
                drawn++;
            }
        }

        /// <summary>Раскладка команд матча. Приезжает клиенту в настройках комнаты.</summary>
        private static TeamLayout Teams()
        {
            MatchManager match = MatchManager.Instance;
            return match != null ? match.Settings.Teams : TeamLayout.Ffa;
        }

        private void DrawHeroes(float arenaSize, int localSlot)
        {
            for (int i = 0; i < _heroes.Count; i++)
            {
                HeroController hero = _heroes[i];
                if (hero == null || !hero.IsAlive)
                    continue;

                bool local = hero.Slot == localSlot;
                Sprite icon = local && localHeroIcon != null ? localHeroIcon : heroIcon;

                Push(Project(hero.Position, arenaSize), SlotColor(hero.Slot), icon, heroScale, local);

                // Стрелка своего полководца смотрит туда же, куда камера: без этого
                // на миникарте не понять, в какую сторону ты развёрнут.
                if (!local || localHeroIcon == null || _used == 0)
                    continue;

                HeroOrbitCamera camera = HeroOrbitCamera.Current;

                if (camera != null)
                    _markers[_used - 1].SetRotation(camera.Yaw);
            }
        }

        private void Push(Vector2 position, Color color, Sprite sprite, float scale, bool highlighted)
        {
            if (_used >= _markers.Count)
                return;

            MinimapMarkerView marker = _markers[_used++];
            marker.Show(true);
            marker.Apply(position, color, sprite, scale, highlighted);
        }

        private Vector2 Project(Vector3 world, float arenaSize)
        {
            Vector2 size = field.rect.size;
            float half = arenaSize * 0.5f;

            float x = Mathf.Clamp(world.x / half, -1f, 1f) * size.x * 0.5f;
            float y = Mathf.Clamp(world.z / half, -1f, 1f) * size.y * 0.5f;

            return new Vector2(x, y);
        }

        private Color SlotColor(int slot)
        {
            TeamColorConfig colors = Match.Config != null ? Match.Config.TeamColors : null;
            return TeamPalette.Primary(colors, slot);
        }

        private void BuildPool()
        {
            if (_markers.Count > 0 || field == null || markerTemplate == null)
                return;

            markerTemplate.gameObject.SetActive(false);

            // Пул на всех полководцев, флаги и свою армию.
            int capacity = PlayerSlots.MaxSupported * 2 + 2 + Mathf.Max(0, maxUnitMarkers);

            for (int i = 0; i < capacity; i++)
            {
                MinimapMarkerView marker = Instantiate(markerTemplate, field);
                marker.name = "Marker_" + i;
                marker.Show(false);
                _markers.Add(marker);
            }
        }

        private void Rescan()
        {
            _rescanTimer = Mathf.Max(0.25f, rescanInterval);

            _heroes.Clear();
            _heroes.AddRange(Object.FindObjectsByType<HeroController>(FindObjectsInactive.Exclude));

            _points.Clear();
            _points.AddRange(Object.FindObjectsByType<CapturePointBehaviour>(FindObjectsInactive.Exclude));

            _units.Clear();
            _units.AddRange(Object.FindObjectsByType<UnitEntity>(FindObjectsInactive.Exclude));
        }
    }
}
