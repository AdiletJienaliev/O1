using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Warlord.UI;
using Warlord.UI.Screens;
using Warlord.UI.Widgets;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Раскладка экранов. Порядок блоков в коде повторяет порядок на экране —
    /// сверху вниз, слева направо, чтобы правку было где искать.
    /// </summary>
    public static partial class WarlordUiBuilder
    {
        private static GameObject BuildRoot(Templates templates)
        {
            GameObject rootGo = new("UI_Root", typeof(RectTransform));
            RectTransform root = (RectTransform)rootGo.transform;
            root.Stretch();

            CreateCanvas(rootGo);

            ConnectScreen connect = BuildConnectScreen(root);
            LobbyScreen lobby = BuildLobbyScreen(root, templates);
            HudScreen hud = BuildHudScreen(root, templates);
            ResultScreen result = BuildResultScreen(root, templates);

            WarlordHud warlordHud = rootGo.AddComponent<WarlordHud>();

            using (Bind bind = new(warlordHud))
            {
                bind.Ref("connectScreen", connect)
                    .Ref("lobbyScreen", lobby)
                    .Ref("hudScreen", hud)
                    .Ref("resultScreen", result);
            }

            return rootGo;
        }

        #region Экран подключения

        private static ConnectScreen BuildConnectScreen(RectTransform parent)
        {
            RectTransform screen = ScreenRoot("Screen_Connect", parent, out CanvasGroup group);

            RectTransform backdrop = Ui.Node("Backdrop", screen).Stretch();
            backdrop.Sprite(Kit.BackgroundGradient, new Color(0.06f, 0.07f, 0.11f, 1f), Image.Type.Simple);

            RectTransform glow = Ui.Node("Glow", screen).Stretch();
            glow.Sprite(Kit.BackgroundGlow, new Color(1f, 1f, 1f, 0.18f), Image.Type.Simple);

            TextMeshProUGUI title = Ui.Label("Title", screen, "WARLORD", 96f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontTitle);
            title.rectTransform.At(Ui.Top, new Vector2(0f, -110f), new Vector2(900f, 110f));
            title.Shadow(new Color(0f, 0f, 0f, 0.6f), new Vector2(3f, -4f));

            TextMeshProUGUI subtitle = Ui.Label("Subtitle", screen, "Полководец · арена на четверых", 26f, Ui.InkMuted, TextAlignmentOptions.Center);
            subtitle.rectTransform.At(Ui.Top, new Vector2(0f, -214f), new Vector2(900f, 34f));

            RectTransform panel = Ui.Node("Panel", screen).At(Ui.Center, new Vector2(0f, -30f), new Vector2(660f, 520f));
            panel.Sprite(Kit.PopupBody, Color.white);

            RectTransform header = Ui.Node("Header", panel).At(Ui.Top, new Vector2(0f, 18f), new Vector2(420f, 84f));
            header.Sprite(Kit.PopupHeader, Color.white);
            Ui.Label("Text", header, "СЕТЕВАЯ ИГРА", 30f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            TMP_InputField address = InputField("Address", panel, "127.0.0.1", "адрес хоста");
            address.GetComponent<RectTransform>().At(Ui.Top, new Vector2(0f, -110f), new Vector2(520f, 68f));

            TMP_InputField port = InputField("Port", panel, "7770", "порт");
            port.GetComponent<RectTransform>().At(Ui.Top, new Vector2(0f, -190f), new Vector2(520f, 68f));
            port.contentType = TMP_InputField.ContentType.IntegerNumber;

            Button host = MenuButton("HostButton", panel, "СОЗДАТЬ ХОСТ", Kit.ButtonGreen);
            host.GetComponent<RectTransform>().At(Ui.Top, new Vector2(0f, -282f), new Vector2(430f, 76f));

            Button join = MenuButton("JoinButton", panel, "ПОДКЛЮЧИТЬСЯ", Kit.ButtonBlue);
            join.GetComponent<RectTransform>().At(Ui.Top, new Vector2(0f, -370f), new Vector2(430f, 76f));

            Button quit = MenuButton("QuitButton", panel, "ВЫХОД", Kit.ButtonGray);
            quit.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(0f, 26f), new Vector2(200f, 60f));

            TextMeshProUGUI status = Ui.Label("Status", screen, "", 22f, Ui.InkMuted, TextAlignmentOptions.Center);
            status.rectTransform.At(Ui.Bottom, new Vector2(0f, 96f), new Vector2(900f, 30f));

            TextMeshProUGUI version = Ui.Label("Version", screen, "", 18f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Right);
            version.rectTransform.At(Ui.BottomRight, new Vector2(-24f, 20f), new Vector2(240f, 24f));

            ConnectScreen connect = screen.gameObject.AddComponent<ConnectScreen>();

            using (Bind bind = new(connect))
            {
                bind.Ref("group", group)
                    .Ref("hostButton", host)
                    .Ref("joinButton", join)
                    .Ref("quitButton", quit)
                    .Ref("addressField", address)
                    .Ref("portField", port)
                    .Ref("statusLabel", status)
                    .Ref("versionLabel", version)
                    .Ref("panel", panel);
            }

            return connect;
        }

        #endregion

        #region Экран лобби

        private static LobbyScreen BuildLobbyScreen(RectTransform parent, Templates templates)
        {
            RectTransform screen = ScreenRoot("Screen_Lobby", parent, out CanvasGroup group);

            RectTransform backdrop = Ui.Node("Backdrop", screen).Stretch();
            backdrop.Sprite(Kit.BackgroundGradient, new Color(0.06f, 0.07f, 0.11f, 1f), Image.Type.Simple);

            TextMeshProUGUI title = Ui.Label("Title", screen, "ЛОББИ", 64f, Ui.Ink, TextAlignmentOptions.Center, Kit.FontTitle);
            title.rectTransform.At(Ui.Top, new Vector2(0f, -50f), new Vector2(700f, 76f));

            TextMeshProUGUI map = Ui.Label("Map", screen, "Арена", 26f, Ui.InkMuted, TextAlignmentOptions.Center);
            map.rectTransform.At(Ui.Top, new Vector2(0f, -128f), new Vector2(700f, 34f));

            // Экран раскладывается в четыре колонки: список Steam-комнаты, слоты, настройки,
            // чат. Ширины и отступы держим здесь одним расчётом (LobbyColumns), иначе панели
            // Steam, которые ставятся отдельным скриптом, встают вплотную к базовым и налезают.
            RectTransform panel = Ui.Node("Players", screen)
                .At(Ui.Center, new Vector2(LobbyColumns.PlayersX, LobbyColumns.CenterY), new Vector2(LobbyColumns.PlayersWidth, LobbyColumns.Height));
            panel.Sprite(Kit.PanelLarge, Color.white);

            RectTransform slots = Ui.Node("Slots", panel)
                .At(Ui.Top, new Vector2(0f, -40f), new Vector2(LobbyColumns.PlayersWidth - 50f, 380f)).Pivot(Ui.Top);
            slots.Column(12f, TextAnchor.UpperCenter);

            LobbySlotView slotTemplate = Spawn<LobbySlotView>(templates.LobbySlot, slots, "SlotTemplate");
            slotTemplate.gameObject.SetActive(false);

            // Цвета и настройки комнаты.
            RectTransform side = Ui.Node("Side", screen)
                .At(Ui.Center, new Vector2(LobbyColumns.SideX, LobbyColumns.CenterY), new Vector2(LobbyColumns.SideWidth, LobbyColumns.Height));
            side.Sprite(Kit.PanelLarge, Color.white);

            Ui.Label("ColorTitle", side, "ЦВЕТ", 24f, Ui.InkMuted, TextAlignmentOptions.Left, Kit.FontTitle)
                .rectTransform.At(Ui.TopLeft, new Vector2(32f, -34f), new Vector2(260f, 30f));

            RectTransform colors = Ui.Node("Colors", side).At(Ui.TopLeft, new Vector2(32f, -74f), new Vector2(LobbyColumns.SideWidth - 64f, 64f));
            colors.Row(16f);

            Button colorTemplate = Ui.Button("ColorTemplate", colors, Kit.CircleSmall, Color.white, out _);
            colorTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);
            colorTemplate.gameObject.SetActive(false);

            // Боты. Стоят между цветом и настройками комнаты: набрать состав нужно
            // раньше, чем крутить длительность матча, и кнопка «заполнить» должна быть
            // на виду — ради неё половина сессий и запускается в одиночку.
            Button fillBots = MenuButton("FillBotsButton", side, "ЗАПОЛНИТЬ БОТАМИ", Kit.ButtonGreen, 22f);
            fillBots.GetComponent<RectTransform>()
                .At(Ui.TopLeft, new Vector2(32f, -152f), new Vector2(LobbyColumns.SideWidth - 64f, 56f)).Pivot(Ui.TopLeft);

            Button botDifficulty = MenuButton("BotDifficultyButton", side, "НОВЫЕ БОТЫ: ОБЫЧНЫЙ", Kit.ButtonNavy, 18f);
            botDifficulty.GetComponent<RectTransform>()
                .At(Ui.TopLeft, new Vector2(32f, -216f), new Vector2(LobbyColumns.SideWidth - 64f, 48f)).Pivot(Ui.TopLeft);
            TextMeshProUGUI botDifficultyLabel = botDifficulty.GetComponentInChildren<TextMeshProUGUI>();

            RectTransform settings = Ui.Node("HostSettings", side)
                .At(Ui.TopLeft, new Vector2(32f, -284f), new Vector2(LobbyColumns.SideWidth - 64f, 190f)).Pivot(Ui.TopLeft);

            Ui.Label("SettingsTitle", settings, "НАСТРОЙКИ КОМНАТЫ", 24f, Ui.InkMuted, TextAlignmentOptions.Left, Kit.FontTitle)
                .rectTransform.At(Ui.TopLeft, new Vector2(0f, 0f), new Vector2(400f, 30f));

            Slider duration = SettingSlider("Duration", settings, "Длительность", new Vector2(0f, -54f), out TextMeshProUGUI durationValue);
            Slider gold = SettingSlider("StartingGold", settings, "Стартовое золото", new Vector2(0f, -128f), out TextMeshProUGUI goldValue);

            // Управление.
            Button ready = MenuButton("ReadyButton", screen, "ГОТОВ", Kit.ButtonGreen);
            ready.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(-240f, 60f), new Vector2(300f, 84f));
            TextMeshProUGUI readyLabel = ready.GetComponentInChildren<TextMeshProUGUI>();

            Button start = MenuButton("StartButton", screen, "НАЧАТЬ МАТЧ", Kit.ButtonOrange);
            start.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(120f, 60f), new Vector2(360f, 84f));

            TextMeshProUGUI startHint = Ui.Label("StartHint", screen, "Ждём готовности всех игроков", 20f, Ui.InkMuted, TextAlignmentOptions.Center);
            startHint.rectTransform.At(Ui.Bottom, new Vector2(120f, 24f), new Vector2(420f, 26f));

            Button leave = MenuButton("LeaveButton", screen, "ВЫЙТИ", Kit.ButtonGray);
            leave.GetComponent<RectTransform>().At(Ui.TopLeft, new Vector2(30f, -30f), new Vector2(180f, 62f));

            // Приглашения. Экран сам прячет их, когда играем по адресу, а не через платформу.
            Button invite = MenuButton("InviteButton", screen, "ПРИГЛАСИТЬ ДРУЗЕЙ", Kit.ButtonBlue);
            invite.GetComponent<RectTransform>().At(Ui.TopRight, new Vector2(-30f, -30f), new Vector2(340f, 62f));

            TextMeshProUGUI platformMembers = Ui.Label("PlatformMembers", screen, "", 20f, Ui.InkMuted, TextAlignmentOptions.Right);
            platformMembers.rectTransform.At(Ui.TopRight, new Vector2(-30f, -100f), new Vector2(440f, 26f));

            LobbyScreen lobby = screen.gameObject.AddComponent<LobbyScreen>();

            using (Bind bind = new(lobby))
            {
                bind.Ref("group", group)
                    .Ref("slotContainer", slots)
                    .Ref("slotTemplate", slotTemplate)
                    .Ref("colorContainer", colors)
                    .Ref("colorTemplate", colorTemplate)
                    .Ref("readyButton", ready)
                    .Ref("readyLabel", readyLabel)
                    .Ref("startButton", start)
                    .Ref("startHintLabel", startHint)
                    .Ref("leaveButton", leave)
                    .Ref("hostSettingsRoot", settings.gameObject)
                    .Ref("durationSlider", duration)
                    .Ref("durationLabel", durationValue)
                    .Ref("startingGoldSlider", gold)
                    .Ref("startingGoldLabel", goldValue)
                    .Ref("mapLabel", map)
                    .Ref("inviteButton", invite)
                    .Ref("platformMembersLabel", platformMembers)
                    .Ref("fillBotsButton", fillBots)
                    .Ref("botDifficultyButton", botDifficulty)
                    .Ref("botDifficultyLabel", botDifficultyLabel);
            }

            return lobby;
        }

        #endregion

        #region Экран итогов

        private static ResultScreen BuildResultScreen(RectTransform parent, Templates templates)
        {
            RectTransform screen = ScreenRoot("Screen_Result", parent, out CanvasGroup group);

            RectTransform shade = Ui.Node("Shade", screen).Stretch();
            shade.Sprite(null, new Color(0.04f, 0.05f, 0.08f, 0.88f));

            TextMeshProUGUI title = Ui.Label("Title", screen, "ПОБЕДА", 96f, Ui.Gold, TextAlignmentOptions.Center, Kit.FontTitle);
            title.rectTransform.At(Ui.Top, new Vector2(0f, -90f), new Vector2(1000f, 110f));
            title.Shadow(new Color(0f, 0f, 0f, 0.6f), new Vector2(3f, -4f));

            RectTransform accent = Ui.Node("Accent", screen).At(Ui.Top, new Vector2(0f, -206f), new Vector2(420f, 8f));
            Image accentBar = accent.Sprite(Kit.PillSmall, Ui.Gold);

            TextMeshProUGUI reason = Ui.Label("Reason", screen, "", 28f, Ui.InkMuted, TextAlignmentOptions.Center);
            reason.rectTransform.At(Ui.Top, new Vector2(0f, -238f), new Vector2(1000f, 36f));

            RectTransform table = Ui.Node("Table", screen).At(Ui.Center, new Vector2(0f, -20f), new Vector2(960f, 420f));
            table.Sprite(Kit.PanelLarge, Color.white);

            RectTransform head = Ui.Node("Head", table).At(Ui.Top, new Vector2(0f, -22f), new Vector2(880f, 30f));
            Ui.Label("H1", head, "ИГРОК", 20f, Ui.InkMuted, TextAlignmentOptions.Left)
                .rectTransform.At(Ui.Left, new Vector2(100f, 0f), new Vector2(200f, 24f));
            Ui.Label("H2", head, "ФЛАГ", 20f, Ui.InkMuted, TextAlignmentOptions.Center)
                .rectTransform.At(Ui.Center, new Vector2(90f, 0f), new Vector2(140f, 24f));
            Ui.Label("H3", head, "БАЗЫ", 20f, Ui.InkMuted, TextAlignmentOptions.Center)
                .rectTransform.At(Ui.Right, new Vector2(-190f, 0f), new Vector2(90f, 24f));
            Ui.Label("H4", head, "АРМИЯ", 20f, Ui.InkMuted, TextAlignmentOptions.Center)
                .rectTransform.At(Ui.Right, new Vector2(-90f, 0f), new Vector2(90f, 24f));

            RectTransform rows = Ui.Node("Rows", table).At(Ui.Top, new Vector2(0f, -60f), new Vector2(890f, 330f)).Pivot(Ui.Top);
            rows.Column(10f, TextAnchor.UpperCenter);

            ResultRowView rowTemplate = Spawn<ResultRowView>(templates.MatchResultRow, rows, "RowTemplate");
            rowTemplate.gameObject.SetActive(false);

            Button leave = MenuButton("LeaveButton", screen, "В МЕНЮ", Kit.ButtonBlue);
            leave.GetComponent<RectTransform>().At(Ui.Bottom, new Vector2(0f, 70f), new Vector2(360f, 84f));

            ResultScreen result = screen.gameObject.AddComponent<ResultScreen>();

            using (Bind bind = new(result))
            {
                bind.Ref("group", group)
                    .Ref("titleLabel", title)
                    .Ref("reasonLabel", reason)
                    .Ref("accentBar", accentBar)
                    .Ref("rowContainer", rows)
                    .Ref("rowTemplate", rowTemplate)
                    .Ref("leaveButton", leave);
            }

            return result;
        }

        #endregion

        #region Общие детали

        private static RectTransform ScreenRoot(string name, RectTransform parent, out CanvasGroup group)
        {
            RectTransform screen = Ui.Node(name, parent).Stretch();
            group = screen.Group();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return screen;
        }

        /// <summary>
        /// Кнопка с подписью. Кегль вынесен в параметр: 28f верен для больших кнопок меню,
        /// но в HUD те же кнопки высотой 30px, и текст такого размера вылезал за экран.
        /// </summary>
        private static Button MenuButton(string name, Transform parent, string caption, Sprite sprite, float fontSize = 28f)
        {
            Button button = Ui.Button(name, parent, sprite, Color.white, out Image background);
            background.Shadow(new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -3f));

            TextMeshProUGUI label = Ui.Label("Text", button.transform, caption, fontSize, Color.white, TextAlignmentOptions.Center, Kit.FontTitle);
            label.rectTransform.Stretch(0f, 4f, 0f, 0f);

            return button;
        }

        private static TMP_InputField InputField(string name, Transform parent, string text, string placeholder)
        {
            TMP_DefaultControls.Resources resources = new() { inputField = Kit.InputFrame };
            GameObject go = TMP_DefaultControls.CreateInputField(resources);

            go.name = name;
            go.transform.SetParent(parent, false);

            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.text = text;

            if (field.placeholder is TextMeshProUGUI hint)
            {
                hint.text = placeholder;
                hint.fontSize = 24f;
                hint.color = new Color(0.5f, 0.54f, 0.62f, 1f);

                if (Kit.FontBody != null)
                    hint.font = Kit.FontBody;
            }

            if (field.textComponent is TextMeshProUGUI content)
            {
                content.fontSize = 26f;
                content.color = new Color(0.12f, 0.14f, 0.2f);

                if (Kit.FontBody != null)
                    content.font = Kit.FontBody;
            }

            return field;
        }

        private static Slider SettingSlider(string name, Transform parent, string caption, Vector2 position, out TextMeshProUGUI value)
        {
            RectTransform group = Ui.Node(name, parent).At(Ui.TopLeft, position, new Vector2(460f, 60f));

            Ui.Label("Caption", group, caption, 22f, Ui.InkMuted, TextAlignmentOptions.Left)
                .rectTransform.At(Ui.TopLeft, new Vector2(0f, 0f), new Vector2(300f, 26f));

            value = Ui.Label("Value", group, "0", 24f, Ui.Ink, TextAlignmentOptions.Right, Kit.FontNumbers);
            value.rectTransform.At(Ui.TopRight, new Vector2(0f, 0f), new Vector2(160f, 26f));

            Slider slider = Ui.Slider("Slider", group, Kit.WideBarFrame, Kit.WideBarFill, Ui.Gold, out _);
            slider.GetComponent<RectTransform>().At(Ui.BottomLeft, new Vector2(0f, 0f), new Vector2(460f, 22f));
            slider.interactable = true;

            return slider;
        }

        /// <summary>Чип ресурса: пилюля с иконкой, крупным значением и мелкой подписью.</summary>
        private static RectTransform Chip(string name, Transform parent, Sprite icon, out TextMeshProUGUI value, out TextMeshProUGUI caption)
        {
            RectTransform chip = Ui.Node(name, parent);
            chip.sizeDelta = new Vector2(126f, 36f);
            chip.Sprite(Kit.Pill, Color.white);

            Image iconImage = Ui.Icon("Icon", chip, icon, new Vector2(18f, 18f));
            iconImage.rectTransform.At(Ui.Left, new Vector2(8f, 0f), new Vector2(18f, 18f));

            value = Ui.Label("Value", chip, "0", 20f, Ui.Ink, TextAlignmentOptions.Left, Kit.FontNumbers);
            value.rectTransform.At(Ui.Left, new Vector2(31f, 3f), new Vector2(74f, 22f));

            caption = Ui.Label("Caption", chip, "", 12f, Ui.InkMuted, TextAlignmentOptions.Left);
            caption.rectTransform.At(Ui.Left, new Vector2(31f, -10f), new Vector2(88f, 14f));

            return chip;
        }

        #endregion
    }
}
