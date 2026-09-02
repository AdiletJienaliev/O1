using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FishNet.Component.Transforming;
using FishNet.Object;
using Warlord.Configs;
using Warlord.EditorTools.UI;
using Warlord.Presentation;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Разовые операции настройки проекта. Всё, что иначе делается перетаскиванием ассетов
    /// по инспектору и потому забывается при следующем клоне репозитория.
    /// </summary>
    public static class WarlordSetup
    {
        private const string ConfigFolder = "Assets/_InternalAssets/Configs";
        private const string FlowAssetPath = ConfigFolder + "/GameFlowConfig.asset";
        private const string HeroPrefabPath = "Assets/_InternalAssets/Prefabs/Units/Player.prefab";

        [MenuItem("Warlord/Настройка/1. Создать GameFlowConfig и связать с GameConfig", priority = 0)]
        public static void CreateFlowConfig()
        {
            GameConfig game = LoadSingle<GameConfig>();
            if (game == null)
                return;

            GameFlowConfig flow = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(FlowAssetPath);

            if (flow == null)
            {
                flow = ScriptableObject.CreateInstance<GameFlowConfig>();
                Directory.CreateDirectory(ConfigFolder);
                AssetDatabase.CreateAsset(flow, FlowAssetPath);
                Debug.Log("Warlord: создан " + FlowAssetPath, flow);
            }

            using (Bind bind = new(game))
                bind.Ref("flow", flow);

            EditorUtility.SetDirty(game);
            AssetDatabase.SaveAssets();

            Selection.activeObject = flow;
            EditorGUIUtility.PingObject(flow);
            Debug.Log("Warlord: GameFlowConfig связан с GameConfig. Галочка skipLobby — там.", flow);
        }

        [MenuItem("Warlord/Настройка/2. Починить ссылки конфигов", priority = 1)]
        public static void RepairConfigLinks()
        {
            GameConfig game = LoadSingle<GameConfig>();
            if (game == null)
                return;

            List<string> report = new();

            RepairMap(game, report);
            RepairUnitPrefabs(report);

            AssetDatabase.SaveAssets();

            if (report.Count == 0)
            {
                Debug.Log("Warlord: ссылки конфигов в порядке, чинить нечего.", game);
                return;
            }

            Debug.Log("Warlord: починено ссылок — " + report.Count + ":\n" + string.Join("\n", report), game);
        }

        [MenuItem("Warlord/Настройка/3. Поставить орбитальную камеру в сцену", priority = 2)]
        public static void SetupCamera()
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                GameObject go = new("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
                Debug.Log("Warlord: в сцене не было камеры с тегом MainCamera — создана новая.", go);
            }

            if (camera.GetComponent<HeroOrbitCamera>() == null)
            {
                Undo.AddComponent<HeroOrbitCamera>(camera.gameObject);
                Debug.Log("Warlord: на камеру сцены добавлена HeroOrbitCamera.", camera);
            }
            else
            {
                Debug.Log("Warlord: HeroOrbitCamera уже стоит на камере сцены.", camera);
            }

            DisableLegacyFollowers(camera.gameObject);
            DisableHeroPrefabCamera();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeObject = camera.gameObject;
        }

        /// <summary>
        /// Гасит старые скрипты слежения на той же камере. Тип берём по имени: они лежат
        /// в Assembly-CSharp, на который сборка редактора Warlord сослаться не может.
        /// </summary>
        private static void DisableLegacyFollowers(GameObject cameraObject)
        {
            MonoBehaviour[] components = cameraObject.GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour component in components)
            {
                if (component == null || component is HeroOrbitCamera)
                    continue;

                string name = component.GetType().Name;
                if (name != "MainCameraController" && name != "MovementControll")
                    continue;

                Undo.RecordObject(component, "Disable legacy camera script");
                component.enabled = false;
                Debug.Log("Warlord: выключен старый скрипт камеры " + name + " — им управляет HeroOrbitCamera.", component);
            }
        }

        [MenuItem("Warlord/Настройка/4. Настроить предсказание полководца", priority = 3)]
        public static void ConfigureHeroPrediction()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(HeroPrefabPath);

            if (contents == null)
            {
                Debug.LogError("Warlord: не найден префаб полководца " + HeroPrefabPath);
                return;
            }

            List<string> report = new();

            NetworkObject networkObject = contents.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.LogError("Warlord: на префабе полководца нет NetworkObject");
                return;
            }

            EnablePrediction(networkObject, contents, report);
            RemoveNetworkTransform(contents, report);

            if (report.Count == 0)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.Log("Warlord: предсказание полководца уже настроено.");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, HeroPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();

            Debug.Log("Warlord: префаб полководца исправлен:\n" + string.Join("\n", report),
                AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath));
        }

        /// <summary>
        /// HeroController построен на Replicate/Reconcile, но у NetworkObject предсказание
        /// было выключено: сглаживания не было, и каждая реконсиляция дёргала трансформ.
        /// </summary>
        private static void EnablePrediction(NetworkObject networkObject, GameObject root, List<string> report)
        {
            SerializedObject serialized = new(networkObject);

            SerializedProperty enabled = serialized.FindProperty("_enablePrediction");
            SerializedProperty graphical = serialized.FindProperty("_graphicalObject");

            if (enabled != null && !enabled.boolValue)
            {
                enabled.boolValue = true;
                report.Add("NetworkObject.EnablePrediction = true");
            }

            if (graphical != null && graphical.objectReferenceValue == null)
            {
                Transform visual = FindGraphicalRoot(root);

                if (visual != null)
                {
                    graphical.objectReferenceValue = visual;
                    report.Add("NetworkObject.GraphicalObject = " + visual.name);
                }
                else
                {
                    report.Add("графический объект не найден — назначьте вручную для сглаживания");
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// NetworkTransform на предсказанном полководце дублирует синхронизацию и с
        /// включёнными clientAuthoritative + sendToOwner тянет трансформ владельца назад,
        /// перебивая CharacterController. Состояния и так расходятся через state forwarding.
        /// </summary>
        private static void RemoveNetworkTransform(GameObject root, List<string> report)
        {
            NetworkTransform transform = root.GetComponent<NetworkTransform>();

            if (transform == null)
                return;

            Object.DestroyImmediate(transform, true);
            report.Add("удалён NetworkTransform — он дрался с предсказанием за трансформ");
        }

        /// <summary>Первый потомок с рендерером: его сглаживает предсказание, за ним же едет камера.</summary>
        private static Transform FindGraphicalRoot(GameObject root)
        {
            Renderer renderer = root.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.transform : null;
        }

        /// <summary>
        /// Камера внутри префаба полководца спавнится у каждого игрока и перекрывает сцену.
        /// Орбитальная камера живёт в сцене одна, поэтому встроенную гасим.
        /// </summary>
        private static void DisableHeroPrefabCamera()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);

            if (prefab == null)
            {
                Debug.LogWarning("Warlord: не найден префаб полководца " + HeroPrefabPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(HeroPrefabPath);
            Camera inner = contents.GetComponentInChildren<Camera>(true);

            if (inner == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            if (!inner.gameObject.activeSelf)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.Log("Warlord: встроенная камера полководца уже выключена.");
                return;
            }

            inner.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(contents, HeroPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            Debug.Log("Warlord: камера внутри Player.prefab выключена — снимает сцену только орбитальная.", prefab);
        }

        private static void RepairMap(GameConfig game, List<string> report)
        {
            if (game.Map != null)
                return;

            MapConfig map = LoadSingle<MapConfig>(silent: true);

            if (map == null)
            {
                report.Add("MapConfig в проекте не найден — карту назначьте вручную");
                return;
            }

            using (Bind bind = new(game))
                bind.Ref("map", map);

            EditorUtility.SetDirty(game);
            report.Add("GameConfig.map = " + map.name);
        }

        private static void RepairUnitPrefabs(List<string> report)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(UnitConfig));

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnitConfig unit = AssetDatabase.LoadAssetAtPath<UnitConfig>(path);

                if (unit == null || unit.prefab != null)
                    continue;

                GameObject prefab = FindUnitPrefab(unit.unitId);

                if (prefab == null)
                {
                    report.Add(unit.name + ": префаб по id \"" + unit.unitId + "\" не найден");
                    continue;
                }

                unit.prefab = prefab;
                EditorUtility.SetDirty(unit);
                report.Add(unit.name + ".prefab = " + prefab.name);
            }
        }

        /// <summary>Ищет префаб юнита по строковому id: Unit_SwordsMan -> swordsman.</summary>
        private static GameObject FindUnitPrefab(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:Prefab Unit_");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);

                if (!name.StartsWith("Unit_", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(name.Substring(5), unitId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static T LoadSingle<T>(bool silent = false) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            if (guids.Length == 0)
            {
                if (!silent)
                    Debug.LogError("Warlord: в проекте не найден ассет " + typeof(T).Name);

                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
