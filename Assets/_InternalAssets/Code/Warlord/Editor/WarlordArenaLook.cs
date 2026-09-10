using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Общий вид арены: свет, небо, дальность теней и детализация блокаута.
    ///
    /// Профиль пост-обработки этот скрипт не трогает намеренно —
    /// <see cref="WarlordPostProcessingSetup"/> выставляет его по ГДД §4, где читаемость
    /// толпы поставлена выше сочности. Здесь правится только то, что на различимость
    /// команд не влияет: геометрия света, горизонт и микрорельеф поверхностей.
    ///
    /// Материалы называются Blockout_* — арена сейчас блокаут, и задача не «нарисовать арт»,
    /// а довести серые коробки до состояния, в котором читается масштаб и форма.
    /// </summary>
    public static class WarlordArenaLook
    {
        private const string TextureFolder = "Assets/_InternalAssets/Art/Textures";
        private const string DetailPath = TextureFolder + "/Blockout_Detail.png";
        private const string MaterialFolder = "Assets/_InternalAssets/Art/Materials";
        private const string SkyboxPath = MaterialFolder + "/Warlord_Sky.mat";
        private const string BlockoutFolder = MaterialFolder + "/Blockout";

        private const int TextureSize = 512;

        /// <summary>
        /// Число повторов текстуры на UV-развёртку меша. Задано напрямую, а не пересчётом
        /// из мировых единиц: развёртки у объектов блокаута разные, и общей формулы,
        /// которая одинаково годилась бы и земле, и камню, тут нет.
        /// </summary>
        private static readonly Dictionary<string, float> Tiling = new()
        {
            { "Blockout_Ground", 24f },
            { "Blockout_Hill", 12f },
            { "Blockout_Rock", 4f },
            { "Blockout_Landmark", 6f },
            { "Blockout_Outpost", 8f },
            { "Blockout_Base_0", 10f },
            { "Blockout_Base_1", 10f },
            { "Blockout_Base_2", 10f },
            { "Blockout_Base_3", 10f },
        };

        [MenuItem("Warlord/Настройка/17. Вид арены: свет, небо, детали", priority = 16)]
        public static void Build()
        {
            var report = new List<string>();

            Texture2D detail = EnsureDetailTexture(report);
            ApplyToBlockoutMaterials(detail, report);
            ConfigureSky(report);
            ConfigureSun(report);
            ConfigureShadowDistance(report);
            ConfigureAmbientOcclusion(report);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("Warlord: вид арены обновлён\n" + string.Join("\n", report));
        }

        #region Текстура детали

        /// <summary>
        /// Бесшовная текстура микрорельефа: мягкий шум плюс еле заметная сетка.
        /// Без неё блокаут — заливка одним цветом, на которой глаз не считывает ни размер
        /// поверхности, ни расстояние до неё.
        /// </summary>
        private static Texture2D EnsureDetailTexture(List<string> report)
        {
            Directory.CreateDirectory(TextureFolder);

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float value = 0f;
                    float amplitude = 0.6f;
                    int period = 8;

                    // Три октавы: крупные пятна задают неровность земли, мелкие — зерно.
                    for (int octave = 0; octave < 3; octave++)
                    {
                        value += Noise(x, y, period) * amplitude;
                        amplitude *= 0.5f;
                        period *= 2;
                    }

                    // Шум идёт вокруг единицы и только притемняет: цвет материала задаётся
                    // в _BaseColor, а текстура обязана его не перекрашивать. Размах ±20%:
                    // при ±14% на экране в игре рельеф не читался вовсе.
                    float shade = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(value));

                    shade *= Grid(x, y);

                    byte channel = (byte)(Mathf.Clamp01(shade) * 255f);
                    pixels[y * TextureSize + x] = new Color32(channel, channel, channel, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(DetailPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(DetailPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(DetailPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            report.Add("текстура детали: " + DetailPath);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(DetailPath);
        }

        /// <summary>Бесшовный value-noise: решётка замыкается по модулю периода.</summary>
        private static float Noise(int x, int y, int period)
        {
            float cell = TextureSize / (float)period;

            float fx = x / cell;
            float fy = y / cell;

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);

            float tx = Smooth(fx - x0);
            float ty = Smooth(fy - y0);

            float a = Hash(x0, y0, period);
            float b = Hash(x0 + 1, y0, period);
            float c = Hash(x0, y0 + 1, period);
            float d = Hash(x0 + 1, y0 + 1, period);

            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;

            int hash = x * 374761393 + y * 668265263 + period * 1442695040;
            hash = (hash ^ (hash >> 13)) * 1274126177;
            hash ^= hash >> 16;

            return (hash & 0xFFFF) / 65535f;
        }

        /// <summary>Слабая сетка: даёт глазу опорные линии для оценки расстояния.</summary>
        private static float Grid(int x, int y)
        {
            const int Step = TextureSize / 4;
            const float Depth = 0.05f;

            int dx = Mathf.Min(x % Step, Step - x % Step);
            int dy = Mathf.Min(y % Step, Step - y % Step);

            bool line = dx < 1 || dy < 1;

            return line ? 1f - Depth : 1f;
        }

        private static void ApplyToBlockoutMaterials(Texture2D detail, List<string> report)
        {
            if (detail == null)
                return;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { BlockoutFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || !material.HasProperty("_BaseMap"))
                    continue;

                string name = Path.GetFileNameWithoutExtension(path);
                float tiles = Tiling.TryGetValue(name, out float value) ? value : 8f;

                material.SetTexture("_BaseMap", detail);
                material.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));

                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.06f);

                EditorUtility.SetDirty(material);
                report.Add("деталь на материале: " + name + " (повторов " + tiles + ")");
            }
        }

        #endregion

        #region Небо, солнце, тени

        /// <summary>
        /// Своё процедурное небо вместо дефолтного. Дефолт даёт ровную синеву без горизонта,
        /// и арена выглядит вырезанной из фона; горизонт под цвет земли склеивает их вместе.
        /// </summary>
        private static void ConfigureSky(List<string> report)
        {
            Shader gradient = Shader.Find("Warlord/Gradient Sky");

            if (gradient == null)
            {
                report.Add("шейдер неба не найден — небо оставлено как есть");
                return;
            }

            var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);

            if (skybox == null)
            {
                Directory.CreateDirectory(MaterialFolder);
                skybox = new Material(gradient);
                AssetDatabase.CreateAsset(skybox, SkyboxPath);
                report.Add("создано небо " + SkyboxPath);
            }

            skybox.shader = gradient;

            // Горизонт светлый и чуть тёплый — под цвет тумана и песка арены;
            // зенит спокойный синий, чтобы небо не перетягивало на себя внимание
            // и не спорило с командными цветами.
            skybox.SetColor("_TopColor", new Color(0.20f, 0.42f, 0.76f));
            skybox.SetColor("_HorizonColor", new Color(0.76f, 0.81f, 0.86f));
            skybox.SetColor("_BottomColor", new Color(0.42f, 0.40f, 0.35f));

            // Меньше единицы — переход к синеве идёт круто у самого горизонта. Игровая
            // камера смотрит почти горизонтально и видит только нижнюю полосу неба:
            // при плавном переходе (1.6) она целиком оставалась белёсой, без синевы вовсе.
            skybox.SetFloat("_HorizonSharpness", 0.5f);
            skybox.SetFloat("_HorizonHeight", 0f);
            skybox.SetFloat("_Exposure", 1f);

            EditorUtility.SetDirty(skybox);

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.05f;

            // Туман — ровно цвет горизонта, иначе на стыке дали и неба видна полоса.
            RenderSettings.fogColor = new Color(0.74f, 0.79f, 0.84f);

            DynamicGI.UpdateEnvironment();

            report.Add("небо и ambient настроены");
        }

        /// <summary>
        /// Солнце пониже и теплее. Отвесный белый свет давал короткую тень прямо под
        /// объектом, и коробки блокаута читались как плоские пятна.
        /// </summary>
        private static void ConfigureSun(List<string> report)
        {
            Light sun = null;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional)
                    continue;

                sun = light;
                break;
            }

            if (sun == null)
            {
                report.Add("направленный свет в сцене не найден");
                return;
            }

            sun.transform.rotation = Quaternion.Euler(44f, -34f, 0f);
            sun.color = new Color(1f, 0.957f, 0.886f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;

            // Тень не в ноль: в тени стоят юниты, и их командный цвет обязан читаться (ГДД §4.5).
            sun.shadowStrength = 0.78f;

            EditorUtility.SetDirty(sun);
            report.Add("солнце: угол 44°, тёплый тон, тень 0.78");
        }

        /// <summary>
        /// Дальность теней под размер арены. При 50 метрах тени обрывались сразу за базой,
        /// и середина карты выглядела нарисованной на плоскости.
        /// </summary>
        private static void ConfigureShadowDistance(List<string> report)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);

                if (asset == null)
                    continue;

                var serialized = new SerializedObject(asset);
                SerializedProperty distance = serialized.FindProperty("m_ShadowDistance");

                if (distance == null)
                    continue;

                distance.floatValue = 130f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);

                report.Add("дальность теней 130: " + Path.GetFileNameWithoutExtension(path));
            }
        }

        /// <summary>
        /// Радиус SSAO под масштаб сцены. При 0.3 затенение собиралось в трёх сантиметрах
        /// от контакта — на юните ростом два метра его просто не видно.
        /// </summary>
        private static void ConfigureAmbientOcclusion(List<string> report)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null || !asset.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                        continue;

                    var serialized = new SerializedObject(asset);

                    bool changed = SetLeaf(serialized, "Radius", 0.75f)
                                   | SetLeaf(serialized, "Intensity", 0.8f)
                                   | SetLeaf(serialized, "Falloff", 120f);

                    if (!changed)
                        continue;

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);

                    report.Add("SSAO подстроен: " + Path.GetFileNameWithoutExtension(path));
                }
            }
        }

        /// <summary>
        /// Ищет свойство по имени листа на любой глубине: поля SSAO лежат во вложенной
        /// структуре настроек, и её путь между версиями URP менялся.
        /// </summary>
        private static bool SetLeaf(SerializedObject serialized, string leafName, float value)
        {
            SerializedProperty iterator = serialized.GetIterator();

            while (iterator.NextVisible(true))
            {
                if (iterator.name != leafName || iterator.propertyType != SerializedPropertyType.Float)
                    continue;

                iterator.floatValue = value;
                return true;
            }

            return false;
        }

        #endregion
    }
}
