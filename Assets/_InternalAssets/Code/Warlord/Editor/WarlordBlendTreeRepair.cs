using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Приведение двумерных blend tree к безопасному виду.
    ///
    /// Проблема, которую он чинит, в дереве бега: клипы там стоят только вперёд и назад,
    /// а сбоку пусто. 2D Simple Directional по определению раскладывает выборку по
    /// направлениям имеющихся клипов, и точка сбоку между двумя противоположными клипами
    /// для него неоднозначна — оттуда и странные позы при развороте. Freeform Cartesian
    /// считает веса по расстоянию до точек и на разреженном наборе ведёт себя предсказуемо.
    ///
    /// Заодно в дерево бега подставляются боковые клипы шага (ускоренные), а в центр —
    /// стойка: это страховка на случай, если в оси когда-нибудь придёт ноль.
    /// </summary>
    public static class WarlordBlendTreeRepair
    {
        private const string AnimatorFolder = "Assets/_InternalAssets/Art/Animation/Animators";

        /// <summary>Насколько ускоряется клип шага, подставленный в дерево бега.</summary>
        private const float SideStepTimeScale = 1.5f;

        [MenuItem("Warlord/Настройка/8. Починить двумерные blend tree", priority = 7)]
        public static void Repair()
        {
            List<string> report = new();

            string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { AnimatorFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                if (controller == null)
                    continue;

                string name = Path.GetFileNameWithoutExtension(path);

                foreach (AnimatorControllerLayer layer in controller.layers)
                    RepairMachine(layer.stateMachine, name, report);

                EditorUtility.SetDirty(controller);
            }

            AssetDatabase.SaveAssets();

            if (report.Count == 0)
            {
                Debug.Log("Warlord: двумерные blend tree уже в порядке.");
                return;
            }

            Debug.Log("Warlord: blend tree починены:\n" + string.Join("\n", report));
        }

        private static void RepairMachine(AnimatorStateMachine machine, string controllerName, List<string> report)
        {
            foreach (ChildAnimatorState child in machine.states)
                RepairMotion(child.state.motion, controllerName, report);

            foreach (ChildAnimatorStateMachine nested in machine.stateMachines)
                RepairMachine(nested.stateMachine, controllerName, report);
        }

        private static void RepairMotion(Motion motion, string controllerName, List<string> report)
        {
            if (motion is not BlendTree tree)
                return;

            foreach (ChildMotion child in tree.children)
                RepairMotion(child.motion, controllerName, report);

            if (tree.blendType == BlendTreeType.Simple1D || tree.blendType == BlendTreeType.Direct)
                return;

            string label = controllerName + " / " + tree.name;

            if (tree.blendType != BlendTreeType.FreeformCartesian2D)
            {
                tree.blendType = BlendTreeType.FreeformCartesian2D;
                report.Add(label + ": тип → Freeform Cartesian");
            }

            EnsureSides(tree, label, report);
            EnsureCenter(tree, label, report);
        }

        /// <summary>
        /// Боковые клипы. Дерево бега приходит только с «вперёд» и «назад», и при доворосте
        /// оси направления вектор неизбежно проходит через бок — там обязан быть клип.
        /// </summary>
        private static void EnsureSides(BlendTree tree, string label, List<string> report)
        {
            if (HasChildNear(tree, new Vector2(1f, 0f)) || HasChildNear(tree, new Vector2(-1f, 0f)))
                return;

            AnimationClip forward = FindChildClip(tree, new Vector2(0f, 1f));
            if (forward == null)
                return;

            AnimationClip left = LoadSibling(forward, "WalkLeft");
            AnimationClip right = LoadSibling(forward, "WalkRight");

            if (left == null || right == null)
            {
                report.Add(label + ": боковых клипов шага рядом не нашлось — добавьте вручную");
                return;
            }

            Append(tree, left, new Vector2(-1f, 0f), SideStepTimeScale);
            Append(tree, right, new Vector2(1f, 0f), SideStepTimeScale);

            report.Add(label + ": добавлены боковые клипы " + left.name + " / " + right.name);
        }

        private static void EnsureCenter(BlendTree tree, string label, List<string> report)
        {
            if (HasChildNear(tree, Vector2.zero))
                return;

            AnimationClip any = FindAnyClip(tree);
            AnimationClip idle = any != null ? LoadSibling(any, "Idle") : null;

            if (idle == null)
                return;

            Append(tree, idle, Vector2.zero, 1f);
            report.Add(label + ": в центр добавлена стойка " + idle.name);
        }

        private static void Append(BlendTree tree, Motion motion, Vector2 position, float timeScale)
        {
            List<ChildMotion> children = new(tree.children)
            {
                new ChildMotion
                {
                    motion = motion,
                    position = position,
                    timeScale = timeScale,
                    cycleOffset = 0f,
                    directBlendParameter = tree.blendParameter,
                    mirror = false
                }
            };

            tree.children = children.ToArray();
        }

        private static bool HasChildNear(BlendTree tree, Vector2 position)
        {
            foreach (ChildMotion child in tree.children)
            {
                if ((child.position - position).sqrMagnitude < 0.01f)
                    return true;
            }

            return false;
        }

        private static AnimationClip FindChildClip(BlendTree tree, Vector2 position)
        {
            foreach (ChildMotion child in tree.children)
            {
                if ((child.position - position).sqrMagnitude < 0.01f && child.motion is AnimationClip clip)
                    return clip;
            }

            return null;
        }

        private static AnimationClip FindAnyClip(BlendTree tree)
        {
            foreach (ChildMotion child in tree.children)
            {
                if (child.motion is AnimationClip clip)
                    return clip;
            }

            return null;
        }

        /// <summary>
        /// Клип из того же набора: у пака имя файла — это «действие_оружие», и соседний клип
        /// лежит в той же папке под тем же суффиксом. Искать поиском по проекту незачем —
        /// так гарантированно берётся клип того же оружия, а не одноимённый из чужого набора.
        /// </summary>
        private static AnimationClip LoadSibling(AnimationClip reference, string action)
        {
            string path = AssetDatabase.GetAssetPath(reference);
            if (string.IsNullOrEmpty(path))
                return null;

            string fileName = Path.GetFileNameWithoutExtension(path);
            int underscore = fileName.IndexOf('_');

            if (underscore < 0)
                return null;

            string suffix = fileName.Substring(underscore);
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string extension = Path.GetExtension(path);

            string siblingPath = directory + "/" + action + suffix + extension;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(siblingPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }

            return null;
        }
    }
}
