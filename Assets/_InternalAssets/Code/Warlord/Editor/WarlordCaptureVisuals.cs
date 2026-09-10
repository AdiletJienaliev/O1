using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Warlord.Core;
using Warlord.EditorTools.UI;
using Warlord.Gameplay.Capture;
using Warlord.Gameplay.World;
using Warlord.Presentation;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Ставит флагам в сцене видимое тело. До этого у объектов Flag_* были только Transform
    /// и <see cref="CapturePointBehaviour"/>: точка работала, но игрок не видел ни её самой,
    /// ни радиуса захвата, ни владельца — на арене это просто пустое место на земле.
    ///
    /// Геометрия примитивная и намеренно: ГДД §4 требует читаемости толпы, и высокий контрастный
    /// силуэт флага с кольцом по земле читается лучше детализированной модели.
    /// </summary>
    public static class WarlordCaptureVisuals
    {
        private const string ViewNode = "View";
        private const string BuyZoneNode = "BuyZoneView";
        private const string MaterialFolder = "Assets/_InternalAssets/Art/Materials";

        [MenuItem("Warlord/Настройка/15. Визуал точек захвата и зон покупки", priority = 14)]
        public static void Build()
        {
            Material poleMaterial = EnsureOpaque("Warlord_FlagPole", new Color(0.24f, 0.22f, 0.20f));
            Material bannerMaterial = EnsureOpaque("Warlord_FlagBanner", Color.white);

            // Кольцо — контур, заливка — сплошной диск. Разные материалы: с общей текстурой
            // обода заливка прогресса тоже стала бы бубликом и перестала читаться как шкала.
            Material ringMaterial = EnsureTransparent("Warlord_CaptureRing", EnsureRingTexture());
            Material fillMaterial = EnsureTransparent("Warlord_CaptureFill", null);

            var points = Object.FindObjectsByType<CapturePointBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var report = new List<string>();

            foreach (CapturePointBehaviour point in points)
            {
                Build(point, poleMaterial, bannerMaterial, ringMaterial, fillMaterial);
                report.Add(point.name + " (" + point.Kind + ")");
            }

            int zones = BuildBuyZones(ringMaterial, report);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("Warlord: флагов " + points.Length + ", зон покупки " + zones + "\n"
                      + string.Join("\n", report));
        }

        /// <summary>Круг зоны покупки на каждой базе. Показывать его будет сам компонент.</summary>
        private static int BuildBuyZones(Material ringMaterial, List<string> report)
        {
            var bases = Object.FindObjectsByType<PlayerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (PlayerBase playerBase in bases)
            {
                Transform existing = playerBase.transform.Find(BuyZoneNode);

                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                var view = new GameObject(BuyZoneNode);
                view.transform.SetParent(playerBase.transform, false);

                Renderer ring = Primitive(
                    PrimitiveType.Cylinder, "Ring", view.transform, ringMaterial,
                    new Vector3(0f, 0.04f, 0f),
                    new Vector3(24f, 0.01f, 24f));

                // Зона покупки — разметка, а не объект: тень от неё выглядела бы грязью.
                ring.shadowCastingMode = ShadowCastingMode.Off;
                ring.receiveShadows = false;

                BuyZoneView component = view.AddComponent<BuyZoneView>();

                using (Bind bind = new(component))
                {
                    bind.Ref("owner", playerBase)
                        .Ref("ring", ring)
                        .Ref("ringTransform", ring.transform);
                }

                report.Add("зона покупки: слот " + playerBase.Slot);
            }

            return bases.Length;
        }

        private static void Build(CapturePointBehaviour point, Material pole, Material banner, Material ring, Material fill)
        {
            Transform existing = point.transform.Find(ViewNode);

            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var view = new GameObject(ViewNode);
            view.transform.SetParent(point.transform, false);

            // Центральный флаг выше базовых и аванпостов: он главная цель матча
            // и должен быть виден с другого конца арены.
            bool central = point.Kind == CapturePointKind.CentralFlag;

            // Центральный флаг — ориентир всей карты: его видно от базы через всю арену,
            // поэтому он вдвое выше остальных и с заметно более толстым древком.
            // На 4.6 м он читался с базы как тонкая палка и терялся на фоне построек.
            float height = central ? 9f : 3.6f;
            float poleRadius = central ? 0.2f : 0.13f;

            Renderer poleRenderer = Primitive(
                PrimitiveType.Cylinder, "Pole", view.transform, pole,
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(poleRadius, height * 0.5f, poleRadius));

            float bannerHeight = central ? 2.2f : 1.2f;
            float bannerWidth = central ? 3.6f : 1.8f;

            Renderer bannerRenderer = Primitive(
                PrimitiveType.Cube, "Banner", view.transform, banner,
                new Vector3(bannerWidth * 0.5f, height - bannerHeight * 0.7f, 0f),
                new Vector3(bannerWidth, bannerHeight, 0.07f));

            // Кольцо кладём чуть выше нуля: вровень с землёй оно мерцает от z-fighting.
            Renderer ringRenderer = Primitive(
                PrimitiveType.Cylinder, "Ring", view.transform, ring,
                new Vector3(0f, 0.06f, 0f),
                new Vector3(12f, 0.01f, 12f));

            // Заливка прогресса — ниже кольца на сантиметр, иначе два прозрачных диска
            // на одной высоте спорят за порядок отрисовки и мерцают.
            Renderer progressRenderer = Primitive(
                PrimitiveType.Cylinder, "Progress", view.transform, fill,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(0f, 0.01f, 0f));

            CapturePointView component = view.AddComponent<CapturePointView>();

            using (Bind bind = new(component))
            {
                bind.Ref("point", point)
                    .Ref("banner", bannerRenderer)
                    .Ref("ring", ringRenderer)
                    .Ref("ringTransform", ringRenderer.transform)
                    .Ref("progress", progressRenderer)
                    .Ref("progressTransform", progressRenderer.transform);
            }

            // Древко не красим в цвет команды — иначе на светлых цветах силуэт теряется.
            _ = poleRenderer;
        }

        /// <summary>
        /// Примитив без коллайдера. Коллайдер здесь вреден: флаг стоит ровно там, куда
        /// идут юниты и герой, и капсула цилиндра превратила бы точку захвата в столб,
        /// об который армия застревает.
        /// </summary>
        private static Renderer Primitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Material material,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (go.TryGetComponent(out Collider collider))
                Object.DestroyImmediate(collider);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            return renderer;
        }

        private static Material EnsureOpaque(string name, Color color)
        {
            Material material = Load(name);

            if (material == null)
                material = Create(name, "Universal Render Pipeline/Lit");

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>
        /// Полупрозрачный неосвещаемый материал: цвет такой поверхности — чистая информация
        /// о владельце, и подмешивать в неё свет нельзя.
        /// </summary>
        private static Material EnsureTransparent(string name, Texture2D mask)
        {
            Material material = Load(name);

            if (material == null)
                material = Create(name, "Universal Render Pipeline/Unlit");

            if (mask != null)
                material.SetTexture("_BaseMap", mask);

            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>
        /// Текстура-обод: прозрачная середина и мягкая полоса по краю. Без неё кольцо было
        /// сплошным диском и читалось как разлитая по земле краска, а не как граница зоны.
        /// Цилиндр Unity разворачивает эту текстуру ровно на свои торцы.
        /// </summary>
        private static Texture2D EnsureRingTexture()
        {
            const string Path = "Assets/_InternalAssets/Art/Textures/Warlord_RingMask.png";
            const int Size = 256;

            Directory.CreateDirectory("Assets/_InternalAssets/Art/Textures");

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            float half = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Полоса между 0.82 и 0.97 радиуса, мягкая с обеих сторон.
                    float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.88f, distance));
                    float outer = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.94f, 1f, distance));

                    byte alpha = (byte)(Mathf.Clamp01(inner * outer) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(Path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(Path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        }

        private static Material Load(string name)
            => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");

        private static Material Create(string name, string shaderName)
        {
            Directory.CreateDirectory(MaterialFolder);

            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError("Warlord: шейдер не найден — " + shaderName);
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialFolder + "/" + name + ".mat");

            return material;
        }
    }
}
