using HeathenEngineering.SteamworksIntegration.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Warlord.EditorTools.UI;
using Warlord.Networking.Steam;
using Warlord.UI.Screens;

namespace Warlord.EditorTools.Steam
{
    /// <summary>
    /// Достраивает экран лобби виджетами Steam: чат комнаты, список участников с аватарами
    /// и выпадающий список друзей для приглашений.
    /// </summary>
    /// <remarks>
    /// Живёт отдельно от <see cref="WarlordUiBuilder"/> и в своей сборке, потому что тот не
    /// должен знать про Steam: без пакетов Heathen интерфейс обязан собираться целиком.
    /// Поэтому пункт запускается после «UI/1» и правит уже готовый префаб, а не заменяет его.
    /// Карточки и сообщения — готовые префабы Heathen: свой вид у них уже настроен, нам нужны
    /// только интерфейсы IChatMessage и IUserProfile на их корнях.
    /// </remarks>
    public static class WarlordSteamUi
    {
        private const string RootPrefabPath = "Assets/_InternalAssets/Prefabs/UI/UI_Root.prefab";
        private const string LobbyScreenName = "Screen_Lobby";

        private const string ChatNode = "SteamChat";
        private const string MembersNode = "SteamMembers";
        private const string InviteNode = "SteamFriendInvite";

        [MenuItem("Warlord/UI/3. Добавить в лобби чат и друзей Steam", priority = 2)]
        public static void Build()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("Warlord Steam UI: сначала выполните «Warlord/UI/1»");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(RootPrefabPath);

            try
            {
                Transform screen = root.transform.Find(LobbyScreenName);

                if (screen == null)
                {
                    Debug.LogError($"Warlord Steam UI: в префабе нет {LobbyScreenName}");
                    return;
                }

                LobbyScreen lobby = screen.GetComponent<LobbyScreen>();
                RectTransform screenRect = (RectTransform)screen;

                // Пункт идемпотентен: старые узлы сносим целиком, а не пытаемся чинить.
                Drop(screenRect, ChatNode);
                Drop(screenRect, MembersNode);
                Drop(screenRect, InviteNode);

                BuildMembers(screenRect, lobby);
                BuildChat(screenRect);
                BuildFriendInvite(screenRect);

                PrefabUtility.SaveAsPrefabAsset(root, RootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            Debug.Log("Warlord Steam UI: чат и список друзей добавлены в лобби. " +
                      "Обновите сцену пунктом «Warlord/UI/2».", saved);

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }

        #region Участники

        private static void BuildMembers(RectTransform screen, LobbyScreen lobby)
        {
            GameObject card = FindHeathenPrefab("Friend Profile");

            if (card == null)
                return;

            RectTransform panel = Ui.Node(MembersNode, screen).At(
                Ui.Left,
                new Vector2(LobbyColumns.MembersOffsetFromLeft, LobbyColumns.CenterY),
                new Vector2(LobbyColumns.MembersWidth, LobbyColumns.Height));
            panel.Sprite(Kit.PanelLarge, Color.white);

            Ui.Label("Title", panel, "В КОМНАТЕ", 22f, Ui.InkMuted, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.At(Ui.Top, new Vector2(0f, -22f), new Vector2(LobbyColumns.MembersWidth - 40f, 28f));

            RectTransform content = Ui.Node("Content", panel)
                .At(Ui.Top, new Vector2(0f, -60f), new Vector2(LobbyColumns.MembersWidth - 32f, LobbyColumns.Height - 80f)).Pivot(Ui.Top);
            VerticalLayoutGroup layout = content.Column(8f, TextAnchor.UpperCenter);

            // Карточка участника — готовый префаб Heathen со своей шириной (заметно шире
            // колонки). Ui.Column по умолчанию ширину детей не трогает, и карточка вылезала
            // за панель, накрывая собой список слотов справа.
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            content.Fit(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize);

            if (!screen.TryGetComponent(out SteamLobbyMemberList list))
                list = screen.gameObject.AddComponent<SteamLobbyMemberList>();

            using (Bind bind = new(list))
            {
                bind.Ref("content", content)
                    .Ref("memberTemplate", card);
            }

            // Простая строка с никами больше не нужна: тот же состав теперь виден карточками.
            if (lobby == null)
                return;

            using (Bind bind = new(lobby))
                bind.Ref("platformMembersLabel", (Object)null);
        }

        #endregion

        #region Чат

        private static void BuildChat(RectTransform screen)
        {
            GameObject mine = FindHeathenPrefab("My Chat Message Template");
            GameObject theirs = FindHeathenPrefab("Their Chat Message Template");

            if (mine == null || theirs == null)
                return;

            RectTransform panel = Ui.Node(ChatNode, screen).At(
                Ui.Right,
                new Vector2(LobbyColumns.ChatOffsetFromRight, LobbyColumns.CenterY),
                new Vector2(LobbyColumns.ChatWidth, LobbyColumns.Height));
            panel.Sprite(Kit.PanelLarge, Color.white);

            Ui.Label("Title", panel, "ЧАТ", 22f, Ui.InkMuted, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.At(Ui.Top, new Vector2(0f, -22f), new Vector2(LobbyColumns.ChatWidth - 40f, 28f));

            RectTransform view = Ui.Node("Scroll", panel)
                .At(Ui.Top, new Vector2(0f, -58f), new Vector2(LobbyColumns.ChatWidth - 32f, LobbyColumns.Height - 150f)).Pivot(Ui.Top);
            ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            RectTransform viewport = Ui.Node("Viewport", view).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Ui.Node("Content", viewport).At(Ui.Top, Vector2.zero, new Vector2(LobbyColumns.ChatWidth - 32f, 0f)).Pivot(Ui.Top);
            content.Column(6f, TextAnchor.UpperLeft);
            content.Fit(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize);

            scroll.viewport = viewport;
            scroll.content = content;

            TMP_InputField input = InputField("Input", panel, "написать...");
            ((RectTransform)input.transform).At(Ui.Bottom, new Vector2(0f, 26f), new Vector2(LobbyColumns.ChatWidth - 32f, 52f));

            if (!screen.TryGetComponent(out SteamLobbyChat chat))
                chat = screen.gameObject.AddComponent<SteamLobbyChat>();

            using (Bind bind = new(chat))
            {
                bind.Ref("root", panel.gameObject)
                    .Ref("input", input)
                    .Ref("scroll", scroll)
                    .Ref("messageRoot", content)
                    .Ref("myMessageTemplate", mine)
                    .Ref("theirMessageTemplate", theirs);
            }
        }

        #endregion

        #region Друзья

        private static void BuildFriendInvite(RectTransform screen)
        {
            GameObject dropDownPrefab = FindHeathenPrefab("Friend Invite Dropdown Menu");

            if (dropDownPrefab == null)
                return;

            // Холдер высокий: кнопка вызова стоит сверху, а список друзей разворачивается
            // под ней. Раньше и кнопка, и список лежали в одном прямоугольнике 340×60 —
            // список рисовался позади кнопки и был обрезан почти целиком.
            RectTransform holder = Ui.Node(InviteNode, screen).At(Ui.TopRight, new Vector2(-30f, -100f), new Vector2(340f, 470f));

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dropDownPrefab, holder);
            instance.name = "FriendInviteDropDown";

            if (instance.transform is RectTransform dropRect)
                dropRect.At(Ui.TopRight, new Vector2(0f, -62f), dropRect.sizeDelta).Pivot(Ui.TopRight);

            FriendInviteDropDown dropDown = instance.GetComponentInChildren<FriendInviteDropDown>(true);

            if (dropDown == null)
            {
                Debug.LogWarning("Warlord Steam UI: в префабе друзей нет FriendInviteDropDown", instance);
                return;
            }

            if (!screen.TryGetComponent(out SteamInviteBinder binder))
                binder = screen.gameObject.AddComponent<SteamInviteBinder>();

            using (Bind bind = new(binder))
                bind.Ref("inviteDropDown", dropDown);

            // У выпадашки нет своей кнопки вызова — её открывает Show().
            Button open = MenuButton("OpenButton", holder, "СПИСОК ДРУЗЕЙ");
            ((RectTransform)open.transform).At(Ui.TopRight, Vector2.zero, new Vector2(340f, 54f)).Pivot(Ui.TopRight);
            UnityEventTools.AddPersistentListener(open.onClick, dropDown.Show);
        }

        #endregion

        #region Детали

        private static void Drop(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);

            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
        }

        /// <summary>
        /// Префабы Heathen ищутся по имени, а не по пути: версия пакета входит в путь
        /// («…/3.6.0/Prefabs/…») и меняется при каждом обновлении.
        /// </summary>
        private static GameObject FindHeathenPrefab(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.Contains("Toolkit for Steamworks SDK"))
                    continue;

                if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                    continue;

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            Debug.LogError($"Warlord Steam UI: не найден префаб Heathen «{name}». " +
                           "Импортируйте образцы пакета Toolkit for Steamworks SDK.");
            return null;
        }

        private static Button MenuButton(string name, Transform parent, string caption)
        {
            Button button = Ui.Button(name, parent, Kit.ButtonNavy, Color.white, out _);

            Ui.Label("Text", button.transform, caption, 22f, Color.white, TextAlignmentOptions.Center, Kit.FontTitle)
                .rectTransform.Stretch();

            return button;
        }

        private static TMP_InputField InputField(string name, Transform parent, string placeholder)
        {
            TMP_DefaultControls.Resources resources = new() { inputField = Kit.InputFrame };
            GameObject go = TMP_DefaultControls.CreateInputField(resources);

            go.name = name;
            go.transform.SetParent(parent, false);

            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 240;

            if (field.placeholder is TextMeshProUGUI hint)
            {
                hint.text = placeholder;
                hint.fontSize = 20f;
                hint.color = new Color(0.5f, 0.54f, 0.62f, 1f);

                if (Kit.FontBody != null)
                    hint.font = Kit.FontBody;
            }

            if (field.textComponent is TextMeshProUGUI content)
            {
                content.fontSize = 20f;
                content.color = new Color(0.12f, 0.14f, 0.2f);

                if (Kit.FontBody != null)
                    content.font = Kit.FontBody;
            }

            return field;
        }

        #endregion
    }
}
