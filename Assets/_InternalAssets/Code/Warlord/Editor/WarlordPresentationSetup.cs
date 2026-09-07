using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Warlord.EditorTools.UI;
using Warlord.Presentation;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Довешивает на префабы полководца и юнитов презентацию, которую иначе пришлось бы
    /// перетаскивать по инспекторам для каждого типа: раскраску в цвет команды и рабочую
    /// полоску здоровья над головой. Идемпотентно — повторный запуск ничего не дублирует.
    /// </summary>
    public static class WarlordPresentationSetup
    {
        private const string PrefabFolder = "Assets/_InternalAssets/Prefabs/Units";

        [MenuItem("Warlord/Настройка/7. Цвета команд и полоски здоровья на префабах", priority = 6)]
        public static void BuildPresentation()
        {
            List<string> report = new();

            foreach (string path in CollectPrefabs())
                Process(path, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (report.Count == 0)
            {
                Debug.Log("Warlord: цвета и полоски здоровья уже настроены.");
                return;
            }

            Debug.Log("Warlord: презентация настроена:\n" + string.Join("\n", report));
        }

        private static IEnumerable<string> CollectPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);

                // PlayerState — чистое состояние без модели, красить и мерить нечего.
                if (name == "Player" || name.StartsWith("Unit_", System.StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }

        private static void Process(string path, List<string> report)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
                return;

            bool changed = false;
            string name = Path.GetFileNameWithoutExtension(path);

            if (!contents.TryGetComponent(out TeamColorApplier _))
            {
                contents.AddComponent<TeamColorApplier>();
                report.Add(name + ": добавлен TeamColorApplier");
                changed = true;
            }

            changed |= SetupHealthBar(contents, name, report);

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(contents, path);

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>
        /// Полоска здоровья. Компонент вешается на сам Canvas — его же он и разворачивает
        /// к камере, а прячет вложенную полосу. Гасить Canvas нельзя: вместе с ним
        /// выключился бы и сам компонент, и обратно полоска уже не вернулась бы.
        /// </summary>
        private static bool SetupHealthBar(GameObject contents, string prefabName, List<string> report)
        {
            Image fill = FindFill(contents);

            if (fill == null)
            {
                report.Add(prefabName + ": заливка полоски не найдена — назначьте её вручную");
                return false;
            }

            Canvas canvas = fill.GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                report.Add(prefabName + ": полоска лежит вне Canvas — пропущена");
                return false;
            }

            if (canvas.TryGetComponent(out WorldHealthBarView _))
                return false;

            WorldHealthBarView view = canvas.gameObject.AddComponent<WorldHealthBarView>();

            using (Bind bind = new(view))
            {
                bind.Ref("fill", fill)
                    .Ref("root", TopmostUnder(canvas.transform, fill.transform));
            }

            report.Add(prefabName + ": добавлена WorldHealthBarView (заливка " + fill.name + ")");
            return true;
        }

        /// <summary>Заливка полосы: ищем Image на объекте с именем Fill, иначе самый глубокий Image.</summary>
        private static Image FindFill(GameObject contents)
        {
            Image[] images = contents.GetComponentsInChildren<Image>(true);
            Image deepest = null;
            int deepestDepth = -1;

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                if (image.name.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && image.name.IndexOf("Area", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return image;
                }

                int depth = Depth(image.transform);
                if (depth > deepestDepth)
                {
                    deepest = image;
                    deepestDepth = depth;
                }
            }

            return deepest;
        }

        /// <summary>Предок объекта, лежащий непосредственно под Canvas: его и прячем целиком.</summary>
        private static GameObject TopmostUnder(Transform canvas, Transform child)
        {
            Transform current = child;

            while (current != null && current.parent != null && current.parent != canvas)
                current = current.parent;

            return current != null ? current.gameObject : child.gameObject;
        }

        private static int Depth(Transform transform)
        {
            int depth = 0;

            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }
    }
}
