using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Warlord.EditorTools.Agent
{
    /// <summary>
    /// Файловый мост между CLI-агентом и живым редактором.
    /// Читает запросы из .agent/inbox/*.req, выполняет их в главном потоке
    /// и кладёт JSON-ответ в .agent/outbox/&lt;id&gt;.res.
    /// Протокол запроса: первая строка — команда, дальше строки key=value.
    /// </summary>
    [InitializeOnLoad]
    internal static class AgentBridge
    {
        const string EnabledKey = "Warlord.AgentBridge.Enabled";
        const string MenuPath = "Warlord/Agent Bridge/Включён";
        const double PollInterval = 0.12;
        const double ShotTimeout = 6.0;
        const int MaxNodes = 6000;

        static readonly string ProjectRoot;
        static readonly string RootDir;
        static readonly string InboxDir;
        static readonly string OutboxDir;
        static readonly string ShotsDir;

        static double _nextPoll;
        static PendingShot _pending;

        sealed class PendingShot
        {
            public string Id;
            public string Path;
            public double Deadline;
            public int Width;
            public int Height;
        }

        static AgentBridge()
        {
            ProjectRoot = Directory.GetParent(Application.dataPath).FullName;
            RootDir = Path.Combine(ProjectRoot, ".agent");
            InboxDir = Path.Combine(RootDir, "inbox");
            OutboxDir = Path.Combine(RootDir, "outbox");
            ShotsDir = Path.Combine(RootDir, "shots");
            EditorApplication.update += Update;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath, false, 900)]
        static void ToggleEnabled() => Enabled = !Enabled;

        [MenuItem(MenuPath, true, 900)]
        static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        [MenuItem("Warlord/Agent Bridge/Открыть папку .agent", false, 901)]
        static void OpenFolder()
        {
            Directory.CreateDirectory(ShotsDir);
            EditorUtility.RevealInFinder(ShotsDir);
        }

        // ─────────────────────────────── цикл ───────────────────────────────

        static void Update()
        {
            if (!Enabled) return;
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + PollInterval;

            if (_pending != null)
            {
                TickPendingShot();
                return;
            }

            if (!Directory.Exists(InboxDir)) return;

            string[] files;
            try { files = Directory.GetFiles(InboxDir, "*.req"); }
            catch { return; }
            if (files.Length == 0) return;

            Array.Sort(files, StringComparer.Ordinal);
            string request = files[0];
            string id = Path.GetFileNameWithoutExtension(request);

            string body;
            try { body = File.ReadAllText(request); }
            catch { return; }
            try { File.Delete(request); } catch { /* уже подобран */ }

            try
            {
                Dispatch(id, body);
            }
            catch (Exception e)
            {
                var json = new AgentJson();
                json.BeginObj().Bool("ok", false).Str("error", e.Message).Str("stack", e.StackTrace).EndObj();
                Respond(id, json.ToString());
            }
        }

        static void Respond(string id, string json)
        {
            Directory.CreateDirectory(OutboxDir);
            string tmp = Path.Combine(OutboxDir, id + ".tmp");
            string final = Path.Combine(OutboxDir, id + ".res");
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            if (File.Exists(final)) File.Delete(final);
            File.Move(tmp, final);
        }

        static void Ok(string id, Action<AgentJson> fill = null)
        {
            var json = new AgentJson();
            json.BeginObj().Bool("ok", true);
            fill?.Invoke(json);
            json.EndObj();
            Respond(id, json.ToString());
        }

        static void Fail(string id, string message)
        {
            var json = new AgentJson();
            json.BeginObj().Bool("ok", false).Str("error", message).EndObj();
            Respond(id, json.ToString());
        }

        // ─────────────────────────── разбор запроса ───────────────────────────

        static string Parse(string body, out Dictionary<string, string> args)
        {
            args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string command = null;
            foreach (string raw in body.Split('\n'))
            {
                string line = raw.Trim('\r', ' ', '\t');
                if (line.Length == 0) continue;
                if (command == null) { command = line.ToLowerInvariant(); continue; }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                args[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return command ?? "status";
        }

        static string Arg(Dictionary<string, string> a, string key, string fallback = null)
            => a.TryGetValue(key, out var v) && v.Length > 0 ? v : fallback;

        static int ArgInt(Dictionary<string, string> a, string key, int fallback)
            => int.TryParse(Arg(a, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        static bool ArgBool(Dictionary<string, string> a, string key, bool fallback)
        {
            string v = Arg(a, key);
            if (v == null) return fallback;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        // ───────────────────────────── команды ─────────────────────────────

        static void Dispatch(string id, string body)
        {
            string cmd = Parse(body, out var args);

            switch (cmd)
            {
                case "ping": Ok(id, j => j.Str("unity", Application.unityVersion).Str("project", Application.productName)); break;
                case "status": Status(id); break;
                case "screenshot": case "shot": Screenshot(id, args); break;
                case "play": Play(id); break;
                case "stop": Stop(id); break;
                case "pause": EditorApplication.isPaused = true; Ok(id); break;
                case "resume": EditorApplication.isPaused = false; Ok(id); break;
                case "step": EditorApplication.Step(); Ok(id); break;
                case "sceneview": SceneViewCamera(id, args); break;
                case "ui": Ok(id, j => AgentUi.Dump(j, ArgBool(args, "texts", true))); break;
                case "click": Click(id, args); break;
                case "hierarchy": Hierarchy(id, args); break;
                case "inspect": Inspect(id, args); break;
                case "select": Select(id, args); break;
                case "logs": Logs(id, args); break;
                case "clearlogs": Ok(id, j => j.Bool("cleared", AgentConsole.Clear())); break;
                case "scenes": Scenes(id); break;
                case "open": OpenScene(id, args); break;
                case "menu": ExecuteMenu(id, args); break;
                case "exec": Exec(id, args); break;
                case "compile": CompilationPipeline.RequestScriptCompilation(); Ok(id); break;
                case "refresh": AssetDatabase.Refresh(); Ok(id); break;
                default: Fail(id, $"неизвестная команда: {cmd}"); break;
            }
        }

        static void Status(string id)
        {
            AgentConsole.Counts(out int errors, out int warnings, out int logs);
            Ok(id, j =>
            {
                j.Str("unity", Application.unityVersion);
                j.Bool("playing", EditorApplication.isPlaying);
                j.Bool("paused", EditorApplication.isPaused);
                j.Bool("compiling", EditorApplication.isCompiling);
                j.Bool("updating", EditorApplication.isUpdating);
                j.Bool("busy", EditorApplication.isCompiling || EditorApplication.isUpdating);
                j.Str("activeScene", SceneManager.GetActiveScene().name);
                j.Num("errors", errors).Num("warnings", warnings).Num("logs", logs);

                j.BeginArr("openScenes");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    j.BeginObj().Str("name", scene.name).Str("path", scene.path)
                     .Bool("loaded", scene.isLoaded).Bool("dirty", scene.isDirty).EndObj();
                }
                j.EndArr();

                var selection = Selection.activeGameObject;
                j.Str("selection", selection != null ? PathOf(selection.transform) : null);
            });
        }

        static void Play(string id)
        {
            if (EditorApplication.isPlaying) { Ok(id, j => j.Str("note", "уже в play mode")); return; }
            // Отвечаем до входа в play mode: перезагрузка домена убьёт состояние моста.
            Ok(id, j => j.Str("note", "вход в play mode; дождись status.playing=true"));
            EditorApplication.EnterPlaymode();
        }

        static void Stop(string id)
        {
            if (!EditorApplication.isPlaying) { Ok(id, j => j.Str("note", "уже в edit mode")); return; }
            Ok(id, j => j.Str("note", "выход из play mode"));
            EditorApplication.ExitPlaymode();
        }

        // ──────────────────────────── скриншоты ────────────────────────────

        static void Screenshot(string id, Dictionary<string, string> args)
        {
            string target = (Arg(args, "target", "game") ?? "game").ToLowerInvariant();
            int width = Mathf.Clamp(ArgInt(args, "width", 1920), 64, 3840);
            int height = Mathf.Clamp(ArgInt(args, "height", 1080), 64, 2160);
            string name = Arg(args, "name") ?? $"{target}-{DateTime.Now:HHmmss-fff}.png";
            if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name += ".png";

            Directory.CreateDirectory(ShotsDir);
            string path = Path.Combine(ShotsDir, name);
            if (File.Exists(path)) File.Delete(path);

            if (target == "game" && HasGameView())
            {
                ScreenCapture.CaptureScreenshot(path, Mathf.Clamp(ArgInt(args, "supersize", 1), 1, 4));
                _pending = new PendingShot
                {
                    Id = id,
                    Path = path,
                    Deadline = EditorApplication.timeSinceStartup + ShotTimeout,
                    Width = width,
                    Height = height,
                };
                return;
            }

            Camera camera = ResolveCamera(target, Arg(args, "camera"));
            if (camera == null) { Fail(id, $"камера не найдена для target={target}"); return; }

            RenderToFile(camera, width, height, path);
            RespondShot(id, path, "camera:" + camera.name);
        }

        static bool HasGameView()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            return type != null && Resources.FindObjectsOfTypeAll(type).Length > 0;
        }

        static Camera ResolveCamera(string target, string cameraName)
        {
            if (target == "scene")
            {
                var view = SceneView.lastActiveSceneView;
                return view != null ? view.camera : null;
            }
            if (!string.IsNullOrEmpty(cameraName))
            {
                foreach (var cam in Camera.allCameras)
                    if (cam.name == cameraName) return cam;
                return null;
            }
            return Camera.main != null ? Camera.main : Camera.allCameras.FirstOrDefault();
        }

        static void TickPendingShot()
        {
            var shot = _pending;
            if (File.Exists(shot.Path) && new FileInfo(shot.Path).Length > 0)
            {
                _pending = null;
                RespondShot(shot.Id, shot.Path, "gameview");
                return;
            }

            if (EditorApplication.timeSinceStartup > shot.Deadline)
            {
                _pending = null;
                var camera = ResolveCamera("game", null);
                if (camera == null) { Fail(shot.Id, "Game View не отрисовался и активной камеры нет"); return; }
                RenderToFile(camera, shot.Width, shot.Height, shot.Path);
                RespondShot(shot.Id, shot.Path, "camera-fallback:" + camera.name);
                return;
            }

            // Гоним кадр: в edit mode Game View сам по себе не перерисуется.
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }

        static void RespondShot(string id, string path, string method)
        {
            long size = File.Exists(path) ? new FileInfo(path).Length : 0;
            Ok(id, j => j.Str("path", path).Str("method", method).Num("bytes", size)
                         .Bool("playing", EditorApplication.isPlaying));
        }

        static void RenderToFile(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            bool submitted = false;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                try
                {
                    camera.SubmitRenderRequest(new RenderPipeline.StandardRequest { destination = rt });
                    submitted = true;
                }
                catch { submitted = false; }
            }

            if (!submitted)
            {
                var previousTarget = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = previousTarget;
            }

            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;

            File.WriteAllBytes(path, texture.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(texture);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        // ──────────────────────────── иерархия ────────────────────────────

        static void Hierarchy(string id, Dictionary<string, string> args)
        {
            int maxDepth = ArgInt(args, "depth", 6);
            bool withComponents = ArgBool(args, "components", true);
            string sceneFilter = Arg(args, "scene");
            string rootFilter = Arg(args, "root");
            int budget = MaxNodes;

            Ok(id, j =>
            {
                j.BeginArr("scenes");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    if (sceneFilter != null && scene.name != sceneFilter) continue;

                    j.BeginObj().Str("name", scene.name).Bool("dirty", scene.isDirty);
                    j.BeginArr("roots");
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (rootFilter != null && root.name != rootFilter) continue;
                        WriteNode(j, root.transform, 0, maxDepth, withComponents, ref budget);
                    }
                    j.EndArr().EndObj();
                }
                j.EndArr();
                j.Bool("truncated", budget <= 0);
            });
        }

        static void WriteNode(AgentJson j, Transform t, int depth, int maxDepth, bool withComponents, ref int budget)
        {
            if (budget-- <= 0) return;

            j.BeginObj();
            j.Str("name", t.name);
            j.Bool("active", t.gameObject.activeInHierarchy);
            if (t.gameObject.tag != "Untagged") j.Str("tag", t.gameObject.tag);

            if (withComponents)
            {
                j.BeginArr("components");
                foreach (var component in t.GetComponents<Component>())
                    j.Str(null, component == null ? "<missing script>" : component.GetType().Name);
                j.EndArr();
            }

            if (t.childCount > 0)
            {
                if (depth >= maxDepth)
                {
                    j.Num("hiddenChildren", t.childCount);
                }
                else
                {
                    j.BeginArr("children");
                    for (int i = 0; i < t.childCount; i++)
                        WriteNode(j, t.GetChild(i), depth + 1, maxDepth, withComponents, ref budget);
                    j.EndArr();
                }
            }
            j.EndObj();
        }

        static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static GameObject FindObject(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var direct = GameObject.Find(path);
            if (direct != null) return direct;

            string[] parts = path.Split('/');
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;
                    var current = root.transform;
                    for (int p = 1; p < parts.Length && current != null; p++)
                        current = current.Find(parts[p]);
                    if (current != null) return current.gameObject;
                }
            }

            // Последний шанс: поиск по имени в глубину, включая выключенные объекты.
            string leaf = parts[parts.Length - 1];
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == leaf) return t.gameObject;
            }
            return null;
        }

        static void Inspect(string id, Dictionary<string, string> args)
        {
            string path = Arg(args, "path");
            var go = FindObject(path);
            if (go == null) { Fail(id, $"объект не найден: {path}"); return; }

            string only = Arg(args, "component");
            int maxProps = ArgInt(args, "props", 60);

            Ok(id, j =>
            {
                j.Str("path", PathOf(go.transform));
                j.Bool("activeSelf", go.activeSelf);
                j.Bool("activeInHierarchy", go.activeInHierarchy);
                j.Str("tag", go.tag).Num("layer", go.layer);

                var position = go.transform.position;
                var scale = go.transform.localScale;
                j.BeginObj("transform")
                 .Num("x", position.x).Num("y", position.y).Num("z", position.z)
                 .Num("sx", scale.x).Num("sy", scale.y).Num("sz", scale.z)
                 .EndObj();

                j.BeginArr("components");
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) { j.BeginObj().Str("type", "<missing script>").EndObj(); continue; }
                    string type = component.GetType().Name;
                    if (only != null && !type.Equals(only, StringComparison.OrdinalIgnoreCase)) continue;

                    j.BeginObj().Str("type", type);
                    j.BeginObj("fields");
                    try
                    {
                        var so = new SerializedObject(component);
                        var it = so.GetIterator();
                        bool enterChildren = true;
                        int written = 0;
                        while (it.NextVisible(enterChildren) && written < maxProps)
                        {
                            enterChildren = false;
                            if (it.name == "m_Script") continue;
                            j.Str(it.name, DescribeProperty(it));
                            written++;
                        }
                    }
                    catch (Exception e)
                    {
                        j.Str("<error>", e.Message);
                    }
                    j.EndObj().EndObj();
                }
                j.EndArr();
            });
        }

        static string DescribeProperty(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.####", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return p.objectReferenceValue != null
                        ? $"{p.objectReferenceValue.name} ({p.objectReferenceValue.GetType().Name})"
                        : "null";
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length
                        ? p.enumDisplayNames[p.enumValueIndex]
                        : p.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return p.vector4Value.ToString();
                case SerializedPropertyType.Rect: return p.rectValue.ToString();
                case SerializedPropertyType.Quaternion: return p.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.ArraySize: return p.intValue.ToString(CultureInfo.InvariantCulture);
                default:
                    return p.isArray && p.propertyType != SerializedPropertyType.String
                        ? $"[{p.arraySize}]"
                        : p.propertyType.ToString();
            }
        }

        /// <summary>
        /// Ставит камеру Scene View: свободный облёт сцены, чтобы осматривать арену
        /// целиком, не двигая героя. Работает и в play mode.
        /// </summary>
        static void SceneViewCamera(string id, Dictionary<string, string> args)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) { Fail(id, "нет активного окна Scene"); return; }

            Vector3 pivot = TryVector(Arg(args, "pivot"), out Vector3 parsedPivot) ? parsedPivot : view.pivot;
            Quaternion rotation = TryVector(Arg(args, "angles"), out Vector3 angles)
                ? Quaternion.Euler(angles)
                : view.rotation;
            float size = float.TryParse(Arg(args, "size"), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSize)
                ? Mathf.Max(0.5f, parsedSize)
                : view.size;

            // В 2D-режиме Unity держит поворот единичным и молча игнорирует заданный,
            // поэтому режим снимаем до перелёта.
            view.in2DMode = false;
            view.orthographic = false;

            // Именно LookAt с instant: присваивание view.rotation запускает плавный
            // перелёт, и снимок уходит раньше, чем камера доедет.
            view.LookAt(pivot, rotation, size, false, instant: true);
            view.Repaint();

            Ok(id, j =>
            {
                j.BeginObj("pivot").Num("x", view.pivot.x).Num("y", view.pivot.y).Num("z", view.pivot.z).EndObj();
                var euler = view.rotation.eulerAngles;
                j.BeginObj("angles").Num("x", euler.x).Num("y", euler.y).Num("z", euler.z).EndObj();
                j.Num("size", view.size);
            });
        }

        static bool TryVector(string text, out Vector3 value)
        {
            value = default;
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split(',');
            if (parts.Length != 3) return false;

            return float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value.x)
                   && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value.y)
                   && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out value.z);
        }

        static void Click(string id, Dictionary<string, string> args)
        {
            string error = AgentUi.Click(Arg(args, "path"), Arg(args, "label"), out string clicked);
            if (error != null) { Fail(id, error); return; }
            Ok(id, j => j.Str("clicked", clicked));
        }

        static void Select(string id, Dictionary<string, string> args)
        {
            string path = Arg(args, "path");
            var go = FindObject(path);
            if (go == null) { Fail(id, $"объект не найден: {path}"); return; }
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Ok(id, j => j.Str("selected", PathOf(go.transform)));
        }

        // ─────────────────────────── логи и сцены ───────────────────────────

        static void Logs(string id, Dictionary<string, string> args)
        {
            int count = Mathf.Clamp(ArgInt(args, "count", 40), 1, 500);
            string level = (Arg(args, "level", "all") ?? "all").ToLowerInvariant();

            if (!AgentConsole.Available) { Fail(id, "internal-API консоли недоступен; читай Editor.log"); return; }

            var items = AgentConsole.Read(count, level);
            Ok(id, j =>
            {
                j.Num("count", items.Count);
                j.BeginArr("entries");
                foreach (var item in items)
                {
                    j.BeginObj().Str("level", item.Level).Str("message", item.Message);
                    if (!string.IsNullOrEmpty(item.File)) j.Str("file", item.File).Num("line", item.Line);
                    j.EndObj();
                }
                j.EndArr();
            });
        }

        static void Scenes(string id)
        {
            Ok(id, j =>
            {
                j.BeginArr("build");
                foreach (var scene in EditorBuildSettings.scenes)
                    j.BeginObj().Str("path", scene.path).Bool("enabled", scene.enabled).EndObj();
                j.EndArr();

                j.BeginArr("assets");
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_InternalAssets" }))
                    j.Str(null, AssetDatabase.GUIDToAssetPath(guid));
                j.EndArr();
            });
        }

        static void OpenScene(string id, Dictionary<string, string> args)
        {
            if (EditorApplication.isPlaying) { Fail(id, "нельзя открывать сцену в play mode"); return; }

            string path = Arg(args, "scene");
            if (string.IsNullOrEmpty(path)) { Fail(id, "нужен scene=<путь или имя>"); return; }

            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                string match = AssetDatabase.FindAssets("t:Scene " + path)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == path);
                if (match == null) { Fail(id, $"сцена не найдена: {path}"); return; }
                path = match;
            }

            bool force = ArgBool(args, "force", false);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                if (!force) { Fail(id, "есть несохранённые изменения сцены; передай force=true или сохрани вручную"); return; }
            }

            var opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Ok(id, j => j.Str("scene", opened.name).Str("path", opened.path));
        }

        static void ExecuteMenu(string id, Dictionary<string, string> args)
        {
            string path = Arg(args, "path");
            if (string.IsNullOrEmpty(path)) { Fail(id, "нужен path=<пункт меню>"); return; }
            bool executed = EditorApplication.ExecuteMenuItem(path);
            Ok(id, j => j.Bool("executed", executed).Str("path", path));
        }

        static void Exec(string id, Dictionary<string, string> args)
        {
            string target = Arg(args, "method");
            if (string.IsNullOrEmpty(target)) { Fail(id, "нужен method=Namespace.Type.Method"); return; }

            int dot = target.LastIndexOf('.');
            if (dot <= 0) { Fail(id, "формат: method=Namespace.Type.Method"); return; }

            string typeName = target.Substring(0, dot);
            string methodName = target.Substring(dot + 1);

            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(typeName); } catch { return null; } })
                .FirstOrDefault(t => t != null);
            if (type == null) { Fail(id, $"тип не найден: {typeName}"); return; }

            const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            string argument = Arg(args, "arg");
            var method = argument == null
                ? type.GetMethod(methodName, Flags, null, Type.EmptyTypes, null)
                : type.GetMethod(methodName, Flags, null, new[] { typeof(string) }, null);
            if (method == null) method = type.GetMethod(methodName, Flags);
            if (method == null) { Fail(id, $"статический метод не найден: {target}"); return; }

            object result = method.Invoke(null, method.GetParameters().Length == 0 ? null : new object[] { argument });
            Ok(id, j => j.Str("method", target).Str("result", result?.ToString()));
        }
    }
}
