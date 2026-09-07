using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Warlord.Configs;
using Warlord.EditorTools.UI;
using Warlord.Gameplay.Units;
using Warlord.Presentation;
using Warlord.Presentation.Animation;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Пересобирает юнитов на модели и анимации ModularRPGHeroesPolyArt: модель, контроллер
    /// аниматора, щит в конфиге, полоска здоровья по росту и радиус агента по ширине плеч.
    ///
    /// Всё описано таблицей <see cref="Looks"/> — она и есть источник правды о том, кто как
    /// выглядит и чем машет. Хочется поменять лучнику лук или дать копейщику другой замах —
    /// правится одна строка таблицы и жмётся пункт меню, а не тридцать полей по инспекторам.
    ///
    /// Операция идемпотентна: повторный запуск приводит префаб к тому же виду, а не копит
    /// в нём слои прошлых запусков.
    /// </summary>
    public static class WarlordUnitVisualSetup
    {
        private const string PrefabFolder = "Assets/_InternalAssets/Prefabs/Units";
        private const string ControllerFolder = "Assets/_InternalAssets/Art/Animation/Animators";
        private const string CharacterFolder = "Assets/ModularRPGHeroesPolyArt/Prefabs/BasicCharacters";

        /// <summary>Запас над макушкой для полоски здоровья, м.</summary>
        private const float HealthBarClearance = 0.45f;

        /// <summary>
        /// Кто во что одет и чем бьёт.
        ///
        /// Копья в ассете нет — ни в мешах, ни в анимациях, — поэтому копейщик получает
        /// молот со щитом: набор анимаций у него всё равно должен быть щитовой, иначе
        /// приказ «Защита» ему нечем отыгрывать. Захочется настоящее копьё — это отдельная
        /// модель и отдельный набор клипов, таблица тут ни при чём.
        /// </summary>
        private readonly struct UnitLook
        {
            public readonly string Prefab;
            public readonly string Controller;
            public readonly string AnimationSet;

            /// <summary>Готовый персонаж из ассета. Пусто — модель у префаба не трогаем.</summary>
            public readonly string Character;

            /// <summary>Замах в порядке предпочтения: чем щитоносцы отличаются друг от друга.</summary>
            public readonly string[] Attacks;

            public readonly bool Shield;

            public UnitLook(
                string prefab,
                string controller,
                string animationSet,
                string character,
                string[] attacks,
                bool shield)
            {
                Prefab = prefab;
                Controller = controller;
                AnimationSet = animationSet;
                Character = character;
                Attacks = attacks;
                Shield = shield;
            }
        }

        private static readonly UnitLook[] Looks =
        {
            // Мечник: меч и круглый щит, прямой рубящий замах.
            new("Unit_SwordsMan", "Unit_SwordsMan", "SwordShield", "SwordShield03",
                new[] { "NormalAttack01" }, true),

            // Легионер: тяжёлый доспех и большой щит, второй замах — размашистее.
            new("Unit_Legionary", "Unit_Legioner", "SwordShield", "SwordShield05",
                new[] { "NormalAttack02" }, true),

            // Копейщик: молот вместо копья, длинная связка вместо одиночного удара.
            new("Unit_SpearMan", "Unit_SpearMan", "SwordShield", "SwordShield02",
                new[] { "Combo01" }, true),

            // Охранник-копейщик (ГДД §1.2). Модель и замах свои, чтобы гарнизон на точке
            // не путался с полевым строем: игрок должен различать их с одного взгляда.
            // Щита в конфиге нет намеренно — приказа «Защита» охранник не получает никогда,
            // и поднятый блок ему просто нечем включить (см. GuardRoutine).
            new("Unit_GuardSpear", "Unit_GuardSpear", "SwordShield", "SwordShield04",
                new[] { "Combo02", "NormalAttack02" }, false),

            // Лучник: щита нет, по приказу «Защита» просто держит слот и стреляет.
            new("Unit_Archer", "Unit_Archer", "Bow", "Bow02",
                new[] { "Attack01" }, false),

            // Маг: посох, свой набор, тоже без щита.
            new("Unit_Mag", "Unit_Mag", "MagicWand", "MagicWand03",
                new[] { "Attack01" }, false),

            // Полководец. Модель у него своя и остаётся как есть — меняется только контроллер:
            // клипы гуманоидные и на его скелет ложатся ретаргетом. Без этого он один остался бы
            // со старым контроллером, который читает оси движения по прежним правилам.
            new("Player", "Character", "SingleTwoHandSword", string.Empty,
                new[] { "NormalAttack01" }, false)
        };

        [MenuItem("Warlord/Настройка/8. Модели, анимации и щиты юнитов", priority = 7)]
        public static void Build()
        {
            List<string> report = new();

            foreach (UnitLook look in Looks)
                Process(look, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Warlord: юниты пересобраны на ModularRPGHeroesPolyArt:\n" + string.Join("\n", report));
        }

        private static void Process(in UnitLook look, List<string> report)
        {
            string prefabPath = PrefabFolder + "/" + look.Prefab + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                report.Add(look.Prefab + ": префаба нет по пути " + prefabPath);
                return;
            }

            UnitConfig config = ResolveConfig(prefabPath);
            float walkThreshold = config != null ? config.walkSpeedFactor : 0.45f;

            AnimatorController controller = WarlordUnitAnimatorBuilder.Build(
                look.AnimationSet,
                ControllerFolder + "/" + look.Controller + ".controller",
                walkThreshold,
                look.Attacks,
                report);

            ApplyToPrefab(prefabPath, in look, controller, report);
            ApplyToConfig(config, in look, report);
        }

        /// <summary>Конфиг юнита берём с самого префаба: имена ассетов и префабов разошлись давно.</summary>
        private static UnitConfig ResolveConfig(string prefabPath)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return asset != null && asset.TryGetComponent(out UnitEntity unit) ? unit.Config : null;
        }

        #region Префаб

        private static void ApplyToPrefab(
            string path,
            in UnitLook look,
            AnimatorController controller,
            List<string> report)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents == null)
                return;

            try
            {
                GameObject model = string.IsNullOrEmpty(look.Character)
                    ? FindExistingModel(contents)
                    : ReplaceModel(contents, look.Character, report);

                if (model == null)
                {
                    report.Add(look.Prefab + ": модель не найдена — префаб не тронут");
                    return;
                }

                Animator animator = SetupAnimator(model, controller);

                if (animator == null)
                {
                    report.Add(look.Prefab + ": на модели нет Animator — анимации не подключены");
                    return;
                }

                BindAnimation(contents, animator);
                BindTeamColor(contents, model, report, look.Prefab);

                Bounds bounds = MeasureModel(model);
                PlaceHealthBar(contents, bounds);
                FitAgent(contents, bounds);

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                report.Add(look.Prefab + ": модель " + model.name + ", рост " + bounds.size.y.ToString("0.00") + " м");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Меняет модель, не трогая всё остальное. Из детей корня уцелевает только тот,
        /// в котором лежит Canvas: это полоска здоровья, она настраивается отдельно
        /// и переживать смену модели обязана.
        /// </summary>
        private static GameObject ReplaceModel(GameObject contents, string characterName, List<string> report)
        {
            string characterPath = CharacterFolder + "/" + characterName + ".prefab";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(characterPath);

            if (source == null)
            {
                report.Add("персонажа " + characterName + " нет по пути " + characterPath);
                return FindExistingModel(contents);
            }

            for (int i = contents.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = contents.transform.GetChild(i);

                if (child.GetComponentInChildren<Canvas>(true) == null)
                    Object.DestroyImmediate(child.gameObject);
            }

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, contents.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            return model;
        }

        /// <summary>Модель уже стоящего префаба: первый ребёнок с Animator, иначе с рендерерами.</summary>
        private static GameObject FindExistingModel(GameObject contents)
        {
            Animator animator = contents.GetComponentInChildren<Animator>(true);

            if (animator != null)
                return animator.gameObject;

            Renderer renderer = contents.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.gameObject : null;
        }

        private static Animator SetupAnimator(GameObject model, AnimatorController controller)
        {
            Animator animator = model.GetComponentInChildren<Animator>(true);

            if (animator == null || controller == null)
                return animator;

            animator.runtimeAnimatorController = controller;

            // Корневое движение забрал бы себе NavMeshAgent на сервере и интерполяция
            // на клиентах: клип, двигающий персонажа сам, дрался бы с обоими.
            animator.applyRootMotion = false;

            // Кости за кадром не считаем, но позу держим: иначе ушедший с экрана строй
            // возвращался бы в кадр в позе-склейке.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            return animator;
        }

        private static void BindAnimation(GameObject contents, Animator animator)
        {
            if (!contents.TryGetComponent(out AnimatorCharacterAnimation animation))
                animation = contents.AddComponent<AnimatorCharacterAnimation>();

            using Bind bind = new(animation);
            bind.Ref("animator", animator);
        }

        /// <summary>
        /// Цвет команды кладём на плащ и щит, а не на всю модель: модульный персонаж собран
        /// из полутора десятков кусков, и покраска всех разом превращает его в силуэт.
        /// Не нашлось ни плаща, ни щита — красим всё, но вполсилы.
        /// </summary>
        private static void BindTeamColor(GameObject contents, GameObject model, List<string> report, string prefabName)
        {
            if (!contents.TryGetComponent(out TeamColorApplier applier))
                applier = contents.AddComponent<TeamColorApplier>();

            List<Object> marked = new();

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy)
                    continue;

                string name = renderer.gameObject.name;

                if (name.StartsWith("Cloth", System.StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("Shield", System.StringComparison.OrdinalIgnoreCase))
                {
                    marked.Add(renderer);
                }
            }

            using Bind bind = new(applier);

            if (marked.Count > 0)
            {
                bind.Refs("renderers", marked.ToArray()).Float("strength", 1f);
                return;
            }

            bind.Refs("renderers").Float("strength", 0.35f);
            report.Add(prefabName + ": плащ и щит не опознаны — цвет команды лёг тонировкой на всю модель");
        }

        #endregion

        #region Размеры

        /// <summary>Габариты модели по включённым рендерерам, в осях самого префаба.</summary>
        private static Bounds MeasureModel(GameObject model)
        {
            Bounds bounds = new(model.transform.position, Vector3.zero);
            bool started = false;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy)
                    continue;

                if (!started)
                {
                    bounds = renderer.bounds;
                    started = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            // Совсем без рендереров остаться нельзя: тогда полоска здоровья уедет в пол,
            // а агент получит нулевой радиус и перестанет расталкивать соседей.
            return started ? bounds : new Bounds(Vector3.up, new Vector3(0.6f, 1.8f, 0.6f));
        }

        private static void PlaceHealthBar(GameObject contents, Bounds bounds)
        {
            Canvas canvas = contents.GetComponentInChildren<Canvas>(true);

            if (canvas == null)
                return;

            Vector3 local = canvas.transform.localPosition;
            local.y = bounds.max.y + HealthBarClearance;
            canvas.transform.localPosition = local;
        }

        /// <summary>
        /// Агент под новый силуэт. Радиус берём по плечам, а не по вытянутому оружию:
        /// иначе лучник с луком в руке расталкивал бы строй на полметра шире, чем занимает.
        /// </summary>
        private static void FitAgent(GameObject contents, Bounds bounds)
        {
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.5f, 0.2f, 0.6f);
            float height = Mathf.Max(bounds.size.y, 0.5f);

            if (contents.TryGetComponent(out NavMeshAgent agent))
            {
                agent.radius = radius;
                agent.height = height;
            }

            if (contents.TryGetComponent(out UnitEntity unit))
            {
                using Bind bind = new(unit);
                bind.Float("bodyRadius", radius);
            }
        }

        #endregion

        /// <summary>Щит и походка в конфиг. Скорость и урон не трогаем — это баланс, а не вид.</summary>
        private static void ApplyToConfig(UnitConfig config, in UnitLook look, List<string> report)
        {
            if (config == null)
            {
                report.Add(look.Prefab + ": конфиг не найден — щит и походку выставьте вручную");
                return;
            }

            config.hasShield = look.Shield;

            if (look.Shield)
            {
                // Сектор чуть уже прямого угла в каждую сторону: строго девяносто означало бы,
                // что удар точно сбоку иногда блокируется, а иногда нет — на дрожании числа.
                config.blockAngle = 75f;

                // Ноль — фронт не пробивается вовсе. Это и просили: обходите с фланга.
                config.blockDamageFactor = 0f;
            }

            EditorUtility.SetDirty(config);
        }
    }
}
