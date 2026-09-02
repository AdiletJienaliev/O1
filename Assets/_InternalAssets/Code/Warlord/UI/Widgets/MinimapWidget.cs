using System.Collections.Generic;
using UnityEngine;
using Warlord.Configs;
using Warlord.Core;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Миникарта арены. Проекция плоская: карта квадратная (<see cref="MapConfig.size"/>),
    /// поэтому мировые X и Z линейно ложатся на прямоугольник виджета без камеры и рендер-текстур.
    /// Без заполненного MapConfig миникарта прячется — рисовать наугад хуже, чем не рисовать.
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

        [Header("Иконки")]
        [SerializeField] private Sprite heroIcon;
        [SerializeField] private Sprite flagIcon;
        [SerializeField] private Sprite baseIcon;

        [Tooltip("Как часто пересобирать список объектов на карте, с.")]
        [SerializeField] private float rescanInterval = 2f;

        private readonly List<MinimapMarkerView> _markers = new(16);
        private readonly List<HeroController> _heroes = new(4);
        private readonly List<CapturePointBehaviour> _points = new(8);

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

            MapConfig map = Match.Config != null ? Match.Config.Map : null;
            bool usable = map != null && map.size > 1f;

            if (root != null && root.activeSelf != usable)
                root.SetActive(usable);

            if (!usable)
                return;

            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
                Rescan();

            _used = 0;
            int localSlot = player != null ? player.Slot : PlayerSlots.None;

            DrawPoints(map);
            DrawHeroes(map, localSlot);

            for (int i = _used; i < _markers.Count; i++)
                _markers[i].Show(false);
        }

        private void DrawPoints(MapConfig map)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                CapturePointBehaviour point = _points[i];
                if (point == null)
                    continue;

                bool central = point.Kind == CapturePointKind.CentralFlag;

                Push(
                    Project(point.transform.position, map),
                    SlotColor(point.OwnerSlot),
                    central ? flagIcon : baseIcon,
                    flagScale,
                    central);
            }
        }

        private void DrawHeroes(MapConfig map, int localSlot)
        {
            for (int i = 0; i < _heroes.Count; i++)
            {
                HeroController hero = _heroes[i];
                if (hero == null || !hero.IsAlive)
                    continue;

                Push(Project(hero.Position, map), SlotColor(hero.Slot), heroIcon, heroScale, hero.Slot == localSlot);
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

        private Vector2 Project(Vector3 world, MapConfig map)
        {
            Vector2 size = field.rect.size;
            float half = map.size * 0.5f;

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

            // Пул на всех полководцев плюс центральный флаг и флаги баз.
            int capacity = PlayerSlots.MaxSupported * 2 + 2;

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
        }
    }
}
