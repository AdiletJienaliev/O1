using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Upgrades;
using Warlord.Core;
using Warlord.EditorTools.UI;
using Warlord.Gameplay.Units;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Контент гарнизона и аванпостов по ГДД §1 и §2: конфиг охранника с префабом,
    /// параметры аванпоста, три улучшения и их набор.
    ///
    /// Все числа взяты из ГДД дословно и живут в одном месте, а не расползаются по инспекторам:
    /// баланс правится в документе, потом здесь, потом жмётся пункт меню. Операция идемпотентна —
    /// повторный запуск приводит ассеты к тем же значениям, а не плодит дубли.
    /// </summary>
    public static class WarlordGarrisonSetup
    {
        private const string ConfigFolder = "Assets/_InternalAssets/Configs";
        private const string PrefabFolder = "Assets/_InternalAssets/Prefabs/Units";

        private const string GuardConfigPath = ConfigFolder + "/Unit_GuardSpear.asset";
        private const string GuardPrefabPath = PrefabFolder + "/Unit_GuardSpear.prefab";
        private const string SourcePrefabPath = PrefabFolder + "/Unit_SpearMan.prefab";

        private const string OutpostConfigPath = ConfigFolder + "/CapturePoint_Outpost.asset";
        private const string UpgradeSetPath = ConfigFolder + "/OutpostUpgradeSetConfig.asset";

        [MenuItem("Warlord/Настройка/11. Охранник: конфиг, префаб и место в ростере", priority = 10)]
        public static void BuildGuard()
        {
            List<string> report = new();

            UnitConfig guard = EnsureGuardConfig(report);
            AppendToRoster(guard, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Warlord: охранник собран:\n" + string.Join("\n", report)
                      + "\nДальше запустите пункт «8. Модели, анимации и щиты юнитов» — он поставит "
                      + "охраннику модель SwordShield04 и соберёт ему контроллер аниматора.", guard);
        }

        [MenuItem("Warlord/Настройка/12. Аванпосты: точка захвата и три улучшения", priority = 11)]
        public static void BuildOutposts()
        {
            List<string> report = new();

            CapturePointConfig outpost = EnsureOutpostConfig(report);
            OutpostUpgradeSetConfig set = EnsureUpgradeSet(report);

            TuneCentralFlag(report);
            TuneBaseFlag(report);
            LinkToGameConfig(outpost, set, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Warlord: аванпосты собраны:\n" + string.Join("\n", report), outpost);
        }

        #region Охранник

        /// <summary>Охранник-копейщик (ГДД §1.2). Числа — дословно из документа.</summary>
        private static UnitConfig EnsureGuardConfig(List<string> report)
        {
            UnitConfig guard = AssetDatabase.LoadAssetAtPath<UnitConfig>(GuardConfigPath);

            if (guard == null)
            {
                guard = ScriptableObject.CreateInstance<UnitConfig>();
                AssetDatabase.CreateAsset(guard, GuardConfigPath);
                report.Add("создан " + GuardConfigPath);
            }

            guard.unitId = "guard_spear";
            guard.displayName = "Охранник-копейщик";

            guard.cost = 70;
            guard.spawnTime = 3f;

            guard.maxHealth = 140;
            guard.armor = 3;
            guard.damagePerHit = 12;
            guard.attackInterval = 1f;
            guard.attackRange = 3.2f;

            // Медленный намеренно (ГДД §1.2): охранник, способный догнать полководца,
            // перестал бы быть гарнизоном и стал бы дешёвым полевым юнитом.
            guard.moveSpeed = 3f;
            guard.turnSpeed = 360f;
            guard.aggroRadiusOverride = 10f;

            // В построениях не участвует, поэтому приоритет слота ничего не значит.
            guard.formationSlotPriority = 0;

            guard.isGarrison = true;
            guard.garrisonLeash = 8f;
            guard.garrisonReturnSpeed = 1.3f;
            guard.garrisonRegenPerSecond = 3f;
            guard.garrisonRegenCombatDelay = 5f;
            guard.maxPerPoint = 6;

            guard.isRanged = false;
            guard.projectilePrefab = null;

            guard.prefab = EnsureGuardPrefab(guard, report);

            EditorUtility.SetDirty(guard);
            return guard;
        }

        /// <summary>
        /// Префаб копируется с копейщика: там уже настроены NetworkObject, UnitEntity,
        /// синхронизация трансформа, агент навигации и полоска здоровья. Собирать это
        /// заново значит гарантированно забыть один из компонентов, а забытый
        /// <c>UnitTransformSync</c> выглядит как «охранник не двигается у клиентов».
        /// </summary>
        private static GameObject EnsureGuardPrefab(UnitConfig guard, List<string> report)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GuardPrefabPath);

            if (existing == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
                {
                    report.Add("префаб копейщика не найден по пути " + SourcePrefabPath
                               + " — префаб охранника соберите вручную");
                    return null;
                }

                if (!AssetDatabase.CopyAsset(SourcePrefabPath, GuardPrefabPath))
                {
                    report.Add("не удалось скопировать префаб копейщика в " + GuardPrefabPath);
                    return null;
                }

                AssetDatabase.ImportAsset(GuardPrefabPath);
                report.Add("создан " + GuardPrefabPath + " (копия копейщика)");
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(GuardPrefabPath);

            try
            {
                contents.name = "Unit_GuardSpear";

                // Конфиг на префабе задаёт запасную скорость анимации и радиус тела до того,
                // как сервер пришлёт статы, поэтому его обязательно перевесить на охранника.
                if (contents.TryGetComponent(out UnitEntity unit))
                {
                    using Bind bind = new(unit);
                    bind.Ref("config", guard);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, GuardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(GuardPrefabPath);
        }

        private static void AppendToRoster(UnitConfig guard, List<string> report)
        {
            UnitRosterConfig roster = LoadSingle<UnitRosterConfig>();

            if (roster == null || guard == null || roster.IndexOf(guard) >= 0)
                return;

            SerializedObject serialized = new(roster);
            SerializedProperty list = serialized.FindProperty("availableUnits");

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = guard;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(roster);

            report.Add("охранник добавлен в ростер под индексом " + (list.arraySize - 1)
                       + " — в панель покупки юнитов он не попадёт, его берут в панели гарнизона");
        }

        #endregion

        #region Аванпост

        /// <summary>Аванпост (ГДД §2.4). Ключевое отличие от центра — откат без гарнизона.</summary>
        private static CapturePointConfig EnsureOutpostConfig(List<string> report)
        {
            CapturePointConfig outpost = AssetDatabase.LoadAssetAtPath<CapturePointConfig>(OutpostConfigPath);

            if (outpost == null)
            {
                outpost = ScriptableObject.CreateInstance<CapturePointConfig>();
                AssetDatabase.CreateAsset(outpost, OutpostConfigPath);
                report.Add("создан " + OutpostConfigPath);
            }

            outpost.pointId = "outpost";
            outpost.displayName = "Аванпост";

            outpost.captureRadius = 5f;
            outpost.captureRatePerSecond = 0.1f;
            outpost.decaptureRatePerSecond = 0.12f;
            outpost.decayRatePerSecond = 0.03f;

            outpost.contestedByHeroes = true;
            outpost.contestedByUnits = false;
            outpost.stackMultiplierPerExtraPlayer = 0f;
            outpost.ownerBarRecoversWhenEmpty = false;

            outpost.garrisonDecay = true;
            outpost.garrisonDecayRatePerSecond = 0.03f;
            outpost.garrisonCheckRadius = 12f;
            outpost.maxGuards = 6;
            outpost.garrisonRingRadius = 4.5f;

            outpost.hasUpgradeSlot = true;
            outpost.allowsRallyPoint = true;

            outpost.rewardType = CaptureRewardType.Continuous;
            outpost.startsOwnedByZoneOwner = false;

            EditorUtility.SetDirty(outpost);
            return outpost;
        }

        /// <summary>Центр гарнизон держать умеет, но без него не откатывается (ГДД §2.3).</summary>
        private static void TuneCentralFlag(List<string> report)
        {
            CapturePointConfig center = AssetDatabase.LoadAssetAtPath<CapturePointConfig>(
                ConfigFolder + "/CapturePoint_CenterFlag.asset");

            if (center == null)
                return;

            center.garrisonDecay = false;
            center.maxGuards = 6;
            center.garrisonRingRadius = 4.5f;
            center.hasUpgradeSlot = false;
            center.allowsRallyPoint = false;

            EditorUtility.SetDirty(center);
            report.Add("центральный флаг: гарнизон до 6, откат без гарнизона выключен (ГДД §2.3)");
        }

        /// <summary>
        /// Флаг базы тоже держит гарнизон, только меньший. Это единственная точка, которая
        /// есть у игрока с первой секунды матча: без гарнизона на ней охранников было бы
        /// некуда ставить, пока не захвачен первый аванпост.
        ///
        /// Улучшения и точка сбора базе не нужны — она и так дом.
        /// </summary>
        private static void TuneBaseFlag(List<string> report)
        {
            CapturePointConfig baseFlag = AssetDatabase.LoadAssetAtPath<CapturePointConfig>(
                ConfigFolder + "/CapturePoint_BaseFlag.asset");

            if (baseFlag == null)
                return;

            baseFlag.garrisonDecay = false;
            baseFlag.maxGuards = 4;

            // Радиус зоны базы восемь метров, кольцо ставим шире её, чтобы охранники
            // стояли на подходах, а не толпились вокруг самого флага.
            baseFlag.garrisonRingRadius = 6f;
            baseFlag.hasUpgradeSlot = false;
            baseFlag.allowsRallyPoint = false;

            EditorUtility.SetDirty(baseFlag);
            report.Add("флаг базы: гарнизон до 4 на кольце 6 м");
        }

        #endregion

        #region Улучшения

        private static OutpostUpgradeSetConfig EnsureUpgradeSet(List<string> report)
        {
            OutpostUpgradeConfig supply = EnsureUpgrade(OutpostUpgradeType.Supply, report, upgrade =>
            {
                upgrade.displayName = "Снабжение";
                upgrade.description = "+5 золота/с, +3 к лимиту армии";

                // Базовый выбор: и деньги, и место под гарнизон. Именно он ставится
                // по умолчанию, если игрок не успел выбрать в бою.
                upgrade.goldPerSecond = 5f;
                upgrade.unitCapBonus = 3;

                upgrade.spawnTimeReduction = 0f;
                upgrade.healPerSecond = 0f;
                upgrade.guardCostReduction = 0f;
                upgrade.guardHealthBonus = 0f;
            });

            OutpostUpgradeConfig forge = EnsureUpgrade(OutpostUpgradeType.Forge, report, upgrade =>
            {
                upgrade.displayName = "Кузница";
                upgrade.description = "−25 % ко времени спавна, лечение 4 HP/с в радиусе 15 м";

                upgrade.goldPerSecond = 0f;
                upgrade.unitCapBonus = 0;
                upgrade.spawnTimeReduction = 0.25f;
                upgrade.healPerSecond = 4f;
                upgrade.healRadius = 15f;
                upgrade.guardCostReduction = 0f;
                upgrade.guardHealthBonus = 0f;
            });

            OutpostUpgradeConfig mercenaries = EnsureUpgrade(OutpostUpgradeType.Mercenaries, report, upgrade =>
            {
                upgrade.displayName = "Наёмники";
                upgrade.description = "Охранники на этой точке −30 % цены и +30 % HP";

                upgrade.goldPerSecond = 0f;
                upgrade.unitCapBonus = 0;
                upgrade.spawnTimeReduction = 0f;
                upgrade.healPerSecond = 0f;
                upgrade.guardCostReduction = 0.3f;
                upgrade.guardHealthBonus = 0.3f;

                // Уникальный юнит из ГДД §2.5 остаётся незаполненным: своего ростерного
                // наёмника в проекте пока нет, а подставлять сюда обычного мечника значит
                // сделать улучшение бессмысленным и при этом выглядящим рабочим.
            });

            OutpostUpgradeSetConfig set = AssetDatabase.LoadAssetAtPath<OutpostUpgradeSetConfig>(UpgradeSetPath);

            if (set == null)
            {
                set = ScriptableObject.CreateInstance<OutpostUpgradeSetConfig>();
                AssetDatabase.CreateAsset(set, UpgradeSetPath);
                report.Add("создан " + UpgradeSetPath);
            }

            SerializedObject serialized = new(set);
            SerializedProperty list = serialized.FindProperty("upgrades");

            list.arraySize = 3;
            list.GetArrayElementAtIndex(0).objectReferenceValue = supply;
            list.GetArrayElementAtIndex(1).objectReferenceValue = forge;
            list.GetArrayElementAtIndex(2).objectReferenceValue = mercenaries;

            serialized.FindProperty("defaultIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(set);
            return set;
        }

        private static OutpostUpgradeConfig EnsureUpgrade(
            OutpostUpgradeType type,
            List<string> report,
            System.Action<OutpostUpgradeConfig> configure)
        {
            string path = ConfigFolder + "/OutpostUpgrade_" + type + ".asset";
            OutpostUpgradeConfig upgrade = AssetDatabase.LoadAssetAtPath<OutpostUpgradeConfig>(path);

            if (upgrade == null)
            {
                upgrade = ScriptableObject.CreateInstance<OutpostUpgradeConfig>();
                AssetDatabase.CreateAsset(upgrade, path);
                report.Add("создан " + path);
            }

            upgrade.type = type;
            configure(upgrade);

            EditorUtility.SetDirty(upgrade);
            return upgrade;
        }

        #endregion

        private static void LinkToGameConfig(
            CapturePointConfig outpost,
            OutpostUpgradeSetConfig set,
            List<string> report)
        {
            GameConfig game = LoadSingle<GameConfig>();

            if (game == null)
                return;

            using (Bind bind = new(game))
            {
                bind.Ref("outpost", outpost)
                    .Ref("outpostUpgrades", set);
            }

            EditorUtility.SetDirty(game);
            report.Add("аванпост и набор улучшений связаны с GameConfig");
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
