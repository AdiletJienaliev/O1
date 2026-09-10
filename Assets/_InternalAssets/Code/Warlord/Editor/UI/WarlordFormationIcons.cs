using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Иконки построений. В наборе GUI PRO таких нет, а подставлять вместо строя шлем или
    /// флаг хуже, чем оставить пусто: игрок читает иконку как смысл. Поэтому рисуем сами —
    /// строй нагляднее всего показывается расстановкой точек, как его и рисуют в тактиках.
    ///
    /// Точки белые: цвет задаёт кнопка через tint, поэтому одна и та же иконка годится
    /// и в активном, и в приглушённом состоянии.
    /// </summary>
    public static class WarlordFormationIcons
    {
        private const string IconFolder = "Assets/_InternalAssets/Art/UI/Formations";
        private const string ConfigFolder = "Assets/_InternalAssets/Configs";

        private const int Size = 64;
        private const float DotRadius = 5.5f;

        [MenuItem("Warlord/UI/4. Сгенерировать иконки (построения, миникарта)", priority = 3)]
        public static void Build()
        {
            Directory.CreateDirectory(IconFolder);

            var report = new List<string>();

            // Координаты в долях иконки: 0..1, начало в левом нижнем углу.
            Write("Formation_Square", SquareDots(), report);
            Write("Formation_Wedge", WedgeDots(), report);
            Write("Formation_Line", LineDots(), report);

            WriteArrow(report);

            AssetDatabase.Refresh();

            Assign("Formation_Square", report);
            Assign("Formation_Wedge", report);
            Assign("Formation_Line", report);

            AssetDatabase.SaveAssets();

            Debug.Log("Warlord: иконки построений готовы\n" + string.Join("\n", report));
        }

        /// <summary>Каре: плотный квадрат три на три.</summary>
        private static List<Vector2> SquareDots()
        {
            var dots = new List<Vector2>();
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    dots.Add(new Vector2(0.28f + column * 0.22f, 0.28f + row * 0.22f));
            return dots;
        }

        /// <summary>Клин: остриё вверх, ряды по 1, 2, 3.</summary>
        private static List<Vector2> WedgeDots()
        {
            var dots = new List<Vector2> { new(0.5f, 0.78f) };

            dots.Add(new Vector2(0.36f, 0.5f));
            dots.Add(new Vector2(0.64f, 0.5f));

            dots.Add(new Vector2(0.22f, 0.22f));
            dots.Add(new Vector2(0.5f, 0.22f));
            dots.Add(new Vector2(0.78f, 0.22f));

            return dots;
        }

        /// <summary>Линия: широкий мелкий строй в один ряд.</summary>
        private static List<Vector2> LineDots()
        {
            var dots = new List<Vector2>();
            for (int i = 0; i < 5; i++)
                dots.Add(new Vector2(0.14f + i * 0.18f, 0.5f));
            return dots;
        }

        /// <summary>
        /// Стрелка своего полководца на миникарте. Круглая точка не показывает разворот,
        /// а в игре от третьего лица направление обзора — половина ориентирования.
        /// Остриё смотрит вверх: виджет доворачивает метку по азимуту камеры.
        /// </summary>
        private static void WriteArrow(List<string> report)
        {
            const int Size = 64;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            // Треугольник: вершина сверху по центру, основание внизу с выемкой,
            // чтобы стрелка не выглядела бумажным самолётиком.
            var tip = new Vector2(0.5f, 0.94f);
            var left = new Vector2(0.10f, 0.06f);
            var right = new Vector2(0.90f, 0.06f);
            var notch = new Vector2(0.5f, 0.34f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var point = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);

                    bool inside = InTriangle(point, tip, left, notch) || InTriangle(point, tip, notch, right);

                    pixels[y * Size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            string path = IconFolder + "/Minimap_Hero.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            report.Add("нарисовано: " + path);
        }

        private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float Side(Vector2 p, Vector2 from, Vector2 to)
                => (to.x - from.x) * (p.y - from.y) - (to.y - from.y) * (p.x - from.x);

            float ab = Side(point, a, b);
            float bc = Side(point, b, c);
            float ca = Side(point, c, a);

            bool negative = ab < 0f || bc < 0f || ca < 0f;
            bool positive = ab > 0f || bc > 0f || ca > 0f;

            return !(negative && positive);
        }

        private static void Write(string name, List<Vector2> dots, List<string> report)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float nearest = float.MaxValue;

                    foreach (Vector2 dot in dots)
                    {
                        float dx = x + 0.5f - dot.x * Size;
                        float dy = y + 0.5f - dot.y * Size;
                        nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                    }

                    // Мягкий край в один пиксель: иконка живёт на 60×60 кнопке и без
                    // сглаживания точки выглядят рваными.
                    float alpha = Mathf.Clamp01(DotRadius + 0.5f - nearest);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            string path = IconFolder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            report.Add("нарисовано: " + path);
        }

        private static void Assign(string name, List<string> report)
        {
            string configPath = ConfigFolder + "/" + name + ".asset";
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(configPath);

            if (config == null)
            {
                report.Add("конфиг не найден: " + configPath);
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/" + name + ".png");

            if (sprite == null)
            {
                report.Add("спрайт не импортировался: " + name);
                return;
            }

            var serialized = new SerializedObject(config);
            SerializedProperty icon = serialized.FindProperty("icon");

            if (icon == null)
            {
                report.Add("у конфига нет поля icon: " + name);
                return;
            }

            icon.objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            report.Add("назначено: " + name);
        }
    }
}
