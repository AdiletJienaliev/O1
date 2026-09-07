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
            widgets.Add(BuildOrderBar(screen, templates, out Button upgradeToggle, out Button presetToggle));
            widgets.Add(BuildToast(screen));
            widgets.Add(BuildCrosshair(screen));

            UpgradeWidget upgrades = BuildUpgradePanel(screen, templates, out GameObject upgradePanel, out Button upgradeClose);
            widgets.Add(upgrades);

            ArmyPresetWidget preset = BuildArmyPresetPanel(screen, templates, out GameObject presetPanel, out Button presetClose);
            widgets.Add(preset);

            GarrisonShopWidget garrison = BuildGarrisonPanel(
                screen, templates, out GameObject garrisonPanel, out Button garrisonToggle, out Button garrisonClose);
            widgets.Add(garrison);

            HudScreen hud = screen.gameObject.AddComponent<HudScreen>();

            using (Bind bind = new(hud))
            {
                bind.Ref("group", group)
                    .Refs("widgets", widgets.ToArray())
                    .Ref("upgradePanel", upgradePanel)
                    .Ref("upgradeToggleButton", upgradeToggle)
                    .Ref("upgradeCloseButton", upgradeClose)
                    .Ref("presetPanel", presetPanel)
                    .Ref("presetToggleButton", presetToggle)
                    .Ref("presetCloseButton", presetClose)
                    .Ref("garrisonPanel", garrisonPanel)
                    .Ref("garrisonToggleButton", garrisonToggle)
                    .Ref("garrisonCloseButton", garrisonClose);
            }

            return hud;
        }

        #region Ресурсы

        private static ResourceWidget BuildResourceBar(RectTransform screen)
        {
            RectTransform bar = Ui.Node("ResourceBar", screen).At(Ui.TopLeft, new Vector2(14f, -12f), new Vector2(404f, 36f));
            bar.Row(6f);

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
            RectTransform panel = Ui.Node("FlagRace", screen).At(Ui.TopLeft, new Vector2(14f, -54f), new Vector2(288f, 152f));
            panel.Sprite(Kit.PanelLarge, Color.white);

            Image flagIcon = Ui.Icon("Icon", panel, Kit.ItemFlag, new Vector2(13f, 13f));
            flagIcon.rectTransform.At(Ui.TopLeft, new Vector2(11f, -10f), new Vector2(13f, 13f));

            Ui.Label("Title", panel, "УДЕРЖАНИЕ ФЛАГА", 14f, Ui.InkMuted, TextAlignmentOptions.Left, Kit.FontTitle)
                .rectTransform.At(Ui.TopLeft, new Vector2(29f, -10f), new Vector2(190f, 18f));

            RectTransform rows = Ui.Node("Rows", panel).At(Ui.Top, new Vector2(0f, -28f), new Vector2(276f, 116f)).Pivot(Ui.Top);
            rows.Column(3f, TextAnchor.UpperCenter);

            FlagRaceRowView rowTemplate = Spawn<FlagRaceRowView>(templates.FlagHoldRow, rows, "RowTemplate");
            rowTemplate.gameObject.SetActive(false);

            // Баннер владельца флага живёт по центру верха — это общий для всех статус.
            RectTransform banner = Ui.Node("FlagBanner", screen).At(Ui.Top, new Vector2(0f, -74f), new Vector2(268f, 36f));
            banner.Sprite(Kit.Banner, Color.white);

            Image ownerTab = Ui.Icon("OwnerTab", banner, Kit.PillSmall, new Vector2(6f, 18f));
            ownerTab.rectTransform.At(Ui.Left, new Vector2(13f, 0f), new Vector2(6f, 18f));

            TextMeshProUGUI ownerLabel = Ui.Label("Owner", banner, "ФЛАГ СВОБОДЕН", 15f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontTitle);
            ownerLabel.rectTransform.At(Ui.Center, new Vector2(4f, 4f), new Vector2(224f, 18f));

            RectTransform captureBar = Ui.Node("CaptureBar", banner).At(Ui.Bottom, new Vector2(0f, 6f), new Vector2(206f, 6f));
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
            RectTransform clock = Ui.Node("Clock", screen).At(Ui.Top, new Vector2(0f, -10f), new Vector2(162f, 52f));
            clock.Sprite(Kit.Pill, Color.white);

            Image icon = Ui.Icon("Icon", clock, Kit.IconTimer, new Vector2(20f, 20f));
            icon.rectTransform.At(Ui.Left, new Vector2(13f, 0f), new Vector2(20f, 20f));

            TextMeshProUGUI time = Ui.Label("Time", clock, "15:00", 30f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            time.rectTransform.At(Ui.Center, new Vector2(10f, 0f), new Vector2(112f, 34f));

            TextMeshProUGUI phase = Ui.Label("Phase", screen, "Бой", 14f, Ui.InkMuted, TextAlignmentOptions.Center);
            phase.rectTransform.At(Ui.Top, new Vector2(0f, -62f), new Vector2(190f, 18f));

            // Обратный отсчёт перекрывает центр экрана и живёт только в фазе подготовки.
            RectTransform countdown = Ui.Node("Countdown", screen).At(Ui.Center, Vector2.zero, new Vector2(600f, 300f));

            TextMeshProUGUI countdownLabel = Ui.Label("Value", countdown, "3", 120f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontTitle);
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
            RectTransform feed = Ui.Node("EventFeed", screen).At(Ui.TopRight, new Vector2(-14f, -12f), new Vector2(322f, 94f)).Pivot(Ui.TopRight);
            feed.Column(3f, TextAnchor.UpperRight);

            TextMeshProUGUI line = Ui.Label("LineTemplate", feed, "", 15f, Ui.Ink, TextAlignmentOptions.Right);
            line.rectTransform.sizeDelta = new Vector2(310f, 19f);
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
            RectTransform root = Ui.Node("Minimap", screen).At(Ui.TopRight, new Vector2(-14f, -114f), new Vector2(148f, 148f));
            root.Sprite(Kit.PanelSmall, Color.white);

            RectTransform field = Ui.Node("Field", root).Stretch(9f, 9f, 9f, 9f);
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
            RectTransform card = Ui.Node("HeroCard", screen).At(Ui.BottomLeft, new Vector2(14f, 14f), new Vector2(272f, 82f));
            card.Sprite(Kit.PanelLarge, Color.white);

            RectTransform portrait = Ui.Node("Portrait", card).At(Ui.Left, new Vector2(11f, 0f), new Vector2(58f, 58f));
            Image portraitFrame = portrait.Sprite(Kit.CircleLarge, Color.white);

            Ui.Icon("Face", portrait, Kit.ItemCrown, new Vector2(24f, 24f))
                .rectTransform.At(Ui.Center, Vector2.zero, new Vector2(24f, 24f));

            TextMeshProUGUI name = Ui.Label("Name", card, "Игрок 1", 17f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.TopLeft, new Vector2(78f, -13f), new Vector2(124f, 21f));

            RectTransform barRoot = Ui.Node("HealthBar", card).At(Ui.Left, new Vector2(78f, -4f), new Vector2(172f, 16f));
            Slider health = SliderInPlace(barRoot, out Image healthFill);

            TextMeshProUGUI healthLabel = Ui.Label("HealthValue", card, "250 / 250", 14f, Ui.Ink, TextAlignmentOptions.Right, Kit.FontNumbers);
            healthLabel.rectTransform.At(Ui.BottomRight, new Vector2(-12f, 11f), new Vector2(100f, 16f));

            RectTransform buyZone = Ui.Node("BuyZoneBadge", card).At(Ui.TopRight, new Vector2(-11f, -11f), new Vector2(86f, 22f));
            buyZone.Sprite(Kit.TagGreen, Color.white);
            Ui.Label("Text", buyZone, "НА БАЗЕ", 13f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();
            buyZone.gameObject.SetActive(false);

            RectTransform death = Ui.Node("DeathOverlay", card).Stretch(4f, 4f, 4f, 4f);
            death.Sprite(Kit.PanelGray, new Color(0.5f, 0.12f, 0.12f, 0.92f));

            TextMeshProUGUI deathLabel = Ui.Label("Text", death, "Полководец пал", 18f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle);
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

            RectTransform fillArea = Ui.Node("Fill_Area", rect).Stretch(3f, 3f, 3f, 3f);
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
            RectTransform queue = Ui.Node("SpawnQueue", screen).At(Ui.Bottom, new Vector2(0f, 168f), new Vector2(346f, 50f));

            RectTransform slots = Ui.Node("Slots", queue).At(Ui.Center, Vector2.zero, new Vector2(334f, 46f));
            slots.Row(4f, TextAnchor.MiddleCenter);

            SpawnQueueSlotView slotTemplate = Spawn<SpawnQueueSlotView>(templates.SpawnQueueSlot, slots, "SlotTemplate");
            slotTemplate.gameObject.SetActive(false);

            TextMeshProUGUI counter = Ui.Label("Counter", queue, "", 14f, Ui.InkMuted, TextAlignmentOptions.Left, Kit.FontNumbers);
            counter.rectTransform.At(Ui.Right, new Vector2(-2f, 0f), new Vector2(38f, 16f));

            TextMeshProUGUI empty = Ui.Label("EmptyHint", queue, "очередь пуста", 13f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Center);
            empty.rectTransform.At(Ui.Center, Vector2.zero, new Vector2(186f, 16f));

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
            RectTransform shop = Ui.Node("UnitShop", screen).At(Ui.Bottom, new Vector2(0f, 16f), new Vector2(540f, 142f));

            RectTransform cards = Ui.Node("Cards", shop).At(Ui.Center, Vector2.zero, new Vector2(528f, 136f));
            cards.Row(6f, TextAnchor.MiddleCenter);

            UnitCardView cardTemplate = Spawn<UnitCardView>(templates.UnitCard, cards, "CardTemplate");
            cardTemplate.gameObject.SetActive(false);

            RectTransform hint = Ui.Node("BuyZoneHint", shop).At(Ui.Top, new Vector2(0f, 78f), new Vector2(324f, 28f));
            hint.Sprite(Kit.PillSmall, new Color(1f, 1f, 1f, 0.9f));

            TextMeshProUGUI hintLabel = Ui.Label("Text", hint, "Покупка доступна только на своей базе", 14f, Ui.Danger, TextAlignmentOptions.Center);
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

        private static OrderBarWidget BuildOrderBar(
            RectTransform screen,
            Templates templates,
            out Button upgradeToggle,
            out Button presetToggle)
        {
            RectTransform bar = Ui.Node("OrderBar", screen).At(Ui.BottomRight, new Vector2(-14f, 14f), new Vector2(272f, 170f)).Pivot(Ui.BottomRight);

            RectTransform orders = Ui.Node("Orders", bar).At(Ui.Bottom, new Vector2(0f, 0f), new Vector2(268f, 62f));
            orders.Row(5f, TextAnchor.MiddleCenter);

            // Приказов стало четыре вместе с «Защитой» — кнопки поуже, ряд как у построений.
            HotkeyButtonView[] orderButtons = new HotkeyButtonView[4];
            for (int i = 0; i < orderButtons.Length; i++)
            {
                orderButtons[i] = Spawn<HotkeyButtonView>(templates.OrderButton, orders, "Order_" + i);
                orderButtons[i].GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);
            }

            RectTransform formations = Ui.Node("Formations", bar).At(Ui.Bottom, new Vector2(0f, 66f), new Vector2(268f, 62f));
            formations.Row(5f, TextAnchor.MiddleCenter);

            HotkeyButtonView formationTemplate = Spawn<HotkeyButtonView>(templates.OrderButton, formations, "FormationTemplate");

            // Построений теперь четыре вместе с пользовательским строем — кнопки поуже.
            formationTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);
            formationTemplate.gameObject.SetActive(false);

            // Три кнопки панелей идут одной колонкой над рядами приказов: они открываются
            // на базе, а не в бою, и держать их вплотную к боевым кнопкам не нужно.
            upgradeToggle = MenuButton("UpgradeToggle", bar, "ПРОКАЧКА  ⇥", Kit.ButtonNavy);
            upgradeToggle.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(0f, 132f), new Vector2(176f, 30f));

            presetToggle = MenuButton("PresetToggle", bar, "РАССТАНОВКА  B", Kit.ButtonNavy);
            presetToggle.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(0f, 166f), new Vector2(176f, 30f));

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

        /// <summary>
        /// Панель гарнизона (ГДД §1.9): свои точки списком со счётчиком охранников, покупка
        /// на выбранную точку и выбор улучшения аванпоста.
        ///
        /// Кнопка открытия живёт прямо на панели, а не в полосе приказов: приказы отдаются
        /// в бою одной клавишей, а гарнизон покупается на базе — это разные режимы игры,
        /// и мешать их в одном ряду значит промахиваться в бою по кнопке магазина.
        /// </summary>
        private static GarrisonShopWidget BuildGarrisonPanel(
            RectTransform screen,
            Templates templates,
            out GameObject panelObject,
            out Button toggle,
            out Button close)
        {
            toggle = Ui.Button("GarrisonToggle", screen, Kit.ButtonNavy, Color.white, out _);
            toggle.GetComponent<RectTransform>().At(Ui.BottomRight, new Vector2(-62f, 214f), new Vector2(176f, 30f));
            Ui.Label("Text", toggle.transform, "ГАРНИЗОН  G", 15f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            RectTransform overlay = Ui.Node("GarrisonOverlay", screen).Stretch();
            overlay.Sprite(null, new Color(0.04f, 0.05f, 0.08f, 0.7f)).raycastTarget = true;

            RectTransform panel = Ui.Node("Panel", overlay).At(Ui.Center, Vector2.zero, new Vector2(440f, 440f));
            panel.Sprite(Kit.PopupBody, Color.white);

            RectTransform header = Ui.Node("Header", panel).At(Ui.Top, new Vector2(0f, 13f), new Vector2(288f, 52f));
            header.Sprite(Kit.PopupHeader, Color.white);
            Ui.Label("Text", header, "ГАРНИЗОН", 20f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            TextMeshProUGUI summary = Ui.Label("Summary", panel, "Армия 0 / 20 · Гарнизон 0",
                15f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontNumbers);
            summary.rectTransform.At(Ui.Top, new Vector2(0f, -58f), new Vector2(324f, 22f));

            RectTransform rows = Ui.Node("Rows", panel).At(Ui.Top, new Vector2(0f, -84f), new Vector2(396f, 330f)).Pivot(Ui.Top);
            rows.Column(5f, TextAnchor.UpperCenter);

            GarrisonPointRowView rowTemplate = Spawn<GarrisonPointRowView>(templates.GarrisonRow, rows, "RowTemplate");
            rowTemplate.gameObject.SetActive(false);

            RectTransform empty = Ui.Node("EmptyHint", panel).At(Ui.Center, new Vector2(0f, -24f), new Vector2(348f, 38f));
            Ui.Label("Text", empty, "Захватите точку, чтобы поставить на неё гарнизон",
                    15f, Ui.InkMuted, TextAlignmentOptions.Center)
                .rectTransform.Stretch();

            close = Ui.Button("Close", panel, Kit.ButtonCircle, Color.white, out _);
            close.GetComponent<RectTransform>().At(Ui.TopRight, new Vector2(-9f, -9f), new Vector2(34f, 34f));
            Ui.Icon("Icon", close.transform, Kit.IconClose, new Vector2(14f, 14f))
                .rectTransform.At(Ui.Center, Vector2.zero, new Vector2(14f, 14f));

            overlay.gameObject.SetActive(false);
            panelObject = overlay.gameObject;

            GarrisonShopWidget widget = panel.gameObject.AddComponent<GarrisonShopWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("container", rows)
                    .Ref("rowTemplate", rowTemplate)
                    .Ref("summaryLabel", summary)
                    .Ref("emptyHint", empty.gameObject);
            }

            return widget;
        }

        private static UpgradeWidget BuildUpgradePanel(RectTransform screen, Templates templates, out GameObject panelObject, out Button close)
        {
            RectTransform overlay = Ui.Node("UpgradeOverlay", screen).Stretch();
            overlay.Sprite(null, new Color(0.04f, 0.05f, 0.08f, 0.7f)).raycastTarget = true;

            RectTransform panel = Ui.Node("Panel", overlay).At(Ui.Center, new Vector2(0f, 0f), new Vector2(432f, 486f));
            panel.Sprite(Kit.PopupBody, Color.white);

            RectTransform header = Ui.Node("Header", panel).At(Ui.Top, new Vector2(0f, 13f), new Vector2(276f, 52f));
            header.Sprite(Kit.PopupHeader, Color.white);
            Ui.Label("Text", header, "ПРОКАЧКА", 20f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            RectTransform xpChip = Chip("XpChip", panel, Kit.ItemXp, out TextMeshProUGUI xp, out TextMeshProUGUI xpCaption);
            xpChip.At(Ui.Top, new Vector2(0f, -58f), new Vector2(126f, 36f));
            xpCaption.text = "доступно опыта";

            RectTransform branches = Ui.Node("Branches", panel).At(Ui.Top, new Vector2(0f, -102f), new Vector2(384f, 376f)).Pivot(Ui.Top);
            branches.Column(4f, TextAnchor.UpperCenter);

            UpgradeBranchView branchTemplate = Spawn<UpgradeBranchView>(templates.UpgradeBranch, branches, "BranchTemplate");
            branchTemplate.gameObject.SetActive(false);

            close = Ui.Button("Close", panel, Kit.ButtonCircle, Color.white, out _);
            close.GetComponent<RectTransform>().At(Ui.TopRight, new Vector2(-9f, -9f), new Vector2(34f, 34f));
            Ui.Icon("Icon", close.transform, Kit.IconClose, new Vector2(14f, 14f))
                .rectTransform.At(Ui.Center, Vector2.zero, new Vector2(14f, 14f));

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
        /// Панель расстановки армии: слева типы юнитов, справа поле строя. Игрок выбирает тип
        /// и проводит мышью по клеткам — получается своё построение наравне с линией и клином.
        /// Передняя шеренга сверху: так поле читается как вид на строй со стороны противника.
        /// </summary>
        private static ArmyPresetWidget BuildArmyPresetPanel(
            RectTransform screen,
            Templates templates,
            out GameObject panelObject,
            out Button close)
        {
            const int Columns = 11;
            const int Rows = 6;

            RectTransform overlay = Ui.Node("PresetOverlay", screen).Stretch();
            overlay.Sprite(null, new Color(0.04f, 0.05f, 0.08f, 0.7f)).raycastTarget = true;

            RectTransform panel = Ui.Node("Panel", overlay).At(Ui.Center, Vector2.zero, new Vector2(680f, 476f));
            panel.Sprite(Kit.PopupBody, Color.white);

            RectTransform header = Ui.Node("Header", panel).At(Ui.Top, new Vector2(0f, 13f), new Vector2(312f, 52f));
            header.Sprite(Kit.PopupHeader, Color.white);
            Ui.Label("Text", header, "РАССТАНОВКА АРМИИ", 20f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            RectTransform palette = Ui.Node("Palette", panel)
                .At(Ui.TopLeft, new Vector2(24f, -68f), new Vector2(126f, 340f))
                .Pivot(Ui.TopLeft);
            palette.Column(5f, TextAnchor.UpperCenter);

            HotkeyButtonView paletteTemplate = Spawn<HotkeyButtonView>(templates.OrderButton, palette, "PaletteTemplate");
            paletteTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 52f);
            paletteTemplate.gameObject.SetActive(false);

            RectTransform field = Ui.Node("Field", panel)
                .At(Ui.TopLeft, new Vector2(168f, -68f), new Vector2(484f, 340f))
                .Pivot(Ui.TopLeft);

            Ui.Label("Front", field, "фронт", 13f, Ui.InkMuted, TextAlignmentOptions.Center)
                .rectTransform.At(Ui.Top, new Vector2(0f, 0f), new Vector2(126f, 16f));

            // Слой колец радиуса создаётся ДО сетки: порядок в иерархии здесь и есть
            // порядок отрисовки, а кольца обязаны лежать под клетками, а не поверх иконок.
            RectTransform ranges = Ui.Node("Ranges", field)
                .At(Ui.Top, new Vector2(0f, -18f), new Vector2(484f, 286f))
                .Pivot(Ui.Top);

            // Радиус лучника шире всего поля расстановки: без маски его кольцо накрыло бы
            // палитру и кнопки. Обрезанная по краю дуга читается ничуть не хуже.
            ranges.gameObject.AddComponent<RectMask2D>();

            Image rangeTemplate = Ui.Icon("RangeTemplate", ranges, Kit.CircleSmall, new Vector2(66f, 66f));
            rangeTemplate.gameObject.SetActive(false);

            RectTransform grid = Ui.Node("Grid", field)
                .At(Ui.Top, new Vector2(0f, -18f), new Vector2(484f, 286f))
                .Pivot(Ui.Top);
            grid.Grid(new Vector2(34f, 34f), new Vector2(3f, 3f), Columns);

            ArmyPresetCellView cellTemplate = Spawn<ArmyPresetCellView>(templates.ArmyPresetCell, grid, "CellTemplate");
            cellTemplate.gameObject.SetActive(false);

            TextMeshProUGUI hint = Ui.Label(
                "Hint",
                panel,
                "Выберите тип слева и проведите мышью по полю",
                14f,
                Ui.InkMuted,
                TextAlignmentOptions.Center);
            hint.rectTransform.At(Ui.Bottom, new Vector2(0f, 66f), new Vector2(556f, 18f));

            Button apply = MenuButton("Apply", panel, "ПРИМЕНИТЬ", Kit.ButtonGreen);
            apply.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(-92f, 20f), new Vector2(172f, 34f));

            Button clear = MenuButton("Clear", panel, "ОЧИСТИТЬ", Kit.ButtonGray);
            clear.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(92f, 20f), new Vector2(172f, 34f));

            close = Ui.Button("Close", panel, Kit.ButtonCircle, Color.white, out _);
            close.GetComponent<RectTransform>().At(Ui.TopRight, new Vector2(-9f, -9f), new Vector2(34f, 34f));
            Ui.Icon("Icon", close.transform, Kit.IconClose, new Vector2(14f, 14f))
                .rectTransform.At(Ui.Center, Vector2.zero, new Vector2(14f, 14f));

            overlay.gameObject.SetActive(false);
            panelObject = overlay.gameObject;

            ArmyPresetWidget widget = panel.gameObject.AddComponent<ArmyPresetWidget>();

            using (Bind bind = new(widget))
            {
                bind.Ref("paletteContainer", palette)
                    .Ref("paletteTemplate", paletteTemplate)
                    .Ref("gridContainer", grid)
                    .Ref("cellTemplate", cellTemplate)
                    .Ref("rangeContainer", ranges)
                    .Ref("rangeTemplate", rangeTemplate)
                    .Ref("applyButton", apply)
                    .Ref("clearButton", clear)
                    .Ref("hintLabel", hint)
                    .Ref("fallbackIcon", Kit.ItemSword)
                    .Int("columns", Columns)
                    .Int("rows", Rows);
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
