using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Сборка интерфейса Warlord из набора GUI PRO Kit — Simple Casual.
    /// Префабы генерируются кодом, а не руками: раскладка описана один раз и повторяется
    /// один в один после любой правки набора, конфигов или самих виджетов.
    /// </summary>
    public static partial class WarlordUiBuilder
    {
        private const string OutputFolder = "Assets/_InternalAssets/Prefabs/UI";
        private const string RootPrefabPath = OutputFolder + "/UI_Root.prefab";

        /// <summary>Ссылки на сохранённые шаблоны — из них собирается корневой префаб.</summary>
        private struct Templates
        {
            public GameObject UnitCard;
            public GameObject SpawnQueueSlot;
            public GameObject UpgradeBranch;
            public GameObject FlagHoldRow;
            public GameObject OrderButton;
            public GameObject LobbySlot;
            public GameObject MatchResultRow;
            public GameObject MinimapMarker;
            public GameObject ArmyPresetCell;
            public GameObject GarrisonRow;
        }

        [MenuItem("Warlord/UI/1. Собрать префабы интерфейса", priority = 0)]
        public static void BuildAll()
        {
            EnsureFolder();

            Templates templates = new()
            {
                UnitCard = Save(UiTemplates.UnitCard(), "UI_UnitCard"),
                SpawnQueueSlot = Save(UiTemplates.SpawnQueueSlot(), "UI_SpawnQueueSlot"),
                UpgradeBranch = Save(UiTemplates.UpgradeBranch(), "UI_UpgradeBranch"),
                FlagHoldRow = Save(UiTemplates.FlagHoldRow(), "UI_FlagHoldRow"),
                OrderButton = Save(UiTemplates.OrderButton(), "UI_OrderButton"),
                LobbySlot = Save(UiTemplates.LobbySlot(), "UI_LobbySlot"),
                MatchResultRow = Save(UiTemplates.MatchResultRow(), "UI_MatchResultRow"),
                MinimapMarker = Save(UiTemplates.MinimapMarker(), "UI_MinimapMarker"),
                ArmyPresetCell = Save(UiTemplates.ArmyPresetCell(), "UI_ArmyPresetCell"),
                GarrisonRow = Save(UiTemplates.GarrisonRow(), "UI_GarrisonRow")
            };

            GameObject root = BuildRoot(templates);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, RootPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Kit.FlushMissingReport();

            Debug.Log("Warlord UI: префабы собраны в " + OutputFolder, saved);
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }

        [MenuItem("Warlord/UI/2. Поставить UI в открытую сцену", priority = 1)]
        public static void InstallInScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("Warlord UI: сначала выполните «Собрать префабы интерфейса»");
                return;
            }

            // Второй экземпляр HUD рисовал бы всё поверх первого и ловил бы те же клики.
            Object existing = Object.FindAnyObjectByType<Warlord.UI.WarlordHud>();
            if (existing != null)
            {
                Debug.LogWarning("Warlord UI: интерфейс уже есть в сцене", existing);
                Selection.activeObject = existing;
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Install Warlord UI");

            EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeObject = instance;
            Debug.Log("Warlord UI: интерфейс добавлен в сцену", instance);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject events = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Create EventSystem");
        }

        private static GameObject Save(GameObject instance, string name)
        {
            string path = OutputFolder + "/" + name + ".prefab";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            return prefab;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder))
                return;

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>Экземпляр шаблона внутри корневого префаба: связь с исходным префабом сохраняется.</summary>
        private static T Spawn<T>(GameObject prefab, Transform parent, string name) where T : Component
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        private static Canvas CreateCanvas(GameObject go)
        {
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
