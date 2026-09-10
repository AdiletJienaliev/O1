using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warlord.Gameplay.Heroes;
using Warlord.Gameplay.Units;
using Warlord.Presentation;
using Warlord.Presentation.Vfx;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Подключает боевые эффекты. В проекте лежит FX Mega Pack на 129 префабов, и до этого
    /// на него не ссылалась ни одна строчка кода: бой шёл вообще без обратной связи —
    /// удар не отличался от промаха, а смерть юнита от его исчезновения.
    ///
    /// Берём «мультяшное» семейство пака: оно совпадает с низкополигональным стилем арены,
    /// а реалистичная кровь и искры рядом с этими моделями смотрелись бы чужеродно.
    /// </summary>
    public static class WarlordCombatVfx
    {
        private const string FxFolder = "Assets/_ExternalAssets/FX Mega Pack/Prefabs";
        private const string LibraryPath = "Assets/_InternalAssets/Configs/VfxLibrary.asset";
        private const string PrefabFolder = "Assets/_InternalAssets/Prefabs";

        [MenuItem("Warlord/Настройка/16. Боевые эффекты", priority = 15)]
        public static void Build()
        {
            var report = new List<string>();

            VfxLibrary library = EnsureLibrary(report);
            AttachToPrefabs(library, report);
            AttachToCapturePoints(library, report);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("Warlord: боевые эффекты подключены\n" + string.Join("\n", report));
        }

        private static VfxLibrary EnsureLibrary(List<string> report)
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);

            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                report.Add("создана библиотека " + LibraryPath);
            }

            library.meleeHit = Fx("Toon Bonk 01 PS", report);
            library.swing = Fx("_u1 Toon Slash 01 PS", report);
            library.death = Fx("Toon Poof 01 PS", report);
            library.captureBurst = Fx("Toon Spark 02 PS", report);

            EditorUtility.SetDirty(library);

            return library;
        }

        /// <summary>
        /// Ищем по имени во всём паке, а не по прямому пути: часть эффектов лежит
        /// в подпапках обновлений («_Update 1»), и жёсткий путь их не находит.
        /// </summary>
        private static GameObject Fx(string name, List<string> report)
        {
            foreach (string guid in AssetDatabase.FindAssets("\"" + name + "\" t:Prefab", new[] { FxFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(path) != name)
                    continue;

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            report.Add("НЕ НАЙДЕН эффект: " + name);
            return null;
        }

        /// <summary>Вешает компонент на всё, у чего есть юнит или полководец.</summary>
        private static void AttachToPrefabs(VfxLibrary library, List<string> report)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (asset == null)
                    continue;

                bool combatant = asset.GetComponentInChildren<UnitEntity>(true) != null
                                 || asset.GetComponentInChildren<HeroController>(true) != null;

                if (!combatant)
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    if (root.GetComponentInChildren<CombatVfx>(true) == null)
                    {
                        CombatVfx vfx = root.AddComponent<CombatVfx>();
                        Bind(vfx, library);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        report.Add("эффекты добавлены: " + Path.GetFileNameWithoutExtension(path));
                    }
                    else
                    {
                        // Компонент уже стоял — обновляем только ссылку на библиотеку.
                        Bind(root.GetComponentInChildren<CombatVfx>(true), library);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        report.Add("библиотека обновлена: " + Path.GetFileNameWithoutExtension(path));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void AttachToCapturePoints(VfxLibrary library, List<string> report)
        {
            var views = Object.FindObjectsByType<CapturePointView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (CapturePointView view in views)
            {
                var serialized = new SerializedObject(view);
                serialized.FindProperty("vfx").objectReferenceValue = library;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }

            report.Add("точек захвата с эффектом захвата: " + views.Length);
        }

        private static void Bind(CombatVfx vfx, VfxLibrary library)
        {
            var serialized = new SerializedObject(vfx);
            serialized.FindProperty("library").objectReferenceValue = library;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
