using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using Warlord.Core;
using Warlord.EditorTools.UI;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.World;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Блокаут карты на четырёх игроков (ГДД §3). Собирается кодом по трём причинам.
    ///
    /// Первая — симметрия. По ГДД §3.8 карта строится одной четвертью и размножается поворотом
    /// на 90°, и только так симметрия гарантирована геометрически, а не на глаз. Руками
    /// четверть тоже можно повернуть, но любая последующая правка ломает три копии из четырёх.
    ///
    /// Вторая — числа. Координаты, ширины проходов и высоты укрытий заданы в ГДД §3.3 и §3.5
    /// точными значениями. В коде их видно списком и можно сверить с документом за минуту.
    ///
    /// Третья — повторяемость. Сцена пересобирается с нуля одной кнопкой, поэтому блокаут
    /// не жалко выбросить и построить заново после первого же забега с секундомером.
    ///
    /// Это именно блокаут: серые коробки нужного размера в нужных местах. Художественная
    /// геометрия ставится поверх, когда метрика проверена.
    /// </summary>
    public static class WarlordArenaSetup
    {
        private const string ScenePath = "Assets/_InternalAssets/Scenes/Battle_Arena4.unity";
        private const string SourceScenePath = "Assets/_InternalAssets/Scenes/SampleScene.unity";
        private const string MaterialFolder = "Assets/_InternalAssets/Art/Materials/Blockout";

        #region Метрика из ГДД §3.3 и §3.5

        /// <summary>Сторона арены, м.</summary>
        private const float ArenaSize = 160f;

        /// <summary>Отступ от края играбельной зоны до преграды, м (ГДД §3.5).</summary>
        private const float BorderMargin = 10f;

        /// <summary>Позиция базы по каждой оси: угол (+70, +70) и его повороты.</summary>
        private const float BaseOffset = 70f;

        /// <summary>Расстояние аванпоста от центра, м.</summary>
        private const float OutpostOffset = 55f;

        /// <summary>Высота центрального холма, м.</summary>
        private const float HillHeight = 3f;

        /// <summary>Радиус плоской вершины холма. Вокруг флага открытая площадка (ГДД §3.6).</summary>
        private const float HillRadius = 14f;

        /// <summary>Длина подъёма на холм. При высоте 3 м даёт уклон около 17° — в норме ≤ 22°.</summary>
        private const float RampLength = 10f;

        /// <summary>Ширина главного прохода к центру, м. Линия из 6 юнитов проходит с трудом.</summary>
        private const float MainGateWidth = 11f;

        /// <summary>Высота глухого укрытия: выше полководца, стрелы не проходят.</summary>
        private const float TallCoverHeight = 2.5f;

        /// <summary>Высота низкого укрытия: лучников блокирует, видно поверх.</summary>
        private const float LowCoverHeight = 1.2f;

        /// <summary>Сторона плоской площадки базы, м.</summary>
        private const float BasePlatformSize = 30f;

        /// <summary>Сторона площадки аванпоста, м.</summary>
        private const float OutpostPlatformSize = 20f;

        #endregion

        [MenuItem("Warlord/Настройка/13. Создать сцену-арену на 4 игроков", priority = 12)]
        public static void BuildArena()
        {
            if (!EditorUtility.DisplayDialog(
                    "Сборка арены",
                    "Сцена " + Path.GetFileName(ScenePath) + " будет собрана заново: старая геометрия "
                    + "мира в ней удаляется, сетевые объекты и UI сохраняются.\n\nПродолжить?",
                    "Собрать", "Отмена"))
            {
                return;
            }

            List<string> report = new();
            Scene scene = OpenOrCreateScene(report);

            StripWorld(report);

            Transform world = new GameObject("--- World ---").transform;

            BuildArenaBounds(world, report);
            BuildGround(world);
            BuildCenter(world, report);

            // Одна четверть и три её поворота: симметрия получается геометрически (ГДД §3.8).
            for (int quarter = 0; quarter < 4; quarter++)
                BuildQuarter(world, quarter * 90f, report);

            BuildLandmarks(world, report);
            BuildNavMesh(world, report);

            WarlordPostProcessingSetup.ApplyToScene(report);

            AssignSceneIds(scene, report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings(report);

            AssetDatabase.SaveAssets();

            Debug.Log("Warlord: арена на 4 игроков собрана в " + ScenePath + ":\n"
                      + string.Join("\n", report)
                      + "\n\nОсталось вручную: запечь NavMesh (кнопка Bake на объекте NavMesh) "
                      + "и пробежать маршруты из ГДД §3.3 с секундомером.");
        }

        #region Сцена

        /// <summary>
        /// Новая сцена делается копией боевой: в ней уже собраны NetworkManager, MatchManager,
        /// спавнер и UI, а собирать эту обвязку заново — самый надёжный способ получить сцену,
        /// которая выглядит правильно и не запускается.
        /// </summary>
        private static Scene OpenOrCreateScene(List<string> report)
        {
            if (File.Exists(ScenePath))
            {
                report.Add("сцена " + ScenePath + " открыта заново");
                return EditorSceneManager.OpenScene(ScenePath);
            }

            if (!File.Exists(SourceScenePath))
            {
                report.Add("исходной сцены " + SourceScenePath + " нет — создана пустая");
                return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            AssetDatabase.CopyAsset(SourceScenePath, ScenePath);
            AssetDatabase.ImportAsset(ScenePath);

            report.Add("сцена создана копией " + Path.GetFileName(SourceScenePath)
                       + " — сетевая обвязка и UI перенесены как есть");

            return EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Сносит старый мир, оставляя всё, что к геометрии не относится. Список сохраняемого
        /// задан типами компонентов, а не именами: имена в сцене меняются, а NetworkManager
        /// остаётся NetworkManager.
        /// </summary>
        private static void StripWorld(List<string> report)
        {
            Scene scene = SceneManager.GetActiveScene();
            List<GameObject> doomed = new();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsWorthKeeping(root))
                    continue;

                doomed.Add(root);
            }

            for (int i = 0; i < doomed.Count; i++)
                Object.DestroyImmediate(doomed[i]);

            if (doomed.Count > 0)
                report.Add("удалено корневых объектов старого мира: " + doomed.Count);
        }

        /// <summary>Оставляем всё, что не является геометрией: сеть, UI, камеру и свет.</summary>
        private static bool IsWorthKeeping(GameObject root)
        {
            // Мир пересобирается целиком, поэтому базы, флаги и арена уходят даже если
            // висят на объекте с полезным компонентом — иначе получим два комплекта точек.
            if (root.GetComponentInChildren<CapturePointBehaviour>(true) != null
                || root.GetComponentInChildren<PlayerBase>(true) != null
                || root.GetComponentInChildren<MatchArena>(true) != null
                || root.GetComponentInChildren<NavMeshSurface>(true) != null)
            {
                return false;
            }

            return root.GetComponentInChildren<Camera>(true) != null
                || root.GetComponentInChildren<Light>(true) != null
                || root.GetComponentInChildren<Canvas>(true) != null
                || root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null
                || root.GetComponentInChildren<FishNet.Managing.NetworkManager>(true) != null
                || root.GetComponentInChildren<Warlord.Gameplay.Match.MatchManager>(true) != null
                || root.GetComponentInChildren<Warlord.Gameplay.Players.WarlordPlayerSpawner>(true) != null
                || root.GetComponentInChildren<Warlord.Networking.NetworkBootstrap>(true) != null
                || root.GetComponentInChildren<Warlord.Networking.Lobby.LobbyManager>(true) != null;
        }

        /// <summary>
        /// Раздаёт сетевым объектам сцены их SceneId.
        ///
        /// Само это делает FishNet в <c>OnValidate</c>, но там стоит защита от частых пересборок:
        /// не чаще раза в 250 мс. Мы создаём девять флагов за один кадр, поэтому идентификатор
        /// получает первый, а остальные остаются с нулём — и матч встречает их ошибкой
        /// «is expected to be initialized but was not».
        ///
        /// Метод FishNet внутренний, поэтому зовём его через рефлексию: своя генерация
        /// идентификаторов разошлась бы с фишнетовской при первом же их изменении.
        /// Не получилось — честно говорим, каким пунктом меню это чинится руками.
        /// </summary>
        private static void AssignSceneIds(Scene scene, List<string> report)
        {
            const string Manual = "SceneId сетевым объектам не розданы — выполните "
                                  + "Tools > Fish-Networking > Utility > Reserialize NetworkObjects "
                                  + "с галочкой Reserialize Scenes";

            System.Reflection.MethodInfo method = typeof(NetworkObject).GetMethod(
                "CreateSceneId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() },
                null);

            if (method == null)
            {
                report.Add(Manual);
                return;
            }

            try
            {
                object[] arguments = { scene, true, 0 };
                method.Invoke(null, arguments);
                report.Add("SceneId розданы сетевым объектам сцены: изменено " + arguments[2]);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Warlord: " + Manual + "\n" + exception.Message);
                report.Add(Manual);
            }
        }

        private static void RegisterInBuildSettings(List<string> report)
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            report.Add("сцена добавлена в Build Settings — без этого сервер не сможет её загрузить");
        }

        #endregion

        #region Геометрия

        private static void BuildArenaBounds(Transform world, List<string> report)
        {
            GameObject arena = new("Arena", typeof(MatchArena));
            arena.transform.SetParent(world, false);

            SerializedObject serialized = new(arena.GetComponent<MatchArena>());
            serialized.FindProperty("displayName").stringValue = "Арена четырёх";
            serialized.FindProperty("size").floatValue = ArenaSize;
            serialized.FindProperty("heightRange").vector2Value = new Vector2(-10f, 40f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Невидимые стены ставятся по краю играбельной зоны, визуальная преграда — за ней
            // (ГДД §3.6). Полководец не должен упираться в пустоту без объяснения, поэтому
            // за коллайдером стоит видимая скальная гряда.
            Transform bounds = new GameObject("Bounds").transform;
            bounds.SetParent(world, false);

            float half = ArenaSize * 0.5f;

            for (int side = 0; side < 4; side++)
            {
                float angle = side * 90f;
                Quaternion rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 wallCenter = rotation * new Vector3(0f, 6f, half);
                GameObject wall = CreateBox(bounds, "Invisible_" + side, wallCenter, rotation,
                    new Vector3(ArenaSize, 12f, 1f), null);

                wall.GetComponent<MeshRenderer>().enabled = false;

                Vector3 ridgeCenter = rotation * new Vector3(0f, 5f, half + BorderMargin);
                CreateBox(bounds, "Ridge_" + side, ridgeCenter, rotation,
                    new Vector3(ArenaSize + BorderMargin * 2f, 10f, 8f), Palette.Rock);
            }

            report.Add("арена " + ArenaSize + "×" + ArenaSize + " м, преграда в " + BorderMargin + " м за краем");
        }

        private static void BuildGround(Transform world)
        {
            CreateBox(world, "Ground", new Vector3(0f, -0.5f, 0f), Quaternion.identity,
                new Vector3(ArenaSize + BorderMargin * 2f, 1f, ArenaSize + BorderMargin * 2f), Palette.Ground);
        }

        /// <summary>
        /// Центральный холм (ГДД §3.6). Тело холма — цилиндр с вертикальной стенкой: она даёт
        /// те самые непроходимые склоны, и заходить на центр приходится через четыре подъёма,
        /// а не откуда попало. Отдельного «делаем склоны крутыми» тут не нужно — так работает
        /// сама форма.
        /// </summary>
        private static void BuildCenter(Transform world, List<string> report)
        {
            Transform center = new GameObject("Center").transform;
            center.SetParent(world, false);

            GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hill.name = "Hill";
            hill.transform.SetParent(center, false);
            hill.transform.localPosition = new Vector3(0f, HillHeight * 0.5f, 0f);
            hill.transform.localScale = new Vector3(HillRadius * 2f, HillHeight * 0.5f, HillRadius * 2f);
            Paint(hill, Palette.Hill);

            // Примитив-цилиндр приезжает с капсульным коллайдером, а капсула шириной 28 м —
            // это купол, а не холм с плоской вершиной: NavMesh запёкся бы по горбу, и флаг
            // на вершине оказался бы недостижим. Меняем на меш самой формы.
            if (hill.TryGetComponent(out Collider capsule))
                Object.DestroyImmediate(capsule);

            MeshCollider hillCollider = hill.AddComponent<MeshCollider>();
            hillCollider.sharedMesh = hill.GetComponent<MeshFilter>().sharedMesh;

            for (int side = 0; side < 4; side++)
            {
                Quaternion rotation = Quaternion.Euler(0f, side * 90f, 0f);
                float slope = Mathf.Atan2(HillHeight, RampLength) * Mathf.Rad2Deg;

                GameObject ramp = CreateBox(
                    center,
                    "Ramp_" + side,
                    rotation * new Vector3(0f, HillHeight * 0.5f, HillRadius + RampLength * 0.5f),
                    rotation * Quaternion.Euler(slope, 0f, 0f),
                    new Vector3(MainGateWidth, 0.5f, Mathf.Sqrt(RampLength * RampLength + HillHeight * HillHeight)),
                    Palette.Hill);

                ramp.isStatic = true;
            }

            CreateFlag(center, "Flag_Center", new Vector3(0f, HillHeight, 0f), CapturePointKind.CentralFlag, null);

            report.Add("центр: холм " + HillHeight + " м, четыре подъёма шириной " + MainGateWidth + " м");
        }

        /// <summary>
        /// Одна четверть карты: база в углу, аванпост на оси и укрытия между ними.
        /// Все координаты записаны для четверти с поворотом 0 и приезжают в остальные три
        /// поворотом родителя — править нужно ровно одно место.
        /// </summary>
        private static void BuildQuarter(Transform world, float yaw, List<string> report)
        {
            Transform quarter = new GameObject("Quarter_" + Mathf.RoundToInt(yaw)).transform;
            quarter.SetParent(world, false);
            quarter.rotation = Quaternion.Euler(0f, yaw, 0f);

            BuildBase(quarter, report);
            BuildOutpost(quarter, report);
            BuildCover(quarter);
        }

        private static void BuildBase(Transform quarter, List<string> report)
        {
            Vector3 localCenter = new(BaseOffset, 0f, BaseOffset);
            Vector3 worldCenter = quarter.TransformPoint(localCenter);

            int slot = ResolveSlot(worldCenter);

            GameObject baseObject = new("Base_" + slot);
            baseObject.transform.SetParent(quarter, false);
            baseObject.transform.localPosition = localCenter;

            // База развёрнута к центру карты: по её углу разворачивается стартовый строй армии.
            Vector3 toCenter = -worldCenter;
            baseObject.transform.rotation = Quaternion.LookRotation(new Vector3(toCenter.x, 0f, toCenter.z));

            CreateBox(baseObject.transform, "Platform", new Vector3(0f, -0.25f, 0f), Quaternion.identity,
                new Vector3(BasePlatformSize, 0.5f, BasePlatformSize), Palette.Slot(slot));

            BuildBaseWalls(baseObject.transform, slot);

            Transform heroSpawn = CreateChild(baseObject.transform, "HeroSpawn", new Vector3(0f, 0f, 4f));
            Transform unitSpawn = CreateChild(baseObject.transform, "UnitSpawn", new Vector3(0f, 0f, 9f));

            PlayerBase playerBase = baseObject.AddComponent<PlayerBase>();

            using (Bind bind = new(playerBase))
            {
                bind.Int("slot", slot)
                    .Ref("heroSpawnPoint", heroSpawn)
                    .Ref("unitSpawnPoint", unitSpawn)
                    .Float("buyZoneRadius", 12f);
            }

            CreateFlag(baseObject.transform, "Flag_Base", new Vector3(0f, 0f, -6f),
                CapturePointKind.BaseFlag, playerBase);

            report.Add("база слота " + slot + " в " + Format(worldCenter));
        }

        /// <summary>
        /// Стена базы с одним широким проёмом в сторону карты. Нужна не для геймплея,
        /// а для читаемости (ГДД §3.6): без стены плоская площадка не выглядит базой,
        /// и игрок не понимает, где кончается его земля.
        /// </summary>
        private static void BuildBaseWalls(Transform parent, int slot)
        {
            const float WallHeight = 4f;
            const float GateWidth = 12f;

            float half = BasePlatformSize * 0.5f;
            float wing = (BasePlatformSize - GateWidth) * 0.5f;

            // Фронтальная стена разорвана воротами посередине.
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox(parent, "Wall_Front_" + side,
                    new Vector3(side * (half - wing * 0.5f), WallHeight * 0.5f, half),
                    Quaternion.identity,
                    new Vector3(wing, WallHeight, 1.5f), Palette.Slot(slot));
            }

            CreateBox(parent, "Wall_Left", new Vector3(-half, WallHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(1.5f, WallHeight, BasePlatformSize), Palette.Slot(slot));

            CreateBox(parent, "Wall_Right", new Vector3(half, WallHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(1.5f, WallHeight, BasePlatformSize), Palette.Slot(slot));
        }

        /// <summary>
        /// Аванпост: ровная площадка, флаг в середине и глухие укрытия по краям, чтобы
        /// гарнизон не расстреливали с дистанции (ГДД §3.6). Кольцо охранников радиусом
        /// 4.5 м помещается внутри площадки 20×20 с запасом.
        /// </summary>
        private static void BuildOutpost(Transform quarter, List<string> report)
        {
            Vector3 localCenter = new(OutpostOffset, 0f, 0f);
            Vector3 worldCenter = quarter.TransformPoint(localCenter);

            GameObject outpost = new("Outpost_" + Compass(worldCenter));
            outpost.transform.SetParent(quarter, false);
            outpost.transform.localPosition = localCenter;

            CreateBox(outpost.transform, "Platform", new Vector3(0f, -0.15f, 0f), Quaternion.identity,
                new Vector3(OutpostPlatformSize, 0.3f, OutpostPlatformSize), Palette.Outpost);

            // Три глухих укрытия по краям площадки. Четвёртая сторона оставлена открытой —
            // это основной подход, и он должен читаться как вход, а не как щель.
            CreateBox(outpost.transform, "Cover_A", new Vector3(-7f, TallCoverHeight * 0.5f, 5f),
                Quaternion.identity, new Vector3(4f, TallCoverHeight, 1.5f), Palette.Rock);

            CreateBox(outpost.transform, "Cover_B", new Vector3(-7f, TallCoverHeight * 0.5f, -5f),
                Quaternion.identity, new Vector3(4f, TallCoverHeight, 1.5f), Palette.Rock);

            CreateBox(outpost.transform, "Cover_C", new Vector3(0f, LowCoverHeight * 0.5f, -8.5f),
                Quaternion.identity, new Vector3(8f, LowCoverHeight, 1.5f), Palette.Rock);

            CreateFlag(outpost.transform, "Flag_Outpost", Vector3.zero, CapturePointKind.Outpost, null);

            report.Add("аванпост " + Compass(worldCenter) + " в " + Format(worldCenter));
        }

        /// <summary>
        /// Гряды между точками. Правило ГДД §3.6: из любой точки по прямой должно быть видно
        /// не дальше 40–50 м в большинстве направлений, иначе лучники теряют смысл, а карта
        /// ощущается пустым полем.
        /// </summary>
        private static void BuildCover(Transform quarter)
        {
            Transform cover = new GameObject("Cover").transform;
            cover.SetParent(quarter, false);
            cover.localPosition = Vector3.zero;

            // Гряда вдоль диагонали база — центр, разрывающая прямую видимость от базы к холму.
            CreateBox(cover, "Ridge_Diagonal", new Vector3(42f, TallCoverHeight * 0.5f, 42f),
                Quaternion.Euler(0f, 45f, 0f), new Vector3(18f, TallCoverHeight, 2f), Palette.Rock);

            // Обходной проход шириной 6 м: строй в нём ломается, идут колонной (ГДД §3.5).
            CreateBox(cover, "Ridge_Flank_A", new Vector3(30f, TallCoverHeight * 0.5f, 8f),
                Quaternion.identity, new Vector3(2f, TallCoverHeight, 14f), Palette.Rock);

            CreateBox(cover, "Ridge_Flank_B", new Vector3(30f, TallCoverHeight * 0.5f, 28f),
                Quaternion.identity, new Vector3(2f, TallCoverHeight, 14f), Palette.Rock);

            // Низкие камни у подхода к центру: лучников блокируют, обзор оставляют.
            CreateBox(cover, "Stones_A", new Vector3(20f, LowCoverHeight * 0.5f, 20f),
                Quaternion.Euler(0f, 20f, 0f), new Vector3(6f, LowCoverHeight, 2f), Palette.Rock);

            CreateBox(cover, "Stones_B", new Vector3(14f, LowCoverHeight * 0.5f, 34f),
                Quaternion.Euler(0f, -30f, 0f), new Vector3(6f, LowCoverHeight, 2f), Palette.Rock);
        }

        /// <summary>
        /// Ориентиры по сторонам света (ГДД §3.7). При вращательной симметрии все четыре
        /// сектора выглядят одинаково, и без разных силуэтов на горизонте игрок теряется —
        /// это единственное, что спасает от «где я вообще».
        ///
        /// Стоят за играбельной зоной, чтобы не влиять на бой.
        /// </summary>
        private static void BuildLandmarks(Transform world, List<string> report)
        {
            Transform landmarks = new GameObject("Landmarks").transform;
            landmarks.SetParent(world, false);

            float ring = ArenaSize * 0.5f + BorderMargin * 0.5f;

            // Север: высокий обелиск.
            GameObject obelisk = CreateBox(landmarks, "Landmark_N_Obelisk",
                new Vector3(0f, 12f, ring), Quaternion.identity, new Vector3(4f, 24f, 4f), Palette.Landmark);
            obelisk.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            // Восток: разрушенная арка.
            Transform arch = new GameObject("Landmark_E_Arch").transform;
            arch.SetParent(landmarks, false);
            arch.localPosition = new Vector3(ring, 0f, 0f);

            CreateBox(arch, "Pillar_A", new Vector3(0f, 7f, -5f), Quaternion.identity,
                new Vector3(3f, 14f, 3f), Palette.Landmark);
            CreateBox(arch, "Pillar_B", new Vector3(0f, 5f, 5f), Quaternion.identity,
                new Vector3(3f, 10f, 3f), Palette.Landmark);
            CreateBox(arch, "Span", new Vector3(0f, 13f, -1f), Quaternion.Euler(0f, 0f, 8f),
                new Vector3(3f, 2f, 10f), Palette.Landmark);

            // Юг: мёртвое дерево.
            Transform tree = new GameObject("Landmark_S_DeadTree").transform;
            tree.SetParent(landmarks, false);
            tree.localPosition = new Vector3(0f, 0f, -ring);

            CreateBox(tree, "Trunk", new Vector3(0f, 9f, 0f), Quaternion.identity,
                new Vector3(2.5f, 18f, 2.5f), Palette.Landmark);
            CreateBox(tree, "Branch_A", new Vector3(3f, 14f, 0f), Quaternion.Euler(0f, 0f, 35f),
                new Vector3(8f, 1.2f, 1.2f), Palette.Landmark);
            CreateBox(tree, "Branch_B", new Vector3(-3f, 16f, 1f), Quaternion.Euler(10f, 0f, -40f),
                new Vector3(7f, 1.2f, 1.2f), Palette.Landmark);

            // Запад: каменный круг.
            Transform circle = new GameObject("Landmark_W_StoneCircle").transform;
            circle.SetParent(landmarks, false);
            circle.localPosition = new Vector3(-ring, 0f, 0f);

            for (int i = 0; i < 7; i++)
            {
                float angle = i / 7f * Mathf.PI * 2f;
                Vector3 position = new(Mathf.Cos(angle) * 9f, 5f, Mathf.Sin(angle) * 9f);

                CreateBox(circle, "Stone_" + i, position,
                    Quaternion.Euler(0f, angle * Mathf.Rad2Deg, i % 2 == 0 ? 6f : -5f),
                    new Vector3(2.5f, 10f, 1.8f), Palette.Landmark);
            }

            report.Add("ориентиры сторон света: обелиск (С), арка (В), дерево (Ю), круг камней (З)");
        }

        #endregion

        #region NavMesh

        /// <summary>
        /// Поверхность навигации и параметры агента из ГДД §3.6. Размер вокселя мельче
        /// стандартного намеренно: узкие обходные проходы шириной 6 м на грубой сетке
        /// либо зарастают, либо становятся шире, чем построены.
        /// </summary>
        private static void BuildNavMesh(Transform world, List<string> report)
        {
            GameObject surfaceObject = new("NavMesh");
            surfaceObject.transform.SetParent(world, false);

            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.15f;
            surface.overrideTileSize = false;
            surface.minRegionArea = 4f;

            EditorUtility.SetDirty(surface);
            ApplyAgentSettings(report);

            report.Add("NavMeshSurface: воксель 0.15 м, минимальный островок 4 м² (ГДД §3.6)");
        }

        /// <summary>
        /// Параметры агента правятся у типа по умолчанию, а не заводится новый: все префабы
        /// юнитов уже используют нулевой тип, и добавление второго означало бы обход всех
        /// префабов ради переключения поля, которое и так должно быть одно на проект.
        /// Полководцу отдельный тип не нужен — он ходит на CharacterController (ГДД §3.6).
        /// </summary>
        private static void ApplyAgentSettings(List<string> report)
        {
            Object singleton = Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings");

            if (singleton == null)
            {
                report.Add("настройки агента NavMesh не найдены — выставьте их вручную по ГДД §3.6");
                return;
            }

            SerializedObject serialized = new(singleton);
            SerializedProperty settings = serialized.FindProperty("m_Settings");

            if (settings == null || settings.arraySize == 0)
            {
                report.Add("список типов агентов пуст — выставьте параметры вручную по ГДД §3.6");
                return;
            }

            SerializedProperty agent = settings.GetArrayElementAtIndex(0);

            SetFloat(agent, "agentRadius", 0.5f);
            SetFloat(agent, "agentHeight", 2f);
            SetFloat(agent, "agentSlope", 25f);
            SetFloat(agent, "agentClimb", 0.4f);
            SetFloat(agent, "ledgeDropHeight", 0f);
            SetFloat(agent, "maxJumpAcrossDistance", 0f);
            SetFloat(agent, "minRegionArea", 4f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            report.Add("агент NavMesh: радиус 0.5, высота 2, уклон 25°, ступень 0.4 (ГДД §3.6)");
        }

        private static void SetFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);

            if (property != null)
                property.floatValue = value;
        }

        #endregion

        #region Примитивы и материалы

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 size,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = localRotation;
            box.transform.localScale = size;
            box.isStatic = true;

            if (material != null)
                Paint(box, material);

            return box;
        }

        private static void Paint(GameObject target, Material material)
        {
            if (target.TryGetComponent(out MeshRenderer renderer))
                renderer.sharedMaterial = material;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static void CreateFlag(
            Transform parent,
            string name,
            Vector3 localPosition,
            CapturePointKind kind,
            PlayerBase owner)
        {
            GameObject flag = new(name, typeof(NetworkObject), typeof(CapturePointBehaviour));
            flag.transform.SetParent(parent, false);
            flag.transform.localPosition = localPosition;

            CapturePointBehaviour point = flag.GetComponent<CapturePointBehaviour>();

            using Bind bind = new(point);
            bind.Enum("kind", (int)kind);

            if (owner != null)
                bind.Ref("owningBase", owner);
        }

        /// <summary>Серые материалы блокаута. Один набор на всю сцену, чтобы не плодить ассеты.</summary>
        private static class Palette
        {
            private static Material _ground;
            private static Material _rock;
            private static Material _hill;
            private static Material _outpost;
            private static Material _landmark;
            private static readonly Material[] Slots = new Material[PlayerSlots.MaxSupported];

            public static Material Ground => _ground ??= Load("Blockout_Ground", new Color(0.42f, 0.44f, 0.38f));
            public static Material Rock => _rock ??= Load("Blockout_Rock", new Color(0.35f, 0.34f, 0.33f));
            public static Material Hill => _hill ??= Load("Blockout_Hill", new Color(0.48f, 0.45f, 0.36f));
            public static Material Outpost => _outpost ??= Load("Blockout_Outpost", new Color(0.55f, 0.53f, 0.46f));
            public static Material Landmark => _landmark ??= Load("Blockout_Landmark", new Color(0.62f, 0.6f, 0.56f));

            /// <summary>Цвет базы виден с любой дистанции — это тоже ориентир (ГДД §3.7).</summary>
            public static Material Slot(int slot)
            {
                int index = Mathf.Clamp(slot, 0, Slots.Length - 1);

                return Slots[index] ??= Load("Blockout_Base_" + index, SlotColor(index));
            }

            private static Color SlotColor(int slot)
            {
                switch (slot)
                {
                    case 0: return new Color(0.22f, 0.38f, 0.68f);
                    case 1: return new Color(0.68f, 0.26f, 0.24f);
                    case 2: return new Color(0.28f, 0.58f, 0.32f);
                    default: return new Color(0.7f, 0.62f, 0.24f);
                }
            }

            private static Material Load(string name, Color color)
            {
                Directory.CreateDirectory(MaterialFolder);
                string path = MaterialFolder + "/" + name + ".mat";

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material != null)
                    return material;

                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                material.color = color;

                // Блокаут не должен блестеть: зеркальные коробки читаются хуже матовых,
                // а вся задача блокаута — чтобы форма была видна.
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.08f);

                AssetDatabase.CreateAsset(material, path);
                return material;
            }
        }

        #endregion

        #region Мелочи

        /// <summary>Слот по положению базы: юго-запад — нулевой, дальше по таблице ГДД §3.3.</summary>
        private static int ResolveSlot(Vector3 position)
        {
            bool east = position.x > 0f;
            bool north = position.z > 0f;

            if (!east && !north) return 0;
            if (east && !north) return 1;
            if (!east) return 2;
            return 3;
        }

        private static string Compass(Vector3 position)
        {
            if (Mathf.Abs(position.x) > Mathf.Abs(position.z))
                return position.x > 0f ? "E" : "W";

            return position.z > 0f ? "N" : "S";
        }

        private static string Format(Vector3 position)
        {
            return "(" + Mathf.RoundToInt(position.x) + ", " + Mathf.RoundToInt(position.z) + ")";
        }

        #endregion
    }
}
