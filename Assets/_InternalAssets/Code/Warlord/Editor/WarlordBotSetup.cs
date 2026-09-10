using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Warlord.Configs;
using Warlord.Configs.Bots;
using Warlord.Core;
using Warlord.EditorTools.UI;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Сборка ассетов ботов: пять характеров, четыре сложности и набор, связанный с GameConfig.
    ///
    /// Характеры здесь — стартовый набор, а не закрытый список. Добавить шестой означает
    /// создать ещё один ассет через Create → Warlord → Боты → Характер и дописать его
    /// в набор: ни строчки кода. Числа подобраны так, чтобы противники читались с первого
    /// матча — «этот лезет в центр», «этот шастает по тылам», — а не отличались на третьем знаке.
    ///
    /// Пункт идемпотентен: повторный запуск обновляет уже созданные ассеты, а не плодит копии.
    /// </summary>
    public static class WarlordBotSetup
    {
        private const string ConfigFolder = "Assets/_InternalAssets/Configs";
        private const string BotFolder = ConfigFolder + "/Bots";

        [MenuItem("Warlord/Настройка/16. Боты: характеры, сложности и набор", priority = 15)]
        public static void CreateBots()
        {
            Directory.CreateDirectory(BotFolder);

            List<BotPersonalityConfig> personalities = new()
            {
                Conqueror(),
                Steward(),
                Fox(),
                Warden(),
                Butcher()
            };

            List<BotDifficultyConfig> difficulties = new()
            {
                Easy(),
                Normal(),
                Hard(),
                Brutal()
            };

            BotSetConfig set = LoadOrCreate<BotSetConfig>(BotFolder + "/BotSetConfig.asset");

            using (Bind bind = new(set))
            {
                bind.Refs("personalities", personalities.ToArray())
                    .Refs("difficulties", difficulties.ToArray())
                    .Int("defaultPersonality", 0);
            }

            EditorUtility.SetDirty(set);

            GameConfig game = LoadSingle<GameConfig>();

            if (game != null)
            {
                using (Bind bind = new(game))
                    bind.Ref("bots", set);

                EditorUtility.SetDirty(game);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);

            Debug.Log($"Warlord: собран набор ботов — {personalities.Count} характера(ов), " +
                      $"{difficulties.Count} уровня сложности. Добавлять их в комнату можно в лобби.", set);
        }

        #region Характеры

        /// <summary>Прямой напор: центр, бой, минимум обороны. Самый понятный противник для первого матча.</summary>
        private static BotPersonalityConfig Conqueror()
        {
            BotPersonalityConfig p = LoadOrCreate<BotPersonalityConfig>(BotFolder + "/Bot_Conqueror.asset");

            p.displayName = "Завоеватель";
            p.blurb = "Идёт в центр и давит. Обороняется неохотно, зато бьёт первым.";
            p.namePool = new[] { "Кастор", "Магнус", "Вальд", "Ортан" };

            p.centerWeight = 1.6f;
            p.outpostWeight = 0.7f;
            p.defenseWeight = 0.6f;
            p.raidWeight = 0.4f;
            p.huntWeight = 0.9f;
            p.supportWeight = 0.7f;
            p.economyWeight = 0.7f;

            p.caution = 0.25f;
            p.persistence = 0.6f;
            p.opportunism = 0.4f;
            p.vengeance = 0.3f;
            p.massBeforePush = 0.4f;

            p.rangedShare = 0.3f;
            p.tankPreference = 1.2f;
            p.splashPreference = 1f;
            p.garrisonShare = 0.15f;
            p.upgradeEagerness = 0.7f;
            p.branchPriority = new[] { 1.4f, 1.1f, 0.7f, 1f, 0.8f, 0.6f };

            p.raidMinArmyShare = 0.7f;
            p.raidWithArmy = true;
            p.flanks = false;
            p.travelOrder = ArmyOrderType.AttackMove;
            p.holdOrder = ArmyOrderType.HoldGround;

            return Save(p);
        }

        /// <summary>Экономика и аванпосты. Выходит поздно, но с полным лимитом и прокачкой.</summary>
        private static BotPersonalityConfig Steward()
        {
            BotPersonalityConfig p = LoadOrCreate<BotPersonalityConfig>(BotFolder + "/Bot_Steward.asset");

            p.displayName = "Скопидом";
            p.blurb = "Копит золото, забирает аванпосты и выходит в поле уже прокачанным.";
            p.namePool = new[] { "Гелий", "Тибр", "Онис", "Ларх" };

            p.centerWeight = 0.8f;
            p.outpostWeight = 1.7f;
            p.defenseWeight = 1.1f;
            p.raidWeight = 0.25f;
            p.huntWeight = 0.3f;
            p.supportWeight = 0.9f;
            p.economyWeight = 1.6f;

            p.caution = 0.7f;
            p.persistence = 0.7f;
            p.opportunism = 0.35f;
            p.vengeance = 0.2f;
            p.massBeforePush = 0.75f;

            p.rangedShare = 0.45f;
            p.tankPreference = 0.9f;
            p.splashPreference = 1.2f;
            p.garrisonShare = 0.35f;
            p.upgradeEagerness = 0.9f;
            p.branchPriority = new[] { 0.9f, 1f, 1f, 1.6f, 0.8f, 0.4f };

            p.raidMinArmyShare = 0.85f;
            p.raidWithArmy = true;
            p.flanks = false;
            p.travelOrder = ArmyOrderType.FollowLeader;
            p.holdOrder = ArmyOrderType.HoldGround;

            return Save(p);
        }

        /// <summary>
        /// Хитрый: не лезет в лобовую за центр, а ходит по тылам и снимает базы.
        /// Именно этот характер отвечает за «бот залез ко мне на базу, пока я держал флаг».
        /// </summary>
        private static BotPersonalityConfig Fox()
        {
            BotPersonalityConfig p = LoadOrCreate<BotPersonalityConfig>(BotFolder + "/Bot_Fox.asset");

            p.displayName = "Лис";
            p.blurb = "Обходит стороной и бьёт в тыл. Центр берёт только когда его там не ждут.";
            p.namePool = new[] { "Сивый", "Рен", "Хитрец", "Тень" };

            p.centerWeight = 0.65f;
            p.outpostWeight = 1.1f;
            p.defenseWeight = 0.6f;
            p.raidWeight = 1.8f;
            p.huntWeight = 1f;
            p.supportWeight = 0.6f;
            p.economyWeight = 0.9f;

            p.caution = 0.55f;
            p.persistence = 0.35f;
            p.opportunism = 0.9f;
            p.vengeance = 0.4f;
            p.massBeforePush = 0.45f;

            p.rangedShare = 0.4f;
            p.tankPreference = 0.8f;
            p.splashPreference = 1.1f;
            p.garrisonShare = 0.2f;
            p.upgradeEagerness = 0.6f;
            p.branchPriority = new[] { 1.1f, 0.8f, 1f, 1f, 1.5f, 0.7f };

            p.raidMinArmyShare = 0.4f;
            p.raidWithArmy = true;
            p.flanks = true;
            p.travelOrder = ArmyOrderType.FollowLeader;
            p.holdOrder = ArmyOrderType.Defend;

            return Save(p);
        }

        /// <summary>Оборонительный: свои точки, гарнизоны, редкие короткие вылазки.</summary>
        private static BotPersonalityConfig Warden()
        {
            BotPersonalityConfig p = LoadOrCreate<BotPersonalityConfig>(BotFolder + "/Bot_Warden.asset");

            p.displayName = "Страж";
            p.blurb = "Забирает своё и держит намертво. Гарнизоны, щиты, никаких авантюр.";
            p.namePool = new[] { "Борх", "Стен", "Крепь", "Доний" };

            p.centerWeight = 1f;
            p.outpostWeight = 1.2f;
            p.defenseWeight = 1.8f;
            p.raidWeight = 0.15f;
            p.huntWeight = 0.3f;
            p.supportWeight = 1.4f;
            p.economyWeight = 1.1f;

            p.caution = 0.85f;
            p.persistence = 0.85f;
            p.opportunism = 0.2f;
            p.vengeance = 0.25f;
            p.massBeforePush = 0.65f;

            p.rangedShare = 0.35f;
            p.tankPreference = 1.6f;
            p.splashPreference = 0.9f;
            p.garrisonShare = 0.5f;
            p.upgradeEagerness = 0.7f;
            p.branchPriority = new[] { 0.8f, 1.5f, 0.9f, 1.3f, 0.5f, 0.6f };

            p.raidMinArmyShare = 0.9f;
            p.raidWithArmy = true;
            p.flanks = false;
            p.travelOrder = ArmyOrderType.FollowLeader;
            p.holdOrder = ArmyOrderType.Defend;

            return Save(p);
        }

        /// <summary>Охотник за полководцами: злопамятный, лезет в драку, добивает раненых.</summary>
        private static BotPersonalityConfig Butcher()
        {
            BotPersonalityConfig p = LoadOrCreate<BotPersonalityConfig>(BotFolder + "/Bot_Butcher.asset");

            p.displayName = "Мясник";
            p.blurb = "Охотится на полководцев и не забывает обид. Точки его интересуют во вторую очередь.";
            p.namePool = new[] { "Гарм", "Тёсаный", "Скальд", "Барг" };

            p.centerWeight = 0.9f;
            p.outpostWeight = 0.6f;
            p.defenseWeight = 0.5f;
            p.raidWeight = 0.8f;
            p.huntWeight = 2f;
            p.supportWeight = 0.6f;
            p.economyWeight = 0.8f;

            p.caution = 0.15f;
            p.persistence = 0.3f;
            p.opportunism = 0.8f;
            p.vengeance = 0.95f;
            p.massBeforePush = 0.35f;

            p.rangedShare = 0.25f;
            p.tankPreference = 1f;
            p.splashPreference = 1.4f;
            p.garrisonShare = 0.1f;
            p.upgradeEagerness = 0.5f;
            p.branchPriority = new[] { 1.6f, 1f, 0.6f, 0.9f, 1.2f, 1.4f };

            p.raidMinArmyShare = 0.5f;
            p.raidWithArmy = false;
            p.flanks = true;
            p.travelOrder = ArmyOrderType.AttackMove;
            p.holdOrder = ArmyOrderType.HoldGround;

            return Save(p);
        }

        #endregion

        #region Сложности

        private static BotDifficultyConfig Easy()
        {
            BotDifficultyConfig d = LoadOrCreate<BotDifficultyConfig>(BotFolder + "/BotDifficulty_Easy.asset");

            d.displayName = "Лёгкий";
            d.level = BotDifficulty.Easy;

            d.decisionInterval = 1.6f;
            d.reactionDelay = 1.1f;
            d.actionsPerMinute = 28f;

            d.vision = BotVisionMode.Honest;
            d.visionBonus = 0f;
            d.memorySeconds = 8f;

            d.micro = 0.15f;
            d.mistakeChance = 0.45f;
            d.economySkill = 0.25f;
            d.upgradeSkill = 0.2f;
            d.retreatHealthFraction = 0.3f;

            return Save(d);
        }

        private static BotDifficultyConfig Normal()
        {
            BotDifficultyConfig d = LoadOrCreate<BotDifficultyConfig>(BotFolder + "/BotDifficulty_Normal.asset");

            d.displayName = "Обычный";
            d.level = BotDifficulty.Normal;

            d.decisionInterval = 0.9f;
            d.reactionDelay = 0.6f;
            d.actionsPerMinute = 60f;

            d.vision = BotVisionMode.Honest;
            d.visionBonus = 0f;
            d.memorySeconds = 18f;

            d.micro = 0.45f;
            d.mistakeChance = 0.25f;
            d.economySkill = 0.55f;
            d.upgradeSkill = 0.55f;
            d.retreatHealthFraction = 0.35f;

            return Save(d);
        }

        private static BotDifficultyConfig Hard()
        {
            BotDifficultyConfig d = LoadOrCreate<BotDifficultyConfig>(BotFolder + "/BotDifficulty_Hard.asset");

            d.displayName = "Сложный";
            d.level = BotDifficulty.Hard;

            d.decisionInterval = 0.5f;
            d.reactionDelay = 0.3f;
            d.actionsPerMinute = 110f;

            // Разведка союзников, но не всеведение: обмануть его по-прежнему можно,
            // просто дороже.
            d.vision = BotVisionMode.Shared;
            d.visionBonus = 6f;
            d.memorySeconds = 30f;

            d.micro = 0.75f;
            d.mistakeChance = 0.12f;
            d.economySkill = 0.8f;
            d.upgradeSkill = 0.85f;
            d.retreatHealthFraction = 0.4f;

            return Save(d);
        }

        /// <summary>
        /// Верхняя сложность. Всеведение включено явно и честно — это единственный уровень,
        /// который смотрит сквозь карту, и именно поэтому он назван «жестоким», а не «умным».
        /// Прямых гандикапов по деньгам всё равно нет: он побеждает скоростью и точностью.
        /// </summary>
        private static BotDifficultyConfig Brutal()
        {
            BotDifficultyConfig d = LoadOrCreate<BotDifficultyConfig>(BotFolder + "/BotDifficulty_Brutal.asset");

            d.displayName = "Жестокий";
            d.level = BotDifficulty.Brutal;

            d.decisionInterval = 0.3f;
            d.reactionDelay = 0.12f;
            d.actionsPerMinute = 180f;

            d.vision = BotVisionMode.Omniscient;
            d.visionBonus = 12f;
            d.memorySeconds = 45f;

            d.micro = 0.95f;
            d.mistakeChance = 0.04f;
            d.economySkill = 0.95f;
            d.upgradeSkill = 0.95f;
            d.retreatHealthFraction = 0.45f;

            d.incomeMultiplier = 1f;
            d.costMultiplier = 1f;

            return Save(d);
        }

        #endregion

        #region Служебное

        private static T Save<T>(T asset) where T : Object
        {
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            return asset;
        }

        private static T LoadSingle<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            if (guids.Length == 0)
            {
                Debug.LogError($"Warlord: в проекте нет ассета {typeof(T).Name}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        #endregion
    }
}
