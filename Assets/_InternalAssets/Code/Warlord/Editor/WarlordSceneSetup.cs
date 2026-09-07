using System.Collections.Generic;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warlord.EditorTools.UI;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.World;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Расстановка мира в открытой сцене: арена, базы игроков и флаги. Раньше эти точки
    /// лежали в MapConfig, и координаты приходилось выставлять вслепую — теперь всё
    /// это обычные объекты сцены, а инструмент лишь создаёт их заготовки на местах
    /// из прежней дуэльной карты. Дальше базы двигаются мышью.
    /// </summary>
    public static class WarlordSceneSetup
    {
        private readonly struct BaseLayout
        {
            public readonly int Slot;
            public readonly Vector3 Center;
            public readonly float Yaw;
            public readonly Vector3 HeroSpawn;
            public readonly Vector3 UnitSpawn;

            public BaseLayout(int slot, Vector3 center, float yaw, Vector3 heroSpawn, Vector3 unitSpawn)
            {
                Slot = slot;
                Center = center;
                Yaw = yaw;
                HeroSpawn = heroSpawn;
                UnitSpawn = unitSpawn;
            }
        }

        /// <summary>Точки прежней дуэльной карты. Служат только стартовым положением объектов.</summary>
        private static readonly BaseLayout[] Layouts =
        {
            new(0, new Vector3(-42f, 1f, 0f), 90f, new Vector3(-30f, 1f, 0f), new Vector3(-30f, 1f, -4f)),
            new(1, new Vector3(42f, 1f, 0f), 270f, new Vector3(30f, 1f, 0f), new Vector3(30f, 1f, 4f))
        };

        [MenuItem("Warlord/Настройка/6. Создать арену, базы и флаги в сцене", priority = 5)]
        public static void BuildWorld()
        {
            List<string> report = new();

            Transform root = EnsureRoot(report);

            EnsureArena(root, report);

            for (int i = 0; i < Layouts.Length; i++)
                EnsureBase(root, Layouts[i], report);

            EnsureCentralFlag(root, report);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (report.Count == 0)
            {
                Debug.Log("Warlord: мир в сцене уже собран.");
                return;
            }

            Debug.Log("Warlord: мир в сцене собран:\n" + string.Join("\n", report));
        }

        private static Transform EnsureRoot(List<string> report)
        {
            GameObject existing = GameObject.Find("World");

            if (existing != null)
                return existing.transform;

            GameObject root = new("World");
            Undo.RegisterCreatedObjectUndo(root, "Create World");
            report.Add("создан контейнер World");

            return root.transform;
        }

        private static void EnsureArena(Transform root, List<string> report)
        {
            if (Object.FindAnyObjectByType<MatchArena>() != null)
                return;

            GameObject arena = new("Arena", typeof(MatchArena));
            arena.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(arena, "Create Arena");

            report.Add("создана MatchArena — проверьте размер арены в инспекторе");
        }

        private static void EnsureBase(Transform root, in BaseLayout layout, List<string> report)
        {
            // В режиме редактирования OnEnable у баз не выполняется, поэтому статический
            // реестр пуст — ищем прямо по сцене.
            if (FindBase(layout.Slot) != null)
                return;

            GameObject baseObject = new("Base_" + layout.Slot);
            baseObject.transform.SetParent(root, false);
            baseObject.transform.SetPositionAndRotation(layout.Center, Quaternion.Euler(0f, layout.Yaw, 0f));

            Transform heroSpawn = CreateChild(baseObject.transform, "HeroSpawn", layout.HeroSpawn);
            Transform unitSpawn = CreateChild(baseObject.transform, "UnitSpawn", layout.UnitSpawn);

            PlayerBase playerBase = baseObject.AddComponent<PlayerBase>();

            using (Bind bind = new(playerBase))
            {
                bind.Int("slot", layout.Slot)
                    .Ref("heroSpawnPoint", heroSpawn)
                    .Ref("unitSpawnPoint", unitSpawn)
                    .Float("buyZoneRadius", 12f);
            }

            CreateBaseFlag(baseObject.transform, playerBase);

            Undo.RegisterCreatedObjectUndo(baseObject, "Create Player Base");
            report.Add("создана база слота " + layout.Slot + " с флагом");
        }

        /// <summary>
        /// Флаг базы. Живёт дочерним объектом самой базы: так он ездит вместе с ней,
        /// когда базу двигают по сцене, и слот берёт у неё же, а не дублирует числом.
        /// </summary>
        private static void CreateBaseFlag(Transform parent, PlayerBase owner)
        {
            GameObject flag = new("Flag_Base", typeof(NetworkObject), typeof(CapturePointBehaviour));
            flag.transform.SetParent(parent, false);

            CapturePointBehaviour point = flag.GetComponent<CapturePointBehaviour>();

            using Bind bind = new(point);
            bind.Enum("kind", (int)CapturePointKind.BaseFlag)
                .Ref("owningBase", owner);
        }

        private static void EnsureCentralFlag(Transform root, List<string> report)
        {
            CapturePointBehaviour[] points = Object.FindObjectsByType<CapturePointBehaviour>(FindObjectsInactive.Include);

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].Kind == CapturePointKind.CentralFlag)
                    return;
            }

            GameObject flag = new("Flag_Center", typeof(NetworkObject), typeof(CapturePointBehaviour));
            flag.transform.SetParent(root, false);
            flag.transform.position = Vector3.zero;

            CapturePointBehaviour point = flag.GetComponent<CapturePointBehaviour>();

            using (Bind bind = new(point))
                bind.Enum("kind", (int)CapturePointKind.CentralFlag);

            Undo.RegisterCreatedObjectUndo(flag, "Create Central Flag");
            report.Add("создан центральный флаг в начале координат");
        }

        private static PlayerBase FindBase(int slot)
        {
            PlayerBase[] bases = Object.FindObjectsByType<PlayerBase>(FindObjectsInactive.Include);

            for (int i = 0; i < bases.Length; i++)
            {
                if (bases[i].Slot == slot)
                    return bases[i];
            }

            return null;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 worldPosition)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.position = worldPosition;
            return child.transform;
        }
    }
}
