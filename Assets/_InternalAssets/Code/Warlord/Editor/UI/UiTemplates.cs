using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.UI.Widgets;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Переиспользуемые элементы HUD: карточка юнита, строка таблицы, слот очереди.
    /// Каждый метод собирает готовый к сохранению префаб — раскладку экранов
    /// собирает <see cref="WarlordUiBuilder"/> уже из них.
    /// </summary>
    public static class UiTemplates
    {
        #region Карточка юнита

        public static GameObject UnitCard()
        {
            RectTransform root = Ui.Node("UI_UnitCard", null);
            root.sizeDelta = new Vector2(96f, 122f);

            Image frame = root.Sprite(Kit.ItemSlot, Color.white);
            frame.raycastTarget = true;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.colors = CardColors();

            Image icon = Ui.Icon("Icon", root, Kit.ItemSword, new Vector2(46f, 46f));
            icon.rectTransform.At(Ui.Top, new Vector2(0f, -14f), new Vector2(46f, 46f));

            TextMeshProUGUI name = Ui.Label("Name", root, "Мечник", 17f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontTitle);
            name.rectTransform.At(Ui.Top, new Vector2(0f, -66f), new Vector2(92f, 21f));

            TextMeshProUGUI stats = Ui.Label("Stats", root, "100 HP · 10 урон", 12f, Ui.InkMuted, TextAlignmentOptions.Center);
            stats.rectTransform.At(Ui.Top, new Vector2(0f, -85f), new Vector2(92f, 16f));

            RectTransform costPill = Ui.Node("CostPill", root).At(Ui.Bottom, new Vector2(0f, 8f), new Vector2(76f, 24f));
            costPill.Sprite(Kit.PillSmall, Color.white);

            Image coin = Ui.Icon("Coin", costPill, Kit.ItemGold, new Vector2(14f, 14f));
            coin.rectTransform.At(Ui.Left, new Vector2(7f, 0f), new Vector2(14f, 14f));

            TextMeshProUGUI cost = Ui.Label("Cost", costPill, "60", 17f, Ui.Gold, TextAlignmentOptions.Left, Kit.FontNumbers);
            cost.rectTransform.At(Ui.Left, new Vector2(26f, 0f), new Vector2(44f, 21f));

            RectTransform locked = Ui.Node("Locked", root).Stretch(2f, 2f, 2f, 2f);
            locked.Sprite(Kit.ItemSlotDim, new Color(1f, 1f, 1f, 0.82f));
            locked.gameObject.SetActive(false);

            UnitCardView view = root.gameObject.AddComponent<UnitCardView>();

            using (Bind bind = new(view))
            {
                bind.Ref("icon", icon)
                    .Ref("nameLabel", name)
                    .Ref("costLabel", cost)
                    .Ref("statsLabel", stats)
                    .Ref("button", button)
                    .Ref("frame", frame)
                    .Ref("lockedOverlay", locked.gameObject);
            }

            return root.gameObject;
        }

        private static ColorBlock CardColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.55f, 0.85f);
            colors.fadeDuration = 0.08f;
            return colors;
        }

        /// <summary>
        /// Строка точки в панели гарнизона: название, счётчик «занято / лимит», кнопка покупки
        /// охранника и три кнопки улучшения. Улучшения прячутся у точек без слота — у центра
        /// и у флагов баз их нет (ГДД §2.3, §2.5).
        /// </summary>
        public static GameObject GarrisonRow()
        {
            RectTransform root = Ui.Node("UI_GarrisonRow", null);
            root.sizeDelta = new Vector2(376f, 64f);
            Image frame = root.Sprite(Kit.ListRow, Color.white);

            TextMeshProUGUI name = Ui.Label("Name", root, "Аванпост", 17f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.TopLeft, new Vector2(13f, -8f), new Vector2(150f, 20f));

            Image shield = Ui.Icon("Shield", root, Kit.IconDefense, new Vector2(13f, 13f));
            shield.rectTransform.At(Ui.TopLeft, new Vector2(13f, -28f), new Vector2(13f, 13f));

            TextMeshProUGUI guards = Ui.Label("Guards", root, "0 / 6", 15f, Ui.Gold, TextAlignmentOptions.Left, Kit.FontNumbers);
            guards.rectTransform.At(Ui.TopLeft, new Vector2(30f, -28f), new Vector2(76f, 18f));

            Button buy = Ui.Button("Buy", root, Kit.ButtonGreen, Color.white, out _);
            buy.GetComponent<RectTransform>().At(Ui.TopRight, new Vector2(-11f, -7f), new Vector2(90f, 34f));

            Image goldIcon = Ui.Icon("Gold", buy.transform, Kit.IconCoin, new Vector2(13f, 13f));
            goldIcon.rectTransform.At(Ui.Left, new Vector2(8f, 0f), new Vector2(13f, 13f));

            TextMeshProUGUI cost = Ui.Label("Cost", buy.transform, "70", 17f, Color.white, TextAlignmentOptions.Left, Kit.FontNumbers);
            cost.rectTransform.At(Ui.Left, new Vector2(26f, 0f), new Vector2(56f, 21f));

            RectTransform upgrades = Ui.Node("Upgrades", root).At(Ui.Bottom, new Vector2(0f, 6f), new Vector2(364f, 22f));
            upgrades.Row(4f, TextAnchor.MiddleCenter);

            Button[] buttons = new Button[3];
            Image[] frames = new Image[3];
            TextMeshProUGUI[] labels = new TextMeshProUGUI[3];

            for (int i = 0; i < 3; i++)
            {
                Button button = Ui.Button("Upgrade_" + i, upgrades, Kit.ButtonNavy, Color.white, out Image background);
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(116f, 22f);

                labels[i] = Ui.Label("Text", button.transform, "Улучшение", 13f, Color.white, TextAlignmentOptions.Center);
                labels[i].rectTransform.Stretch();

                buttons[i] = button;
                frames[i] = background;
            }

            GarrisonPointRowView view = root.gameObject.AddComponent<GarrisonPointRowView>();

            using (Bind bind = new(view))
            {
                bind.Ref("nameLabel", name)
                    .Ref("guardLabel", guards)
                    .Ref("costLabel", cost)
                    .Ref("buyButton", buy)
                    .Ref("frame", frame)
                    .Ref("upgradeGroup", upgrades.gameObject)
                    .Refs("upgradeButtons", buttons)
                    .Refs("upgradeFrames", frames)
                    .Refs("upgradeLabels", labels);
            }

            return root.gameObject;
        }

        #endregion

        #region Слот очереди постройки

        public static GameObject SpawnQueueSlot()
        {
            RectTransform root = Ui.Node("UI_SpawnQueueSlot", null);
            root.sizeDelta = new Vector2(40f, 40f);
            root.Sprite(Kit.ItemSlotSmall, Color.white);

            CanvasGroup group = root.Group();

            Image icon = Ui.Icon("Icon", root, Kit.ItemSword, new Vector2(23f, 23f));
            icon.rectTransform.At(Ui.Center, new Vector2(0f, 3f), new Vector2(23f, 23f));

            RectTransform barRoot = Ui.Node("Progress", root).At(Ui.Bottom, new Vector2(0f, 4f), new Vector2(32f, 5f));
            barRoot.Sprite(Kit.ThinBarFrame, new Color(1f, 1f, 1f, 0.5f));

            RectTransform fillRect = Ui.Node("Fill", barRoot).Stretch(2f, 2f, 2f, 2f);
            Image fill = fillRect.Sprite(Kit.ThinBarFill, Ui.Gold);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            TextMeshProUGUI eta = Ui.Label("Eta", root, "", 11f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            eta.rectTransform.At(Ui.Top, new Vector2(0f, -2f), new Vector2(36f, 14f));

            SpawnQueueSlotView view = root.gameObject.AddComponent<SpawnQueueSlotView>();

            using (Bind bind = new(view))
            {
                bind.Ref("icon", icon)
                    .Ref("progressFill", fill)
                    .Ref("etaLabel", eta)
                    .Ref("group", group);
            }

            return root.gameObject;
        }

        #endregion

        #region Ветка прокачки

        public static GameObject UpgradeBranch()
        {
            RectTransform root = Ui.Node("UI_UpgradeBranch", null);
            root.sizeDelta = new Vector2(340f, 58f);
            root.Sprite(Kit.ListRow, Color.white);

            Image icon = Ui.Icon("Icon", root, Kit.IconAttack, new Vector2(27f, 27f));
            icon.rectTransform.At(Ui.Left, new Vector2(13f, 0f), new Vector2(27f, 27f));

            TextMeshProUGUI name = Ui.Label("Name", root, "Урон", 17f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.TopLeft, new Vector2(52f, -9f), new Vector2(150f, 20f));

            TextMeshProUGUI description = Ui.Label("Description", root, "+8% к урону", 13f, Ui.InkMuted, TextAlignmentOptions.Left);
            description.rectTransform.At(Ui.TopLeft, new Vector2(52f, -28f), new Vector2(186f, 16f));

            RectTransform pips = Ui.Node("Pips", root).At(Ui.BottomLeft, new Vector2(52f, 7f), new Vector2(124f, 6f));
            pips.Row(3f, TextAnchor.MiddleLeft);

            RectTransform pipTemplate = Ui.Node("Pip", pips);
            pipTemplate.sizeDelta = new Vector2(15f, 6f);
            Image pip = pipTemplate.Sprite(Kit.PillSmall, new Color(1f, 1f, 1f, 0.25f));
            pipTemplate.gameObject.SetActive(false);

            Button buy = Ui.Button("Buy", root, Kit.ButtonGreen, Color.white, out _);
            buy.GetComponent<RectTransform>().At(Ui.Right, new Vector2(-11f, 0f), new Vector2(80f, 38f));

            Image expIcon = Ui.Icon("Exp", buy.transform, Kit.IconExp, new Vector2(13f, 13f));
            expIcon.rectTransform.At(Ui.Left, new Vector2(8f, 0f), new Vector2(13f, 13f));

            TextMeshProUGUI cost = Ui.Label("Cost", buy.transform, "60", 17f, Color.white, TextAlignmentOptions.Left, Kit.FontNumbers);
            cost.rectTransform.At(Ui.Left, new Vector2(25f, 0f), new Vector2(46f, 21f));

            RectTransform maxed = Ui.Node("Maxed", root).At(Ui.Right, new Vector2(-11f, 0f), new Vector2(80f, 38f));
            maxed.Sprite(Kit.PillSmall, new Color(1f, 1f, 1f, 0.55f));
            Ui.Label("Text", maxed, "MAX", 16f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();
            maxed.gameObject.SetActive(false);

            UpgradeBranchView view = root.gameObject.AddComponent<UpgradeBranchView>();

            using (Bind bind = new(view))
            {
                bind.Ref("icon", icon)
                    .Ref("nameLabel", name)
                    .Ref("descriptionLabel", description)
                    .Ref("pipContainer", pips)
                    .Ref("pipTemplate", pip)
                    .Ref("buyButton", buy)
                    .Ref("costLabel", cost)
                    .Ref("maxedBadge", maxed.gameObject);
            }

            return root.gameObject;
        }

        #endregion

        #region Строка гонки за флаг

        public static GameObject FlagHoldRow()
        {
            RectTransform root = Ui.Node("UI_FlagHoldRow", null);
            root.sizeDelta = new Vector2(268f, 30f);
            root.Sprite(Kit.ListRowFlat, new Color(1f, 1f, 1f, 0.9f));

            CanvasGroup group = root.Group();

            RectTransform tab = Ui.Node("ColorTab", root).At(Ui.Left, new Vector2(6f, 0f), new Vector2(5f, 18f));
            Image colorTab = tab.Sprite(Kit.PillSmall, Color.white);

            Image sigil = Ui.Icon("Sigil", root, null, new Vector2(12f, 12f));
            sigil.rectTransform.At(Ui.Left, new Vector2(15f, 0f), new Vector2(12f, 12f));
            sigil.enabled = false;

            TextMeshProUGUI name = Ui.Label("Name", root, "Игрок 1", 14f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.Left, new Vector2(34f, 0f), new Vector2(76f, 18f));

            RectTransform barRoot = Ui.Node("Bar", root).At(Ui.Left, new Vector2(112f, 0f), new Vector2(100f, 10f));
            barRoot.Sprite(Kit.BarFrame, Color.white);

            RectTransform fillRect = Ui.Node("Fill", barRoot).Stretch(2f, 2f, 2f, 2f);
            Image fill = fillRect.Sprite(Kit.BarFillGreen, Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            TextMeshProUGUI time = Ui.Label("Time", root, "00:00", 14f, Ui.Ink, TextAlignmentOptions.Right, Kit.FontNumbers);
            time.rectTransform.At(Ui.Right, new Vector2(-8f, 0f), new Vector2(50f, 18f));

            Image holding = Ui.Icon("Holding", root, Kit.ItemFlag, new Vector2(12f, 12f));
            holding.rectTransform.At(Ui.Right, new Vector2(-58f, 0f), new Vector2(12f, 12f));
            holding.gameObject.SetActive(false);

            Image eliminated = Ui.Icon("Eliminated", root, Kit.ItemSkull, new Vector2(11f, 11f));
            eliminated.rectTransform.At(Ui.Right, new Vector2(-58f, 0f), new Vector2(11f, 11f));
            eliminated.gameObject.SetActive(false);

            FlagRaceRowView view = root.gameObject.AddComponent<FlagRaceRowView>();

            using (Bind bind = new(view))
            {
                bind.Ref("colorTab", colorTab)
                    .Ref("sigil", sigil)
                    .Ref("fill", fill)
                    .Ref("nameLabel", name)
                    .Ref("timeLabel", time)
                    .Ref("holdingBadge", holding.gameObject)
                    .Ref("eliminatedBadge", eliminated.gameObject)
                    .Ref("group", group);
            }

            return root.gameObject;
        }

        #endregion

        #region Кнопка приказа

        public static GameObject OrderButton()
        {
            RectTransform root = Ui.Node("UI_OrderButton", null);
            root.sizeDelta = new Vector2(60f, 60f);

            // Рамка выбора лежит под фоном и выступает за него на несколько пикселей:
            // видна ровно каёмка, а середину закрывает сама кнопка.
            RectTransform selected = Ui.Node("Selected", root).Stretch(-4f, -4f, -4f, -4f);
            selected.Sprite(Kit.Square, Ui.Gold);
            selected.gameObject.SetActive(false);

            RectTransform backgroundRect = Ui.Node("Background", root).Stretch();
            Image background = backgroundRect.Sprite(Kit.ButtonSquare, Color.white);
            background.raycastTarget = true;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = CardColors();

            Image icon = Ui.Icon("Icon", root, Kit.IconSword, new Vector2(25f, 25f));
            icon.rectTransform.At(Ui.Center, new Vector2(0f, 6f), new Vector2(25f, 25f));

            TextMeshProUGUI label = Ui.Label("Label", root, "Стоять", 12f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontTitle);
            label.rectTransform.At(Ui.Bottom, new Vector2(0f, 5f), new Vector2(58f, 15f));

            RectTransform hotkeyPill = Ui.Node("Hotkey", root).At(Ui.TopRight, new Vector2(-2f, -2f), new Vector2(18f, 18f));
            hotkeyPill.Sprite(Kit.CircleSmall, Color.white);

            TextMeshProUGUI hotkey = Ui.Label("Text", hotkeyPill, "1", 12f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            hotkey.rectTransform.Stretch();

            HotkeyButtonView view = root.gameObject.AddComponent<HotkeyButtonView>();

            using (Bind bind = new(view))
            {
                bind.Ref("icon", icon)
                    .Ref("label", label)
                    .Ref("hotkeyLabel", hotkey)
                    .Ref("button", button)
                    .Ref("background", background)
                    .Ref("selectedFrame", selected.gameObject);
            }

            return root.gameObject;
        }

        #endregion

        #region Строка лобби

        public static GameObject LobbySlot()
        {
            RectTransform root = Ui.Node("UI_LobbySlot", null);
            root.sizeDelta = new Vector2(LobbyColumns.SlotWidth, 78f);
            Image background = root.Sprite(Kit.ListRow, Color.white);

            RectTransform indexPill = Ui.Node("IndexPill", root).At(Ui.Left, new Vector2(18f, 0f), new Vector2(44f, 44f));
            indexPill.Sprite(Kit.CircleSmall, Color.white);

            TextMeshProUGUI index = Ui.Label("Text", indexPill, "1", 22f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            index.rectTransform.Stretch();

            // Дальше вся строка размечена краями, а не центрами: у якорей Ui.Left и Ui.Right
            // пивот стоит на соответствующем крае, и заданное смещение — это положение самого
            // края элемента. Считать здесь центрами значит незаметно наложить кнопки друг
            // на друга ровно на половину их ширины.
            Image swatch = Ui.Icon("Swatch", root, Kit.CircleSmall, new Vector2(16f, 16f));
            swatch.rectTransform.At(Ui.Left, new Vector2(70f, 0f), new Vector2(26f, 26f));

            TextMeshProUGUI name = Ui.Label("Name", root, "Свободно", 23f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.Left, new Vector2(106f, 0f), new Vector2(160f, 30f));
            name.overflowMode = TextOverflowModes.Ellipsis;

            // Управление составом идёт от правого края влево: убрать — сложность —
            // характер — команда. Между блоками оставлено по десятку пикселей,
            // чтобы длинная подпись характера упиралась в свою рамку, а не в соседа.
            Button remove = RowButton("RemoveBot", root, Kit.ButtonRed, "✕", -22f, 48f, 22f, out _);
            Button difficulty = RowButton("Difficulty", root, Kit.ButtonNavy, "Обычный", -82f, 106f, 17f, out TextMeshProUGUI difficultyLabel);
            Button personality = RowButton("Personality", root, Kit.ButtonBlue, "Характер", -200f, 144f, 17f, out TextMeshProUGUI personalityLabel);
            Button team = RowButton("Team", root, Kit.ButtonGray, "—", -352f, 44f, 20f, out TextMeshProUGUI teamLabel);

            Button addBot = RowButton("AddBot", root, Kit.ButtonGreen, "+ БОТ", -22f, 122f, 20f, out _);

            RectTransform readyTag = Ui.Node("ReadyBadge", root).At(Ui.Right, new Vector2(-22f, 0f), new Vector2(126f, 44f));
            readyTag.Sprite(Kit.TagGreen, Color.white);
            Ui.Label("Text", readyTag, "ГОТОВ", 20f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();
            readyTag.gameObject.SetActive(false);

            Image hostBadge = Ui.Icon("HostBadge", root, Kit.IconCrown, new Vector2(14f, 14f), Ui.Gold);
            hostBadge.rectTransform.At(Ui.Right, new Vector2(-166f, 0f), new Vector2(28f, 28f));
            hostBadge.gameObject.SetActive(false);

            TextMeshProUGUI emptyHint = Ui.Label("EmptyHint", root, "ожидание игрока", 18f, Ui.InkMuted, TextAlignmentOptions.Right);
            emptyHint.rectTransform.At(Ui.Right, new Vector2(-22f, 0f), new Vector2(220f, 24f));

            LobbySlotView view = root.gameObject.AddComponent<LobbySlotView>();

            using (Bind bind = new(view))
            {
                bind.Ref("indexLabel", index)
                    .Ref("nameLabel", name)
                    .Ref("colorSwatch", swatch)
                    .Ref("background", background)
                    .Ref("readyBadge", readyTag.gameObject)
                    .Ref("hostBadge", hostBadge.gameObject)
                    .Ref("emptyHint", emptyHint.gameObject)
                    .Ref("addBotButton", addBot)
                    .Ref("removeBotButton", remove)
                    .Ref("personalityButton", personality)
                    .Ref("personalityLabel", personalityLabel)
                    .Ref("difficultyButton", difficulty)
                    .Ref("difficultyLabel", difficultyLabel)
                    .Ref("teamButton", team)
                    .Ref("teamLabel", teamLabel);
            }

            return root.gameObject;
        }

        /// <summary>Кнопка в строке лобби: одинаковая высота, подпись по центру, якорь справа.</summary>
        private static Button RowButton(
            string name,
            RectTransform parent,
            Sprite sprite,
            string caption,
            float offsetFromRight,
            float width,
            float fontSize,
            out TextMeshProUGUI label)
        {
            Button button = Ui.Button(name, parent, sprite, Color.white, out _);
            button.GetComponent<RectTransform>().At(Ui.Right, new Vector2(offsetFromRight, 0f), new Vector2(width, 42f));

            label = Ui.Label("Text", button.transform, caption, fontSize, Color.white, TextAlignmentOptions.Center, Kit.FontTitle);
            label.rectTransform.Stretch(6f, 0f, 6f, 0f);

            // Подписи характеров задаются в ассетах и заранее неизвестны. Обрезаем многоточием
            // вместо переполнения: вылезшая надпись рисуется поверх соседней кнопки и читается
            // как сломанная вёрстка, а не как длинное имя.
            label.overflowMode = TextOverflowModes.Ellipsis;

            button.gameObject.SetActive(false);
            return button;
        }

        #endregion

        #region Строка итогов

        public static GameObject MatchResultRow()
        {
            RectTransform root = Ui.Node("UI_MatchResultRow", null);
            root.sizeDelta = new Vector2(880f, 72f);
            Image background = root.Sprite(Kit.TableRow, new Color(1f, 1f, 1f, 0.9f));

            TextMeshProUGUI rank = Ui.Label("Rank", root, "1", 26f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontNumbers);
            rank.rectTransform.At(Ui.Left, new Vector2(30f, 0f), new Vector2(40f, 34f));

            RectTransform tab = Ui.Node("ColorTab", root).At(Ui.Left, new Vector2(78f, 0f), new Vector2(8f, 40f));
            Image colorTab = tab.Sprite(Kit.PillSmall, Color.white);

            TextMeshProUGUI name = Ui.Label("Name", root, "Игрок 1", 24f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontTitle);
            name.rectTransform.At(Ui.Left, new Vector2(102f, 0f), new Vector2(240f, 30f));

            TextMeshProUGUI flagTime = Ui.Label("FlagTime", root, "00:00", 24f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            flagTime.rectTransform.At(Ui.Center, new Vector2(90f, 0f), new Vector2(140f, 30f));

            TextMeshProUGUI bases = Ui.Label("Bases", root, "0", 24f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            bases.rectTransform.At(Ui.Right, new Vector2(-190f, 0f), new Vector2(90f, 30f));

            TextMeshProUGUI army = Ui.Label("Army", root, "0", 24f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontNumbers);
            army.rectTransform.At(Ui.Right, new Vector2(-90f, 0f), new Vector2(90f, 30f));

            Image winner = Ui.Icon("WinnerBadge", root, Kit.ItemTrophy, new Vector2(17f, 17f));
            winner.rectTransform.At(Ui.Right, new Vector2(-24f, 0f), new Vector2(34f, 34f));
            winner.gameObject.SetActive(false);

            ResultRowView view = root.gameObject.AddComponent<ResultRowView>();

            using (Bind bind = new(view))
            {
                bind.Ref("rankLabel", rank)
                    .Ref("nameLabel", name)
                    .Ref("flagTimeLabel", flagTime)
                    .Ref("basesLabel", bases)
                    .Ref("armyLabel", army)
                    .Ref("colorTab", colorTab)
                    .Ref("background", background)
                    .Ref("winnerBadge", winner.gameObject);
            }

            return root.gameObject;
        }

        #endregion

        #region Метка миникарты

        public static GameObject MinimapMarker()
        {
            RectTransform root = Ui.Node("UI_MinimapMarker", null);
            root.sizeDelta = new Vector2(10f, 10f);

            RectTransform ringRect = Ui.Node("Ring", root).Stretch(-3f, -3f, -3f, -3f);
            Image ring = ringRect.Sprite(Kit.CircleSmall, new Color(1f, 1f, 1f, 0.85f));
            ring.enabled = false;

            Image dot = root.Sprite(Kit.CircleSmall, Color.white);

            Image icon = Ui.Icon("Icon", root, null, new Vector2(8f, 8f));
            icon.enabled = false;

            MinimapMarkerView view = root.gameObject.AddComponent<MinimapMarkerView>();

            using (Bind bind = new(view))
            {
                bind.Ref("rect", root)
                    .Ref("dot", dot)
                    .Ref("icon", icon)
                    .Ref("ring", ring);
            }

            return root.gameObject;
        }

        #endregion

        #region Клетка расстановки армии

        /// <summary>
        /// Одна клетка поля расстановки. Фон обязан ловить лучи: по нему игрок и рисует,
        /// а иконка сверху лучи не перехватывает, иначе мазок рвался бы на занятых клетках.
        /// </summary>
        public static GameObject ArmyPresetCell()
        {
            RectTransform root = Ui.Node("UI_ArmyPresetCell", null);
            root.sizeDelta = new Vector2(34f, 34f);

            Image background = root.Sprite(Kit.ItemSlotSmall, new Color(1f, 1f, 1f, 0.18f));
            background.raycastTarget = true;

            Image icon = Ui.Icon("Icon", root, Kit.ItemSword, new Vector2(21f, 21f));
            icon.rectTransform.At(Ui.Center, Vector2.zero, new Vector2(21f, 21f));
            icon.enabled = false;

            ArmyPresetCellView view = root.gameObject.AddComponent<ArmyPresetCellView>();

            using (Bind bind = new(view))
            {
                bind.Ref("background", background)
                    .Ref("icon", icon);
            }

            return root.gameObject;
        }

        #endregion
    }
}
