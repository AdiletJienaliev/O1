using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Собирает контроллер аниматора из готового набора клипов ModularRPGHeroesPolyArt.
    /// Один набор — один тип оружия: Bow, MagicWand, SwordShield, SingleTwoHandSword.
    ///
    /// Контроллер собирается кодом, а не руками в окне Animator, по двум причинам.
    /// Во-первых, наборов пять, а состояний в каждом полтора десятка — руками это
    /// пять шансов промахнуться. Во-вторых, порог смешивания завязан на walkSpeedFactor
    /// из <see cref="Warlord.Configs.UnitConfig"/>: правится число в конфиге — контроллер
    /// пересобирается под него, и походка не разъезжается с реальной скоростью.
    ///
    /// Пересборка идёт поверх существующего ассета: GUID сохраняется, поэтому ссылки
    /// из префабов и сцен не рвутся. Ручные правки в контроллере при этом теряются —
    /// правьте генератор, а не результат.
    /// </summary>
    public static class WarlordUnitAnimatorBuilder
    {
        public const string AnimationRoot = "Assets/ModularRPGHeroesPolyArt/Animations";

        #region Имена параметров. Совпадают с полями AnimatorCharacterAnimation.

        private const string MoveX = "x";
        private const string MoveY = "y";
        private const string Speed = "run";
        private const string Grounded = "grounded";
        private const string Defend = "defend";
        private const string Attack = "attack";
        private const string Jump = "jump";
        private const string Death = "death";
        private const string Respawn = "respawn";
        private const string Block = "block";

        #endregion

        /// <param name="setName">Папка набора внутри Animations, например SwordShield.</param>
        /// <param name="controllerPath">Куда положить контроллер, вместе с расширением.</param>
        /// <param name="walkThreshold">Доля от скорости бега, на которой идёт шаг. Обычно walkSpeedFactor.</param>
        /// <param name="attackPrefixes">
        /// Чем бьёт этот тип, в порядке предпочтения. Три щитоносца делят один набор клипов,
        /// и разный замах — единственное, чем они отличаются в движении, а не в снаряжении.
        /// </param>
        /// <param name="report">Куда дописать, что получилось и чего не хватило.</param>
        public static AnimatorController Build(
            string setName,
            string controllerPath,
            float walkThreshold,
            string[] attackPrefixes,
            List<string> report)
        {
            string folder = AnimationRoot + "/" + setName;

            if (!Directory.Exists(folder))
            {
                report.Add(setName + ": папки с анимациями нет — контроллер не собран");
                return null;
            }

            Clips clips = Clips.Load(folder, attackPrefixes);

            if (clips.Idle == null || clips.Walk == null || clips.Run == null)
            {
                report.Add(setName + ": не хватает базовых клипов (Idle/Walk/Run) — контроллер не собран");
                return null;
            }

            AnimatorController controller = OpenOrCreate(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AddParameters(controller, in clips);

            AnimatorState locomotion = BuildLocomotion(controller, in clips, walkThreshold);
            machine.defaultState = locomotion;

            AnimatorState attack = BuildCombat(machine, locomotion, in clips);
            BuildShield(machine, locomotion, attack, in clips);

            // Возврат из удара в движение ставится последним: он самый общий, и условие
            // «щит опущен» на нём должно проверяться уже после перехода обратно в защиту.
            AnimatorStateTransition exit = attack.AddTransition(locomotion);
            Timed(exit, 0.85f, 0.12f);

            if (clips.Defend != null)
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, Defend);

            EditorUtility.SetDirty(controller);
            report.Add(setName + " → " + Path.GetFileName(controllerPath) + ": " + clips.Describe());

            return controller;
        }

        /// <summary>
        /// Открывает контроллер по пути и вычищает его до пустого. Подобъекты — состояния,
        /// деревья смешивания, переходы — удаляются поимённо: без этого каждая пересборка
        /// оставляла бы внутри ассета прошлый набор мусором, невидимым в окне Animator.
        /// </summary>
        private static AnimatorController OpenOrCreate(string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller == null)
                return AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.parameters = new AnimatorControllerParameter[0];
            controller.layers = new AnimatorControllerLayer[0];

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset != null && asset != controller)
                    Object.DestroyImmediate(asset, true);
            }

            controller.AddLayer("Base Layer");
            return controller;
        }

        private static void AddParameters(AnimatorController controller, in Clips clips)
        {
            controller.AddParameter(MoveX, AnimatorControllerParameterType.Float);
            controller.AddParameter(MoveY, AnimatorControllerParameterType.Float);
            controller.AddParameter(Speed, AnimatorControllerParameterType.Float);

            // Юниты всегда на земле, поэтому по умолчанию true: иначе первый же прыжок
            // полководца завис бы в воздухе, пока кто-нибудь не выставит параметр руками.
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = Grounded,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            });

            controller.AddParameter(Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(Death, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(Respawn, AnimatorControllerParameterType.Trigger);

            if (clips.Jump != null)
                controller.AddParameter(Jump, AnimatorControllerParameterType.Trigger);

            // Параметров щита у лучника и мага просто нет: AnimatorCharacterAnimation
            // проверяет их наличие и молча пропускает вызовы, а лишний параметр
            // в контроллере выглядел бы как забытая, но работающая механика.
            if (clips.Defend != null)
            {
                controller.AddParameter(Defend, AnimatorControllerParameterType.Bool);
                controller.AddParameter(Block, AnimatorControllerParameterType.Trigger);
            }
        }

        /// <summary>
        /// Стойка, шаг и бег одним деревом. Внешнее дерево одномерное по доле от скорости бега,
        /// внутренние — двумерные по направлению шага в осях тела. Разделение и даёт ту самую
        /// походку: на шаге юнит переступает вбок и назад, не разворачивая корпус, а на бегу
        /// смотрит туда, куда бежит.
        /// </summary>
        private static AnimatorState BuildLocomotion(AnimatorController controller, in Clips clips, float walkThreshold)
        {
            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree root, 0);

            root.name = "Locomotion";
            root.blendType = BlendTreeType.Simple1D;
            root.blendParameter = Speed;
            root.useAutomaticThresholds = false;

            root.AddChild(clips.Idle, 0f);

            BlendTree walk = root.CreateBlendTreeChild(Mathf.Clamp(walkThreshold, 0.05f, 0.95f));
            walk.name = "Шаг";
            Directional(walk, clips.Walk, clips.WalkBack, clips.WalkLeft, clips.WalkRight);

            BlendTree run = root.CreateBlendTreeChild(1f);
            run.name = "Бег";

            // Боковых клипов бега в наборе нет, и подмешивать сюда шаг нельзя — на скорости
            // это читалось бы как рассинхрон ног. Строевой стрейф всегда идёт шагом,
            // а на бегу корпус развёрнут по движению, так что вбок дерево и не спрашивают.
            Directional(run, clips.Run, clips.RunBack, null, null);

            return state;
        }

        /// <summary>Двумерное дерево по направлению. Пустые стороны просто не добавляются.</summary>
        private static void Directional(
            BlendTree tree,
            AnimationClip forward,
            AnimationClip back,
            AnimationClip left,
            AnimationClip right)
        {
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = MoveX;
            tree.blendParameterY = MoveY;

            if (forward != null)
                tree.AddChild(forward, new Vector2(0f, 1f));

            if (back != null)
                tree.AddChild(back, new Vector2(0f, -1f));

            if (left != null)
                tree.AddChild(left, new Vector2(-1f, 0f));

            if (right != null)
                tree.AddChild(right, new Vector2(1f, 0f));
        }

        /// <summary>Удар, прыжок, смерть и возвращение в строй. Возвращает состояние удара.</summary>
        private static AnimatorState BuildCombat(AnimatorStateMachine machine, AnimatorState locomotion, in Clips clips)
        {
            // Раскладка узлов в окне Animator. Позиция состояния хранится в машине, а не
            // в самом состоянии, поэтому просто раскладываем по сетке от начала координат.
            Vector3 origin = new(320f, 0f, 0f);

            AnimatorState attack = machine.AddState("Attack", origin + new Vector3(320f, -80f));
            attack.motion = clips.Attack != null ? clips.Attack : clips.Idle;

            // Удар вешается на конкретные состояния, а не на AnyState: с AnyState залетевший
            // триггер поднимал бы труп на ноги, чтобы тот замахнулся ещё раз.
            Trigger(locomotion.AddTransition(attack), Attack, 0.06f);

            AnimatorState death = machine.AddState("Death", origin + new Vector3(320f, 80f));
            death.motion = clips.Die != null ? clips.Die : clips.Idle;

            // А вот смерть обязана перебивать всё подряд, поэтому она и только она — из AnyState.
            AnimatorStateTransition dying = machine.AddAnyStateTransition(death);
            dying.canTransitionToSelf = false;
            Trigger(dying, Death, 0.12f);

            AnimatorState respawn = machine.AddState("Respawn", origin + new Vector3(600f, 80f));
            respawn.motion = clips.DieRecover != null ? clips.DieRecover : clips.Idle;

            Trigger(death.AddTransition(respawn), Respawn, 0.12f);
            Timed(respawn.AddTransition(locomotion), 0.9f, 0.15f);

            if (clips.Jump == null)
                return attack;

            AnimatorState jump = machine.AddState("Jump", origin + new Vector3(320f, -220f));
            jump.motion = clips.Jump;

            Trigger(locomotion.AddTransition(jump), Jump, 0.08f);

            AnimatorStateTransition landing = jump.AddTransition(locomotion);
            Timed(landing, 0.8f, 0.15f);
            landing.AddCondition(AnimatorConditionMode.If, 0f, Grounded);

            return attack;
        }

        /// <summary>Стойка со щитом и приём удара на щит. Собирается только там, где клипы есть.</summary>
        private static void BuildShield(
            AnimatorStateMachine machine,
            AnimatorState locomotion,
            AnimatorState attack,
            in Clips clips)
        {
            if (clips.Defend == null)
                return;

            Vector3 origin = new(320f, 0f, 0f);

            AnimatorState defend = machine.AddState("Defend", origin + new Vector3(-320f, 80f));
            defend.motion = clips.Defend;

            AnimatorStateTransition raise = locomotion.AddTransition(defend);
            raise.hasExitTime = false;
            raise.hasFixedDuration = true;
            raise.duration = 0.2f;
            raise.AddCondition(AnimatorConditionMode.If, 0f, Defend);

            AnimatorStateTransition lower = defend.AddTransition(locomotion);
            lower.hasExitTime = false;
            lower.hasFixedDuration = true;
            lower.duration = 0.2f;
            lower.AddCondition(AnimatorConditionMode.IfNot, 0f, Defend);

            // Бить из-за щита можно, поэтому удар доступен и отсюда. Возврат разведён по
            // состоянию щита: приказ никуда не делся, пока персонаж махал мечом.
            Trigger(defend.AddTransition(attack), Attack, 0.06f);

            AnimatorStateTransition back = attack.AddTransition(defend);
            Timed(back, 0.85f, 0.12f);
            back.AddCondition(AnimatorConditionMode.If, 0f, Defend);

            if (clips.DefendHit == null)
                return;

            AnimatorState impact = machine.AddState("DefendHit", origin + new Vector3(-600f, 80f));
            impact.motion = clips.DefendHit;

            Trigger(defend.AddTransition(impact), Block, 0.04f);
            Timed(impact.AddTransition(defend), 0.8f, 0.1f);
        }

        private static void Trigger(AnimatorStateTransition transition, string parameter, float duration)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        private static void Timed(AnimatorStateTransition transition, float exitTime, float duration)
        {
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
        }

        /// <summary>
        /// Клипы одного набора. Имена в ассете не выдержаны в одном регистре
        /// (SingleTwohandSword против папки SingleTwoHandSword), а часть наборов зовёт
        /// те же движения иначе — поэтому ищем по префиксу до подчёркивания и с вариантами.
        /// </summary>
        private readonly struct Clips
        {
            public readonly AnimationClip Idle;
            public readonly AnimationClip Walk;
            public readonly AnimationClip WalkBack;
            public readonly AnimationClip WalkLeft;
            public readonly AnimationClip WalkRight;
            public readonly AnimationClip Run;
            public readonly AnimationClip RunBack;
            public readonly AnimationClip Attack;
            public readonly AnimationClip Die;
            public readonly AnimationClip DieRecover;
            public readonly AnimationClip Jump;
            public readonly AnimationClip Defend;
            public readonly AnimationClip DefendHit;

            private Clips(string folder, string[] attackPrefixes)
            {
                Idle = Locate(folder, "Idle", "StandingIdle");
                Walk = Locate(folder, "Walk", "NormalWalk", "BattleWalk");
                WalkBack = Locate(folder, "WalkBack", "BattleWalkBackward");
                WalkLeft = Locate(folder, "WalkLeft", "BattleWalkLeft");
                WalkRight = Locate(folder, "WalkRight", "BattleWalkRight");
                Run = Locate(folder, "Run", "NormalRun");
                RunBack = Locate(folder, "RunBack", "NormalRunBack");
                Attack = attackPrefixes != null && attackPrefixes.Length > 0
                    ? Locate(folder, attackPrefixes)
                    : null;

                // Заказанного замаха в наборе может не оказаться — тогда берём любой,
                // лишь бы юнит не бил воздух стойкой.
                Attack ??= Locate(folder, "NormalAttack01", "Attack01", "Combo01");
                Die = Locate(folder, "Die");
                DieRecover = Locate(folder, "DieRecover");
                Jump = Locate(folder, "JumpStart", "JumpAir");
                Defend = Locate(folder, "Defend");
                DefendHit = Locate(folder, "DefendHit");
            }

            public static Clips Load(string folder, string[] attackPrefixes) => new(folder, attackPrefixes);

            public string Describe()
            {
                AnimationClip[] all =
                {
                    Idle, Walk, WalkBack, WalkLeft, WalkRight, Run, RunBack,
                    Attack, Die, DieRecover, Jump, Defend, DefendHit
                };

                int found = 0;

                foreach (AnimationClip clip in all)
                {
                    if (clip != null)
                        found++;
                }

                return found + " из " + all.Length + " клипов" + (Defend != null ? ", со щитом" : string.Empty);
            }

            /// <summary>
            /// Первый файл в папке, чьё имя начинается с префикса и подчёркивания.
            /// Подчёркивание тут не украшение: без него Walk нашёл бы WalkBack,
            /// Die — DieRecover, а Defend — DefendHit.
            /// </summary>
            private static AnimationClip Locate(string folder, params string[] prefixes)
            {
                foreach (string prefix in prefixes)
                {
                    foreach (string path in Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileNameWithoutExtension(path);

                        if (!name.StartsWith(prefix + "_", System.StringComparison.OrdinalIgnoreCase))
                            continue;

                        AnimationClip clip = LoadClip(path.Replace('\\', '/'));

                        if (clip != null)
                            return clip;
                    }
                }

                return null;
            }

            /// <summary>Клип из FBX. Превью-клип, который Unity кладёт рядом, пропускаем.</summary>
            private static AnimationClip LoadClip(string assetPath)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        return clip;
                }

                return null;
            }
        }
    }
}
