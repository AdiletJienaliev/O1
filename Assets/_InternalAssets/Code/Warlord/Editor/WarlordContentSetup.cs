using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Formations;
using Warlord.EditorTools.UI;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Досоздание контента, который иначе пришлось бы собирать вручную по инспекторам:
    /// новый тип юнита с префабом и записью в ростере, построение-пресет в наборе.
    /// Операции идемпотентны — повторный запуск ничего не портит и не плодит дублей.
    /// </summary>
    public static class WarlordContentSetup
    {
        private const string ConfigFolder = "Assets/_InternalAssets/Configs";
        private const string PrefabFolder = "Assets/_InternalAssets/Prefabs/Units";

        private const string MagePath = ConfigFolder + "/Unit_Mage.asset";
        private const string MagePrefabPath = PrefabFolder + "/Unit_Mage.prefab";
        private const string ArcherPrefabPath = PrefabFolder + "/Unit_Archer.prefab";
        private const string PresetFormationPath = ConfigFolder + "/Formation_Preset.asset";

        [MenuItem("Warlord/Настройка/5. Добавить мага и построение-пресет", priority = 4)]
        public static void BuildContent()
        {
            List<string> report = new();

            CreateMage(report);
            CreatePresetFormation(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (report.Count == 0)
            {
                Debug.Log("Warlord: маг и пресет строя уже на месте.");
                return;
            }

            Debug.Log("Warlord: контент дополнен:\n" + string.Join("\n", report));
        }

        /// <summary>
        /// Маг (ГДД §5.2, расширение): бьёт по площади с большим кулдауном. Префаб копируется
        /// с лучника — у того уже настроены модель, аниматор и сетевые компоненты, а собирать
        /// это заново значит гарантированно что-нибудь забыть.
        /// </summary>
        private static void CreateMage(List<string> report)
        {
            UnitConfig mage = AssetDatabase.LoadAssetAtPath<UnitConfig>(MagePath);

            if (mage == null)
            {
                mage = ScriptableObject.CreateInstance<UnitConfig>();
                AssetDatabase.CreateAsset(mage, MagePath);
                report.Add("создан " + MagePath);
            }

            mage.unitId = "mage";
            mage.displayName = "Маг";

            mage.cost = 140;
            mage.spawnTime = 5f;

            mage.maxHealth = 55;
            mage.damagePerHit = 26;

            // Большой кулдаун — плата за площадь: маг не должен ковровым огнём
            // заменять собой всю остальную армию.
            mage.attackInterval = 3.5f;
            mage.attackRange = 12f;
            mage.armor = 0;

            mage.splashRadius = 3.5f;
            mage.splashDamageFactor = 0.75f;

            mage.moveSpeed = 3.4f;
            mage.turnSpeed = 300f;

            // Позади лучников: маг самый хрупкий в ростере.
            mage.formationSlotPriority = 4;

            mage.isRanged = true;
            mage.projectileSpeed = 16f;

            mage.prefab = EnsureMagePrefab(mage, report);

            EditorUtility.SetDirty(mage);
            AppendToRoster(mage, report);
        }

        private static GameObject EnsureMagePrefab(UnitConfig mage, List<string> report)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MagePrefabPath);
            if (existing != null)
                return existing;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath) == null)
            {
                report.Add("префаб лучника не найден — префаб мага соберите вручную");
                return null;
            }

            if (!AssetDatabase.CopyAsset(ArcherPrefabPath, MagePrefabPath))
            {
                report.Add("не удалось скопировать префаб лучника в " + MagePrefabPath);
                return null;
            }

            AssetDatabase.ImportAsset(MagePrefabPath);

            GameObject contents = PrefabUtility.LoadPrefabContents(MagePrefabPath);
            contents.name = "Unit_Mage";

            // Конфиг на префабе задаёт запасную скорость анимации и радиус тела до того,
            // как сервер пришлёт статы, поэтому его обязательно перевесить на мага.
            if (contents.TryGetComponent(out Warlord.Gameplay.Units.UnitEntity unit))
            {
                using Bind bind = new(unit);
                bind.Ref("config", mage);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, MagePrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            report.Add("создан " + MagePrefabPath + " (копия лучника — модель замените на свою)");
            return AssetDatabase.LoadAssetAtPath<GameObject>(MagePrefabPath);
        }

        private static void AppendToRoster(UnitConfig mage, List<string> report)
        {
            UnitRosterConfig roster = LoadSingle<UnitRosterConfig>();
            if (roster == null || roster.IndexOf(mage) >= 0)
                return;

            SerializedObject serialized = new(roster);
            SerializedProperty list = serialized.FindProperty("availableUnits");

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = mage;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(roster);

            report.Add("маг добавлен в ростер под индексом " + (list.arraySize - 1));
        }

        /// <summary>
        /// Построение-пресет: сам ассет пустой по смыслу, форму задаёт игрок в панели
        /// расстановки. В наборе он нужен, чтобы пресет переключался как обычный строй.
        /// </summary>
        private static void CreatePresetFormation(List<string> report)
        {
            PresetFormationConfig preset = AssetDatabase.LoadAssetAtPath<PresetFormationConfig>(PresetFormationPath);

            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<PresetFormationConfig>();
                AssetDatabase.CreateAsset(preset, PresetFormationPath);
                report.Add("создан " + PresetFormationPath);
            }

            preset.formationId = "preset";
            preset.displayName = "Свой строй";
            preset.slotSpacing = 1.8f;
            EditorUtility.SetDirty(preset);

            FormationSetConfig set = LoadSingle<FormationSetConfig>();
            if (set == null || set.IndexOf(preset) >= 0)
                return;

            SerializedObject serialized = new(set);
            SerializedProperty list = serialized.FindProperty("formations");

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = preset;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);

            report.Add("пресет добавлен в набор построений под индексом " + (list.arraySize - 1));
        }

        private static T LoadSingle<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            if (guids.Length == 0)
            {
                Debug.LogError("Warlord: в проекте не найден ассет " + typeof(T).Name);
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
