using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.Domain.Upgrades;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Players;

namespace Warlord.UI.Widgets
{
    /// <summary>
    /// Панель гарнизона (ГДД §1.9, §2.5, §2.6): список своих точек со счётчиком «занято / лимит»,
    /// покупка охранника на выбранную точку и выбор улучшения аванпоста.
    ///
    /// Отдельно от панели покупки юнитов, потому что охранник покупается не сам по себе,
    /// а вместе с точкой: карточка без выбора точки была бы кнопкой, которую сервер
    /// отклоняет всегда.
    ///
    /// Счётчик занятых слотов виден клиенту через SyncVar самой точки, а не считается здесь:
    /// чужие гарнизоны клиенту не видны поимённо, и сервер присылает только число.
    /// </summary>
    public sealed class GarrisonShopWidget : HudWidget
    {
        [Header("Строки")]
        [SerializeField] private RectTransform container;
        [SerializeField] private GarrisonPointRowView rowTemplate;

        [Header("Сводка")]
        [SerializeField] private TextMeshProUGUI summaryLabel;
        [SerializeField] private GameObject emptyHint;

        private readonly List<GarrisonPointRowView> _rows = new(8);
        private readonly List<CapturePointBehaviour> _points = new(8);

        private int _guardRosterIndex = -1;

        protected override void OnInitialized()
        {
            _guardRosterIndex = FindGuardIndex();
            _rows.Clear();

            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);
        }

        public override void Refresh(PlayerState player)
        {
            if (!HasMatch || container == null || rowTemplate == null)
                return;

            // HUD обновляет все виджеты каждый кадр, а эта панель почти всегда закрыта.
            // Без выхода отсюда полный обход сцены в поисках точек шёл бы и в бою.
            if (!isActiveAndEnabled)
                return;

            RebuildRows(player);

            bool running = Match.Phase == MatchPhase.Running && player != null && !player.IsEliminated;

            if (emptyHint != null)
                emptyHint.SetActive(_rows.Count == 0);

            if (summaryLabel != null && player != null)
            {
                // Раздельные числа армии и гарнизона — суть механики лимита: игрок должен
                // видеть, сколько его силы стоит на точках, не пересчитывая в уме (ГДД §1.9).
                summaryLabel.text = "Армия " + player.ArmyCount + " / " + player.UnitCap
                                    + " · Гарнизон " + player.GarrisonCount;
            }

            if (player == null)
                return;

            GameConfig config = Match.Config;
            UnitConfig guard = config != null && config.Roster != null ? config.Roster.Get(_guardRosterIndex) : null;

            if (guard == null)
                return;

            bool inZone = MatchQuery.IsHeroInsideBuyZone(config, HeroController.Local, player.Slot);
            bool hasRoom = player.ArmyCount + player.GarrisonCount + player.SpawnQueue.Count < player.UnitCap;

            for (int i = 0; i < _rows.Count; i++)
                RefreshRow(_rows[i], player, guard, config, running && inZone, hasRoom);
        }

        private void RefreshRow(
            GarrisonPointRowView row,
            PlayerState player,
            UnitConfig guard,
            GameConfig config,
            bool buyable,
            bool hasRoom)
        {
            CapturePointBehaviour point = row.Point;

            if (point == null)
                return;

            int limit = Mathf.Max(1, Mathf.Min(point.MaxGuards, guard.maxPerPoint));
            int guards = point.GuardCount;

            int baseCost = Match.Settings.ResolveUnitCost(guard, config.GameMode);
            int cost = OutpostUpgradeStack.ResolveGuardCost(baseCost, point.Upgrade);

            bool affordable = player.Gold >= cost;
            bool canBuy = buyable && hasRoom && affordable && guards < limit;

            row.Refresh(guards, limit, cost, canBuy, affordable, point.UpgradeIndex);
        }

        /// <summary>
        /// Пересобирает список строк, когда меняется набор своих точек. Полный обход сцены
        /// делается только на изменении: точек на карте пять, но искать их каждый кадр
        /// ради панели, которая почти всегда закрыта, незачем.
        /// </summary>
        private void RebuildRows(PlayerState player)
        {
            CollectOwnPoints(player);

            if (SameAsRows())
                return;

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    Destroy(_rows[i].gameObject);
            }

            _rows.Clear();

            OutpostUpgradeSetConfig upgrades = Match.Config != null ? Match.Config.OutpostUpgrades : null;

            for (int i = 0; i < _points.Count; i++)
            {
                GarrisonPointRowView row = Instantiate(rowTemplate, container);
                row.gameObject.SetActive(true);
                row.name = "PointRow_" + i;
                row.Bind(_points[i], upgrades, Purchase, SelectUpgrade);
                _rows.Add(row);
            }
        }

        /// <summary>Свои точки, на которых вообще возможен гарнизон.</summary>
        private void CollectOwnPoints(PlayerState player)
        {
            _points.Clear();

            if (player == null)
                return;

            CapturePointBehaviour[] all = FindObjectsByType<CapturePointBehaviour>(FindObjectsInactive.Exclude);

            for (int i = 0; i < all.Length; i++)
            {
                CapturePointBehaviour point = all[i];

                if (point.OwnerSlot == player.Slot && point.MaxGuards > 0)
                    _points.Add(point);
            }
        }

        private bool SameAsRows()
        {
            if (_rows.Count != _points.Count)
                return false;

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null || _rows[i].Point != _points[i])
                    return false;
            }

            return true;
        }

        /// <summary>Первый гарнизонный тип в ростере. Пока он один, выбирать не из чего.</summary>
        private int FindGuardIndex()
        {
            UnitRosterConfig roster = Match != null && Match.Config != null ? Match.Config.Roster : null;

            if (roster == null)
                return -1;

            for (int i = 0; i < roster.Count; i++)
            {
                UnitConfig unit = roster.Get(i);

                if (unit != null && unit.isGarrison)
                    return i;
            }

            return -1;
        }

        private void Purchase(CapturePointBehaviour point)
        {
            if (point == null || _guardRosterIndex < 0)
                return;

            PlayerState.Local?.CmdPurchaseGuard((byte)_guardRosterIndex, point);
        }

        private static void SelectUpgrade(CapturePointBehaviour point, int upgradeIndex)
        {
            if (point != null)
                PlayerState.Local?.CmdSelectOutpostUpgrade(point, (byte)Mathf.Clamp(upgradeIndex, 0, byte.MaxValue));
        }
    }
}
