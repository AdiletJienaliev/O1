using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.UI;
using Warlord.UI.Screens;
using Warlord.UI.Widgets;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Боевой HUD. Экран разложен по углам: ресурсы и гонка за флаг слева сверху,
    /// часы по центру, лента и миникарта справа, полководец и покупка снизу.
    /// Центр экрана остаётся пустым — там идёт бой.
    /// </summary>
    public static partial class WarlordUiBuilder
    {
        private static HudScreen BuildHudScreen(RectTransform parent, Templates templates)
        {
            RectTransform screen = ScreenRoot("Screen_Hud", parent, out CanvasGroup group);
            List<HudWidget> widgets = new(12);

            widgets.Add(BuildResourceBar(screen));
            widgets.Add(BuildFlagRace(screen, templates));
            widgets.Add(BuildClock(screen));
            widgets.Add(BuildEventFeed(screen));
            widgets.Add(BuildMinimap(screen, templates));
            widgets.Add(BuildHeroCard(screen));
            widgets.Add(BuildSpawnQueue(screen, templates));
            widgets.Add(BuildUnitShop(screen, templates));
            widgets.Add(BuildOrderBar(screen, templates, out Button upgradeToggle));
            widgets.Add(BuildToast(screen));
            widgets.Add(BuildCrosshair(screen));

            UpgradeWidget upgrades = BuildUpgradePanel(screen, templates, out GameObject upgradePanel, out Button upgradeClose);
            widgets.Add(upgrades);

            HudScreen hud = screen.gameObject.AddComponent<HudScreen>();

            using (Bind bind = new(hud))
            {
                bind.Ref("group", group)
                    .Refs("widgets", widgets.ToArray())
                    .Ref("upgradePanel", upgradePanel)
                    .Ref("upgradeToggleButton", upgradeToggle)
                    .Ref("upgradeCloseButton", upgradeClose);
            }

            return hud;
        }

        #region Ресурсы

        private static ResourceWidget BuildResourceBar(RectTransform screen)
        {
            RectTransform bar = Ui.Node("ResourceBar", screen).At(Ui.TopLeft, new Vector2(26f, -22f), new Vector2(680f, 58f));
            bar.Row(12f);

            Chip("Gold", bar, Kit.ItemGold, out TextMeshProUGUI gold, out TextMeshProUGUI income);
            Chip("Xp", bar, Kit.ItemXp, out TextMeshProUGUI xp, out TextMeshProUGUI xpCaption);
            Chip("Army", bar, Kit.ItemHelmet, out TextMeshProUGUI army, out TextMeshProUGUI armyCaption);

            xpCaption.text = "опыт";
            armyCaption.text = "армия";
            income.text = "+0/с";
            income.color = Ui.Good;

            ResourceWidget widget = bar.gameObject.AddComponent<ResourceWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("goldLabel", gold)
                    .Ref("incomeLabel", income)
                    .Ref("xpLabel", xp)
                    .Ref("armyLabel", army);
            }

            return widget;
        }

        #endregion

        #region Гонка за флаг

        private static FlagRaceWidget BuildFlagRace(RectTransform screen, Templates templates)
        {
            RectTransform panel = Ui.Node("FlagRace", screen).At(Ui.TopLeft, new Vector2(26f, -96f), new Vector2(470f, 254f));
            panel.Sprite(Kit.PanelLarge, Color.white);

            Image flagIcon = Ui.Icon("Icon", panel, Kit.ItemFlag, new Vector2(26f, 26f));
            flagIcon.rectTransform.At(Ui.TopLeft, new Vector2(20f, -18f), new Vector2(26f, 26f));

            Ui.Label("Title", panel, "УДЕРЖАНИЕ ФЛАГА", 20f, Ui.InkMuted, TextAlignmentOptions.Left, Kit.FontTitle)
                .rectTransform.At(Ui.TopLeft, new Vector2(52f, -18f), new Vector2(300f, 26f));

            RectTransform rows = Ui.Node("Rows", panel).At(Ui.Top, new Vector2(0f, -52f), new Vector2(440f, 190f)).Pivot(Ui.Top);
            rows.Column(6f, TextAnchor.UpperCenter);

            FlagRaceRowView rowTemplate = Spawn<FlagRaceRowView>(templates.FlagHoldRow, rows, "RowTemplate");
            rowTemplate.gameObject.SetActive(false);

            // Баннер владельца флага живёт по центру верха — это общий для всех статус.
            RectTransform banner = Ui.Node("FlagBanner", screen).At(Ui.Top, new Vector2(0f, -132f), new Vector2(430f, 58f));
            banner.Sprite(Kit.Banner, Color.white);

            Image ownerTab = Ui.Icon("OwnerTab", banner, Kit.PillSmall, new Vector2(10f, 30f));
            ownerTab.rectTransform.At(Ui.Left, new Vector2(22f, 0f), new Vector2(10f, 30f));

            TextMeshProUGUI ownerLabel = Ui.Label("Owner", banner, "ФЛАГ СВОБОДЕН", 22f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontTitle);
            ownerLabel.rectTransform.At(Ui.Center, new Vector2(6f, 6f), new Vector2(360f, 26f));

            RectTransform captureBar = Ui.Node("CaptureBar", banner).At(Ui.Bottom, new Vector2(0f, 10f), new Vector2(330f, 10f));
            captureBar.Sprite(Kit.ThinBarFrame, new Color(1f, 1f, 1f, 0.45f));

            RectTransform captureFillRect = Ui.Node("Fill", captureBar).Stretch(2f, 2f, 2f, 2f);
            Image captureFill = captureFillRect.Sprite(Kit.ThinBarFill, Ui.Gold);
            captureFill.type = Image.Type.Filled;
            captureFill.fillMethod = Image.FillMethod.Horizontal;
            captureFill.fillAmount = 0f;

            FlagRaceWidget widget = panel.gameObject.AddComponent<FlagRaceWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("container", rows)
                    .Ref("rowTemplate", rowTemplate)
                    .Ref("flagOwnerLabel", ownerLabel)
                    .Ref("flagOwnerTab", ownerTab)
                    .Ref("captureFill", captureFill)
                    .Ref("captureRoot", banner.gameObject);
            }

            return widget;
        }

        #endregion

        #region Часы и обратный отсчёт

        private static MatchTimerWidget BuildClock(RectTransform screen)
        {
            RectTransform clock = Ui.Node("Clock", screen).At(Ui.Top, new Vector2(0f, -18f), new Vector2(270f, 88f));
            clock.Sprite(Kit.Pill, Color.white);

            Image icon = Ui.Icon("Icon", clock, Kit.IconTimer, new Vector2(40f, 40f));
            icon.rectTransform.At(Ui.Left, new Vector2(22f, 0f), new Vector2(40f, 40f));

            TextMeshProUGUI time = Ui.Label("Time", clock, "15:00", 46f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            time.rectTransform.At(Ui.Center, new Vector2(16f, 0f), new Vector2(180f, 52f));

            TextMeshProUGUI phase = Ui.Label("Phase", screen, "Бой", 20f, Ui.InkMuted, TextAlignmentOptions.Center);
            phase.rectTransform.At(Ui.Top, new Vector2(0f, -108f), new Vector2(300f, 26f));

            // Обратный отсчёт перекрывает центр экрана и живёт только в фазе подготовки.
            RectTransform countdown = Ui.Node("Countdown", screen).At(Ui.Center, Vector2.zero, new Vector2(600f, 300f));

            TextMeshProUGUI countdownLabel = Ui.Label("Value", countdown, "3", 180f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontTitle);
            countdownLabel.rectTransform.Stretch();
            countdownLabel.Shadow(new Color(0f, 0f, 0f, 0.7f), new Vector2(4f, -6f));

            countdown.gameObject.SetActive(false);

            MatchTimerWidget widget = clock.gameObject.AddComponent<MatchTimerWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("clockLabel", time)
                    .Ref("phaseLabel", phase)
                    .Ref("countdownRoot", countdown.gameObject)
                    .Ref("countdownLabel", countdownLabel);
            }

            return widget;
        }

        #endregion

        #region Лента событий и миникарта

        private static EventFeedWidget BuildEventFeed(RectTransform screen)
        {
            RectTransform feed = Ui.Node("EventFeed", screen).At(Ui.TopRight, new Vector2(-26f, -22f), new Vector2(520f, 150f)).Pivot(Ui.TopRight);
            feed.Column(6f, TextAnchor.UpperRight);

            TextMeshProUGUI line = Ui.Label("LineTemplate", feed, "", 21f, Ui.Ink, TextAlignmentOptions.Right);
            line.rectTransform.sizeDelta = new Vector2(500f, 28f);
            line.Shadow(new Color(0f, 0f, 0f, 0.8f), new Vector2(1f, -2f));
            line.gameObject.SetActive(false);

            EventFeedWidget widget = feed.gameObject.AddComponent<EventFeedWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("container", feed).Ref("lineTemplate", line);
            }

            return widget;
        }

        private static MinimapWidget BuildMinimap(RectTransform screen, Templates templates)
        {
            RectTransform root = Ui.Node("Minimap", screen).At(Ui.TopRight, new Vector2(-26f, -190f), new Vector2(240f, 240f));
            root.Sprite(Kit.PanelSmall, Color.white);

            RectTransform field = Ui.Node("Field", root).Stretch(14f, 14f, 14f, 14f);
            field.Sprite(Kit.BackgroundPattern, new Color(1f, 1f, 1f, 0.12f), Image.Type.Simple);

            MinimapMarkerView markerTemplate = Spawn<MinimapMarkerView>(templates.MinimapMarker, field, "MarkerTemplate");
            markerTemplate.gameObject.SetActive(false);

            MinimapWidget widget = root.gameObject.AddComponent<MinimapWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("field", field)
                    .Ref("markerTemplate", markerTemplate)
                    .Ref("root", root.gameObject)
                    .Ref("heroIcon", null)
                    .Ref("flagIcon", Kit.ItemFlag)
                    .Ref("baseIcon", Kit.ItemCastle);
            }

            return widget;
        }

        #endregion

        #region Полководец

        private static HeroVitalsWidget BuildHeroCard(RectTransform screen)
        {
            RectTransform card = Ui.Node("HeroCard", screen).At(Ui.BottomLeft, new Vector2(26f, 26f), new Vector2(440f, 132f));
            card.Sprite(Kit.PanelLarge, Color.white);

            RectTransform portrait = Ui.Node("Portrait", card).At(Ui.Left, new Vector2(18f, 0f), new Vector2(96f, 96f));
            Image portraitFrame = portrait.Sprite(Kit.CircleLarge, Color.white);

            Ui.Icon("Face", portrait, Kit.ItemCrown, new Vector2(48f, 48f))
                .rectTransform.At(Ui.Center, Vector2.zero, new Vector2(48f, 48f));

            TextMeshProUGUI name = Ui.Label("Name", card, "Игрок 1", 24f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.TopLeft, new Vector2(128f, -22f), new Vector2(200f, 30f));

            RectTransform barRoot = Ui.Node("HealthBar", card).At(Ui.Left, new Vector2(128f, -6f), new Vector2(280f, 26f));
            Slider health = SliderInPlace(barRoot, out Image healthFill);

            TextMeshProUGUI healthLabel = Ui.Label("HealthValue", card, "250 / 250", 20f, Ui.Ink, TextAlignmentOptions.Right, Kit.FontNumbers);
            healthLabel.rectTransform.At(Ui.BottomRight, new Vector2(-20f, 18f), new Vector2(160f, 24f));

            RectTransform buyZone = Ui.Node("BuyZoneBadge", card).At(Ui.TopRight, new Vector2(-18f, -18f), new Vector2(140f, 34f));
            buyZone.Sprite(Kit.TagGreen, Color.white);
            Ui.Label("Text", buyZone, "НА БАЗЕ", 18f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();
            buyZone.gameObject.SetActive(false);

            RectTransform death = Ui.Node("DeathOverlay", card).Stretch(6f, 6f, 6f, 6f);
            death.Sprite(Kit.PanelGray, new Color(0.5f, 0.12f, 0.12f, 0.92f));

            TextMeshProUGUI deathLabel = Ui.Label("Text", death, "Полководец пал", 26f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle);
            deathLabel.rectTransform.Stretch();
            death.gameObject.SetActive(false);

            HeroVitalsWidget widget = card.gameObject.AddComponent<HeroVitalsWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("healthBar", health)
                    .Ref("healthFill", healthFill)
                    .Ref("healthLabel", healthLabel)
                    .Ref("deathRoot", death.gameObject)
                    .Ref("deathLabel", deathLabel)
                    .Ref("buyZoneBadge", buyZone.gameObject)
                    .Ref("portraitFrame", portraitFrame)
                    .Ref("nameLabel", name);
            }

            return widget;
        }

        /// <summary>Собирает Slider прямо на переданном прямоугольнике — без лишнего вложенного узла.</summary>
        private static Slider SliderInPlace(RectTransform rect, out Image fill)
        {
            rect.Sprite(Kit.BarFrame, Color.white);

            RectTransform fillArea = Ui.Node("Fill_Area", rect).Stretch(4f, 4f, 4f, 4f);
            RectTransform fillRect = Ui.Node("Fill", fillArea).Stretch();
            fill = fillRect.Sprite(Kit.BarFillGreen, Color.white);

            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = fill;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        #endregion

        #region Очередь и покупка

        private static SpawnQueueWidget BuildSpawnQueue(RectTransform screen, Templates templates)
        {
            RectTransform queue = Ui.Node("SpawnQueue", screen).At(Ui.Bottom, new Vector2(0f, 276f), new Vector2(560f, 84f));

            RectTransform slots = Ui.Node("Slots", queue).At(Ui.Center, Vector2.zero, new Vector2(540f, 78f));
            slots.Row(8f, TextAnchor.MiddleCenter);

            SpawnQueueSlotView slotTemplate = Spawn<SpawnQueueSlotView>(templates.SpawnQueueSlot, slots, "SlotTemplate");
            slotTemplate.gameObject.SetActive(false);

            TextMeshProUGUI counter = Ui.Label("Counter", queue, "", 20f, Ui.InkMuted, TextAlignmentOptions.Left, Kit.FontNumbers);
            counter.rectTransform.At(Ui.Right, new Vector2(-4f, 0f), new Vector2(60f, 24f));

            TextMeshProUGUI empty = Ui.Label("EmptyHint", queue, "очередь пуста", 18f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Center);
            empty.rectTransform.At(Ui.Center, Vector2.zero, new Vector2(300f, 24f));

            SpawnQueueWidget widget = queue.gameObject.AddComponent<SpawnQueueWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("container", slots)
                    .Ref("slotTemplate", slotTemplate)
                    .Ref("emptyHint", empty.gameObject)
                    .Ref("counterLabel", counter)
                    .Refs("fallbackIcons", Kit.UnitIcons);
            }

            return widget;
        }

        private static UnitShopWidget BuildUnitShop(RectTransform screen, Templates templates)
        {
            RectTransform shop = Ui.Node("UnitShop", screen).At(Ui.Bottom, new Vector2(0f, 26f), new Vector2(880f, 240f));

            RectTransform cards = Ui.Node("Cards", shop).At(Ui.Center, Vector2.zero, new Vector2(860f, 230f));
            cards.Row(14f, TextAnchor.MiddleCenter);

            UnitCardView cardTemplate = Spawn<UnitCardView>(templates.UnitCard, cards, "CardTemplate");
            cardTemplate.gameObject.SetActive(false);

            RectTransform hint = Ui.Node("BuyZoneHint", shop).At(Ui.Top, new Vector2(0f, 130f), new Vector2(520f, 44f));
            hint.Sprite(Kit.PillSmall, new Color(1f, 1f, 1f, 0.9f));

            TextMeshProUGUI hintLabel = Ui.Label("Text", hint, "Покупка доступна только на своей базе", 20f, Ui.Danger, TextAlignmentOptions.Center);
            hintLabel.rectTransform.Stretch();
            hint.gameObject.SetActive(false);

            UnitShopWidget widget = shop.gameObject.AddComponent<UnitShopWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("container", cards)
                    .Ref("cardTemplate", cardTemplate)
                    .Ref("buyZoneHint", hint.gameObject)
                    .Ref("buyZoneHintLabel", hintLabel)
                    .Refs("fallbackIcons", Kit.UnitIcons);
            }

            return widget;
        }

        #endregion

        #region Приказы и построения

        private static OrderBarWidget BuildOrderBar(RectTransform screen, Templates templates, out Button upgradeToggle)
        {
            RectTransform bar = Ui.Node("OrderBar", screen).At(Ui.BottomRight, new Vector2(-26f, 26f), new Vector2(380f, 260f)).Pivot(Ui.BottomRight);

            RectTransform orders = Ui.Node("Orders", bar).At(Ui.Bottom, new Vector2(0f, 0f), new Vector2(370f, 112f));
            orders.Row(14f, TextAnchor.MiddleCenter);

            HotkeyButtonView[] orderButtons = new HotkeyButtonView[3];
            for (int i = 0; i < orderButtons.Length; i++)
                orderButtons[i] = Spawn<HotkeyButtonView>(templates.OrderButton, orders, "Order_" + i);

            RectTransform formations = Ui.Node("Formations", bar).At(Ui.Bottom, new Vector2(0f, 124f), new Vector2(370f, 92f));
            formations.Row(12f, TextAnchor.MiddleCenter);

            HotkeyButtonView formationTemplate = Spawn<HotkeyButtonView>(templates.OrderButton, formations, "FormationTemplate");
            formationTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(92f, 92f);
            formationTemplate.gameObject.SetActive(false);

            upgradeToggle = MenuButton("UpgradeToggle", bar, "ПРОКАЧКА  ⇥", Kit.ButtonNavy);
            upgradeToggle.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(0f, 228f), new Vector2(280f, 62f));

            OrderBarWidget widget = bar.gameObject.AddComponent<OrderBarWidget>();

            using (Bind bind = new(widget))
            {
                bind.Refs("orderButtons", orderButtons)
                    .Refs("orderIcons", Kit.OrderIcons)
                    .Ref("formationContainer", formations)
                    .Ref("formationTemplate", formationTemplate);
            }

            return widget;
        }

        #endregion

        #region Прокачка и уведомления

        private static UpgradeWidget BuildUpgradePanel(RectTransform screen, Templates templates, out GameObject panelObject, out Button close)
        {
            RectTransform overlay = Ui.Node("UpgradeOverlay", screen).Stretch();
            overlay.Sprite(null, new Color(0.04f, 0.05f, 0.08f, 0.7f)).raycastTarget = true;

            RectTransform panel = Ui.Node("Panel", overlay).At(Ui.Center, new Vector2(0f, 0f), new Vector2(700f, 800f));
            panel.Sprite(Kit.PopupBody, Color.white);

            RectTransform header = Ui.Node("Header", panel).At(Ui.Top, new Vector2(0f, 20f), new Vector2(440f, 84f));
            header.Sprite(Kit.PopupHeader, Color.white);
            Ui.Label("Text", header, "ПРОКАЧКА", 30f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            RectTransform xpChip = Chip("XpChip", panel, Kit.ItemXp, out TextMeshProUGUI xp, out TextMeshProUGUI xpCaption);
            xpChip.At(Ui.Top, new Vector2(0f, -92f), new Vector2(210f, 58f));
            xpCaption.text = "доступно опыта";

            RectTransform branches = Ui.Node("Branches", panel).At(Ui.Top, new Vector2(0f, -166f), new Vector2(620f, 620f)).Pivot(Ui.Top);
            branches.Column(8f, TextAnchor.UpperCenter);

            UpgradeBranchView branchTemplate = Spawn<UpgradeBranchView>(templates.UpgradeBranch, branches, "BranchTemplate");
            branchTemplate.gameObject.SetActive(false);

            close = Ui.Button("Close", panel, Kit.ButtonCircle, Color.white, out _);
            close.GetComponent<RectTransform>().At(Ui.TopRight, new Vector2(-14f, -14f), new Vector2(56f, 56f));
            Ui.Icon("Icon", close.transform, Kit.IconClose, new Vector2(24f, 24f))
                .rectTransform.At(Ui.Center, Vector2.zero, new Vector2(24f, 24f));

            overlay.gameObject.SetActive(false);
            panelObject = overlay.gameObject;

            UpgradeWidget widget = panel.gameObject.AddComponent<UpgradeWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("container", branches)
                    .Ref("branchTemplate", branchTemplate)
                    .Ref("xpLabel", xp)
                    .Refs("branchIcons", Kit.BranchIcons);
            }

            return widget;
        }

        /// <summary>
        /// Прицел центра экрана. Лежит в самом низу списка виджетов, чтобы рисоваться
        /// поверх боя, но под панелями — он подсказка, а не элемент управления.
        /// </summary>
        private static CrosshairWidget BuildCrosshair(RectTransform screen)
        {
            RectTransform root = Ui.Node("Crosshair", screen).At(Ui.Center, Vector2.zero, new Vector2(30f, 30f));

            RectTransform ring = Ui.Node("Ring", root).Stretch();
            ring.Sprite(Kit.CircleSmall, new Color(1f, 1f, 1f, 0.22f));

            RectTransform dot = Ui.Node("Dot", root).At(Ui.Center, Vector2.zero, new Vector2(6f, 6f));
            dot.Sprite(Kit.CircleSmall, new Color(1f, 1f, 1f, 0.85f));

            root.gameObject.SetActive(false);

            CrosshairWidget widget = screen.gameObject.AddComponent<CrosshairWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("root", root.gameObject);
            }

            return widget;
        }

        private static ToastWidget BuildToast(RectTransform screen)
        {
            RectTransform toast = Ui.Node("Toast", screen).At(Ui.Center, new Vector2(0f, 210f), new Vector2(640f, 72f));
            Image background = toast.Sprite(Kit.Pill, Ui.Danger);

            CanvasGroup group = toast.Group(0f);
            group.blocksRaycasts = false;
            group.interactable = false;

            TextMeshProUGUI label = Ui.Label("Text", toast, "", 28f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle);
            label.rectTransform.Stretch();

            ToastWidget widget = toast.gameObject.AddComponent<ToastWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("group", group)
                    .Ref("label", label)
                    .Ref("background", background);
            }

            return widget;
        }

        #endregion
    }
}
