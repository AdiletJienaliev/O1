using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Warlord.Configs;
using Warlord.Gameplay.Units;
using Warlord.Presentation.Combat;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Настоящие стрелы у стрелков: префаб снаряда из меша пака, эмиттер на префабах
    /// дальнобойных юнитов и ссылка на снаряд в их конфигах.
    ///
    /// Снаряд сознательно не сетевой объект — он целиком презентация, а урон и время полёта
    /// считает сервер. Поэтому в списке спавнимых префабов FishNet ему делать нечего.
    /// </summary>
    public static class WarlordArrowSetup
    {
        private const string ArrowMeshPath = "Assets/ModularRPGHeroesPolyArt/Mesh/Arrow01.fbx";
        private const string EffectsFolder = "Assets/_InternalAssets/Prefabs/Effects";
        private const string ArrowPrefabPath = EffectsFolder + "/Projectile_Arrow.prefab";
        private const string UnitPrefabFolder = "Assets/_InternalAssets/Prefabs/Units";

        [MenuItem("Warlord/Настройка/9. Стрелы у стрелков", priority = 8)]
        public static void BuildArrows()
        {
            List<string> report = new();

            GameObject arrow = EnsureArrowPrefab(report);

            if (arrow == null)
            {
                Debug.LogError("Warlord: префаб стрелы собрать не удалось — " + ArrowMeshPath + " не найден");
                return;
            }

            AssignToRangedUnits(arrow, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (report.Count == 0)
            {
                Debug.Log("Warlord: стрелы уже настроены.");
                return;
            }

            Debug.Log("Warlord: стрелы настроены:\n" + string.Join("\n", report));
        }

        private static GameObject EnsureArrowPrefab(List<string> report)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            if (existing != null)
                return existing;

            GameObject mesh = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowMeshPath);
            if (mesh == null)
                return null;

            if (!AssetDatabase.IsValidFolder(EffectsFolder))
            {
                Directory.CreateDirectory(EffectsFolder);
                AssetDatabase.Refresh();
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(mesh);
            instance.name = "Projectile_Arrow";

            // Коллайдеры снаряду не нужны: попадание уже решено сервером, а физическое тело
            // в полёте только цеплялось бы за юнитов и сбивало им навигацию.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            instance.AddComponent<ArrowProjectileView>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, ArrowPrefabPath);
            Object.DestroyImmediate(instance);

            report.Add("создан " + ArrowPrefabPath + " (если стрела летит боком — поправьте modelEulerOffset)");
            return saved;
        }

        private static void AssignToRangedUnits(GameObject arrow, List<string> report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitPrefabFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);

                if (!name.StartsWith("Unit_", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                if (!contents.TryGetComponent(out UnitEntity unit))
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }

                UnitConfig config = ReadConfig(unit);

                if (config == null || !config.isRanged)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }

                bool changed = false;

                if (!contents.TryGetComponent(out ProjectileEmitter _))
                {
                    contents.AddComponent<ProjectileEmitter>();
                    report.Add(name + ": добавлен ProjectileEmitter");
                    changed = true;
                }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(contents, path);

                PrefabUtility.UnloadPrefabContents(contents);

                if (config.projectilePrefab == arrow)
                    continue;

                config.projectilePrefab = arrow;
                EditorUtility.SetDirty(config);
                report.Add(config.displayName + ": снаряд → " + arrow.name);
            }
        }

        /// <summary>Конфиг с префаба юнита. Поле приватное, поэтому читаем его так же, как редактор.</summary>
        private static UnitConfig ReadConfig(UnitEntity unit)
        {
            SerializedObject serialized = new(unit);
            SerializedProperty property = serialized.FindProperty("config");

            return property != null ? property.objectReferenceValue as UnitConfig : null;
        }
    }
}
