using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Warlord.EditorTools.UI;
using Warlord.Presentation.Animation;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Новая внешность полководца: модель из того же пака, что и юниты, и собранный под неё
    /// аниматор.
    ///
    /// Старая модель была стикменом из другого набора, а клипы ей доставались от
    /// ModularRPGHeroesPolyArt — переносить анимацию между чужими друг другу скелетами
    /// Unity умеет только через humanoid-ретаргет, и любое расхождение в аватаре кончается
    /// сломанной позой. Полководец на общем с юнитами скелете снимает этот класс проблем
    /// целиком, а два меча отличают его от любого юнита в бою.
    /// </summary>
    public static class WarlordHeroSetup
    {
        private const string ModelPath = "Assets/ModularRPGHeroesPolyArt/Prefabs/BasicCharacters/DoubleSword03.prefab";
        private const string ClipFolder = "Assets/ModularRPGHeroesPolyArt/Animations/DoubleSwords";
        private const string ClipSuffix = "_DoubleSword";

        private const string ControllerPath = "Assets/_InternalAssets/Art/Animation/Animators/Hero.controller";
        private const string PlayerPrefabPath = "Assets/_InternalAssets/Prefabs/Units/Player.prefab";

        /// <summary>Порог параметра run, на котором шаг переходит в бег.</summary>
        private const float WalkThreshold = 0.45f;

        /// <summary>Ускорение боковых клипов шага, подставленных в дерево бега.</summary>
        private const float SideStepTimeScale = 1.5f;

        [MenuItem("Warlord/Настройка/10. Новая модель и анимации полководца", priority = 9)]
        public static void BuildHero()
        {
            List<string> report = new();

            AnimatorController controller = BuildController(report);

            if (controller == null)
            {
                Debug.LogError("Warlord: клипы полководца не найдены в " + ClipFolder);
                return;
            }

            SwapModel(controller, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Warlord: полководец обновлён:\n" + string.Join("\n", report));
        }

        #region Аниматор

        private static AnimatorController BuildController(List<string> report)
        {
            AnimationClip idle = Clip("Idle");

            if (idle == null)
                return null;

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("x", AnimatorControllerParameterType.Float);
            controller.AddParameter("y", AnimatorControllerParameterType.Float);
            controller.AddParameter("run", AnimatorControllerParameterType.Float);
            controller.AddParameter("grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("respawn", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine root = controller.layers[0].stateMachine;

            AnimatorState locomotion = BuildLocomotion(controller, idle);
            root.defaultState = locomotion;

            AnimatorState attack = AddState(root, "Attack", Clip("NormalAttack01"));
            AnimatorState jump = AddState(root, "Jump", Clip("JumpStart"));
            AnimatorState death = AddState(root, "Death", Clip("Die"));
            AnimatorState respawn = AddState(root, "Respawn", Clip("DieRecover"));

            // Удар и прыжок начинаются сразу, без ожидания конца шага.
            Trigger(locomotion, attack, "attack", 0.06f);
            Trigger(locomotion, jump, "jump", 0.08f);

            // Возврат в движение — по времени клипа: атака доигрывается до конца замаха.
            ExitTo(attack, locomotion, 0.85f, 0.12f);
            ExitTo(respawn, locomotion, 0.9f, 0.15f);

            // Прыжок ждёт ещё и приземления, иначе полководец побежал бы в воздухе.
            AnimatorStateTransition landing = ExitTo(jump, locomotion, 0.8f, 0.15f);
            landing.AddCondition(AnimatorConditionMode.If, 0f, "grounded");

            // Смерть — единственный переход из любого состояния: она обрывает что угодно.
            AnimatorStateTransition dying = root.AddAnyStateTransition(death);
            dying.AddCondition(AnimatorConditionMode.If, 0f, "death");
            dying.hasExitTime = false;
            dying.duration = 0.12f;
            dying.hasFixedDuration = true;
            dying.canTransitionToSelf = false;

            // Поза смерти держится до самого воскрешения — своего времени выхода у неё нет.
            Trigger(death, respawn, "respawn", 0.12f);

            EditorUtility.SetDirty(controller);
            report.Add("собран " + ControllerPath);

            return controller;
        }

        /// <summary>
        /// Движение: одномерное дерево по параметру run выбирает стойку, шаг или бег,
        /// а внутри шага и бега двумерные деревья по x/y выбирают сторону.
        ///
        /// Оба двумерных дерева — Freeform Cartesian и с клипом в центре. Simple Directional
        /// в точке (0,0) не определён, и на любом проходе осей через ноль персонаж встаёт
        /// в T-позу; Cartesian считает веса по расстоянию и такой дыры не имеет.
        /// </summary>
        private static AnimatorState BuildLocomotion(AnimatorController controller, AnimationClip idle)
        {
            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree gait, 0);

            gait.blendType = BlendTreeType.Simple1D;
            gait.blendParameter = "run";
            gait.useAutomaticThresholds = false;

            BlendTree walk = Directional(controller, "Шаг", idle,
                Clip("Walk"), Clip("WalkBack"), Clip("WalkLeft"), Clip("WalkRight"), 1f);

            // Бега вбок в паке нет — берём боковой шаг и разгоняем его: на полной скорости
            // юнит боком почти не ходит, но ось x проходит через бок на каждом развороте,
            // и клип там обязан быть.
            BlendTree run = Directional(controller, "Бег", idle,
                Clip("Run"), Clip("RunBack"), Clip("WalkLeft"), Clip("WalkRight"), SideStepTimeScale);

            gait.children = new[]
            {
                Child(idle, 0f, Vector2.zero, 1f),
                Child(walk, WalkThreshold, Vector2.zero, 1f),
                Child(run, 1f, Vector2.zero, 1f)
            };

            return state;
        }

        private static BlendTree Directional(
            AnimatorController controller,
            string name,
            AnimationClip center,
            AnimationClip forward,
            AnimationClip back,
            AnimationClip left,
            AnimationClip right,
            float sideTimeScale)
        {
            BlendTree tree = new()
            {
                name = name,
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "x",
                blendParameterY = "y",

                // Дерево — подобъект контроллера. Без скрытия оно засоряет ассет отдельной строкой.
                hideFlags = HideFlags.HideInHierarchy
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            List<ChildMotion> children = new(5) { Child(center, 0f, Vector2.zero, 1f) };

            Add(children, forward, new Vector2(0f, 1f), 1f);
            Add(children, back, new Vector2(0f, -1f), 1f);
            Add(children, left, new Vector2(-1f, 0f), sideTimeScale);
            Add(children, right, new Vector2(1f, 0f), sideTimeScale);

            tree.children = children.ToArray();
            return tree;
        }

        private static void Add(List<ChildMotion> children, Motion motion, Vector2 position, float timeScale)
        {
            if (motion != null)
                children.Add(Child(motion, 0f, position, timeScale));
        }

        private static ChildMotion Child(Motion motion, float threshold, Vector2 position, float timeScale)
        {
            return new ChildMotion
            {
                motion = motion,
                threshold = threshold,
                position = position,
                timeScale = timeScale,
                cycleOffset = 0f,
                directBlendParameter = "x",
                mirror = false
            };
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
        {
            AnimatorState state = machine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static void Trigger(AnimatorState from, AnimatorState to, string parameter, float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.hasFixedDuration = true;
        }

        private static AnimatorStateTransition ExitTo(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.hasFixedDuration = true;
            return transition;
        }

        /// <summary>Клип из набора двух мечей. Имена в паке строятся как «действие_оружие».</summary>
        private static AnimationClip Clip(string action)
        {
            string path = ClipFolder + "/" + action + ClipSuffix + ".fbx";

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }

            return null;
        }

        #endregion

        #region Модель

        private static void SwapModel(AnimatorController controller, List<string> report)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            if (model == null)
            {
                report.Add("модель " + ModelPath + " не найдена — замените вручную");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            RemoveOldModel(contents, report);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, contents.transform);
            instance.name = "Model";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator animator = instance.GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;

                // Корневое движение спорило бы с CharacterController за одну и ту же позицию:
                // тело двигает мотор полководца, анимация только показывает это движение.
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            if (contents.TryGetComponent(out AnimatorCharacterAnimation animation))
            {
                using Bind bind = new(animation);
                bind.Ref("animator", animator);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            report.Add("модель полководца → " + model.name + ", аниматор переназначен");
            report.Add("проверьте высоту CharacterController: старая модель была другого роста");
        }

        /// <summary>
        /// Убирает прежнюю модель. Ищем её по Animator: полоска здоровья, капсула и камера
        /// анимации не имеют, а модель без Animator в префабе полководца смысла не имеет.
        /// </summary>
        private static void RemoveOldModel(GameObject contents, List<string> report)
        {
            Animator[] animators = contents.GetComponentsInChildren<Animator>(true);

            foreach (Animator animator in animators)
            {
                if (animator.gameObject == contents)
                    continue;

                // Анимированный элемент интерфейса — не модель: полоску здоровья сносить нельзя.
                if (animator.GetComponentInParent<Canvas>() != null)
                    continue;

                Transform child = TopmostUnder(contents.transform, animator.transform);
                report.Add("удалена прежняя модель " + child.name);
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform TopmostUnder(Transform root, Transform child)
        {
            Transform current = child;

            while (current.parent != null && current.parent != root)
                current = current.parent;

            return current;
        }

        #endregion
    }
}
