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
    /// Пост-обработка URP по ГДД §4.
    ///
    /// Главное правило раздела: в игре, где на экране до восьмидесяти юнитов четырёх цветов,
    /// читаемость важнее сочности. Поэтому половина стандартного набора эффектов здесь
    /// не просто выключена, а не создаётся вовсе — выключенный оверрайд в профиле слишком
    /// легко включить «посмотреть, как будет», и он остаётся включённым до релиза.
    ///
    /// Собирается кодом, потому что профиль — это два десятка чисел, каждое из которых
    /// в ГДД снабжено причиной. В инспекторе причины не видно, и первый же человек,
    /// поднявший Bloom Threshold ниже единицы, засветит доспехи и убьёт различимость толпы.
    /// </summary>
    public static class WarlordPostProcessingSetup
    {
        private const string ProfileFolder = "Assets/_InternalAssets/Art/PostProcessing";
        private const string GlobalProfilePath = ProfileFolder + "/PP_Battle_Global.asset";
        private const string CenterProfilePath = ProfileFolder + "/PP_CenterFlag.asset";

        private const string PostProcessingRoot = "--- Post Processing ---";

        [MenuItem("Warlord/Настройка/14. Пост-процессинг URP в открытой сцене", priority = 13)]
        public static void Build()
        {
            List<string> report = new();

            ApplyToScene(report);
            ApplyToPipelineAssets(report);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Warlord: пост-процессинг настроен:\n" + string.Join("\n", report)
                      + "\n\nПроверьте §4.5: четыре командных цвета должны различаться "
                      + "на свету, в тени и в бою.");
        }

        /// <summary>Профили, объёмы, камера и туман. Вызывается и из сборщика арены.</summary>
        public static void ApplyToScene(List<string> report)
        {
            VolumeProfile global = BuildGlobalProfile(report);
            VolumeProfile center = BuildCenterProfile(report);

            Transform root = EnsureRoot();

            EnsureVolume(root, "Global Volume", global, isGlobal: true, priority: 0f, radius: 0f);
            EnsureVolume(root, "PP_CenterFlag", center, isGlobal: false, priority: 1f, radius: 25f);

            ConfigureCamera(report);
            ConfigureFog(report);
        }

        #region Профиль боя

        /// <summary>
        /// Общий профиль (ГДД §4.3). Каждый оверрайд здесь оставлен по конкретной причине,
        /// и причина записана рядом — иначе через месяц профиль правится на глаз.
        /// </summary>
        private static VolumeProfile BuildGlobalProfile(List<string> report)
        {
            VolumeProfile profile = LoadOrCreateProfile(GlobalProfilePath, report);

            // Тонмаппинг обязателен. Без него HDR-цвета команд выгорают в белый, и синий
            // с зелёным перестают различаться в ярко освещённых местах.
            Tonemapping tonemapping = Ensure<Tonemapping>(profile);
            Set(tonemapping.mode, TonemappingMode.ACES);

            // Насыщенность немного вверх: командные цвета должны читаться. Выше +12 цвета
            // «плывут», и синий уходит в фиолетовый.
            ColorAdjustments color = Ensure<ColorAdjustments>(profile);
            Set(color.postExposure, 0f);
            Set(color.contrast, 10f);
            Set(color.saturation, 8f);
            Set(color.hueShift, 0f);
            Set(color.colorFilter, Color.white);

            // Лёгкое тепло даёт «античность» без цветофильтра поверх всей картинки.
            WhiteBalance balance = Ensure<WhiteBalance>(profile);
            Set(balance.temperature, 5f);
            Set(balance.tint, 0f);

            // Порог выше единицы — светится только реально яркое: магия, столб света над
            // полководцем, блики. Ниже единицы засветились бы доспехи, и толпа слиплась бы.
            Bloom bloom = Ensure<Bloom>(profile);
            Set(bloom.threshold, 1.1f);
            Set(bloom.intensity, 0.35f);
            Set(bloom.scatter, 0.6f);
            Set(bloom.tint, Color.white);
            Set(bloom.clamp, 65472f);
            Set(bloom.highQualityFiltering, false);

            // Виньетка собирает взгляд к центру. Сильнее 0.3 темнеет периферия, где как раз
            // появляются фланговые атаки, — и игрок перестаёт их замечать.
            Vignette vignette = Ensure<Vignette>(profile);
            Set(vignette.intensity, 0.25f);
            Set(vignette.smoothness, 0.4f);
            Set(vignette.rounded, false);

            // Тёплый свет и холодная тень — самый дешёвый способ получить «дорогую» картинку,
            // не добавив в проект ни одного нового ассета.
            ShadowsMidtonesHighlights split = Ensure<ShadowsMidtonesHighlights>(profile);
            Set(split.shadows, new Vector4(1.02f, 1f, 1.05f, 0f));
            Set(split.midtones, new Vector4(1f, 1f, 1f, 0f));
            Set(split.highlights, new Vector4(1.03f, 1.01f, 0.98f, 0f));
            Set(split.shadowsStart, 0f);
            Set(split.shadowsEnd, 0.3f);
            Set(split.highlightsStart, 0.55f);
            Set(split.highlightsEnd, 1f);

            RemoveForbidden(profile, report);

            EditorUtility.SetDirty(profile);
            report.Add("профиль " + Path.GetFileName(GlobalProfilePath) + ": ACES, Bloom 1.10/0.35, виньетка 0.25");

            return profile;
        }

        /// <summary>
        /// Локальный объём центра (ГДД §4.9). Эффект едва заметен, и это цель: центр должен
        /// подсознательно ощущаться главным местом, а заметный переход на входе в зону
        /// читается как баг рендера, а не как приём.
        /// </summary>
        private static VolumeProfile BuildCenterProfile(List<string> report)
        {
            VolumeProfile profile = LoadOrCreateProfile(CenterProfilePath, report);

            Vignette vignette = Ensure<Vignette>(profile);
            Set(vignette.intensity, 0.32f);

            ColorAdjustments color = Ensure<ColorAdjustments>(profile);
            Set(color.postExposure, 0.15f);
            Set(color.saturation, 12f);

            EditorUtility.SetDirty(profile);
            report.Add("профиль " + Path.GetFileName(CenterProfilePath) + ": +0.07 виньетки, +4 насыщенности");

            return profile;
        }

        /// <summary>
        /// Эффекты из таблицы ГДД §4.4 удаляются из профиля, а не выключаются. Каждый из них
        /// либо прячет информацию, которая нужна полководцу, либо конфликтует с командными
        /// цветами, и оставленный выключенным оверрайд — это приглашение включить его обратно.
        /// </summary>
        private static void RemoveForbidden(VolumeProfile profile, List<string> report)
        {
            int removed = 0;

            removed += Remove<DepthOfField>(profile);
            removed += Remove<MotionBlur>(profile);
            removed += Remove<FilmGrain>(profile);
            removed += Remove<LensDistortion>(profile);
            removed += Remove<PaniniProjection>(profile);
            removed += Remove<ChromaticAberration>(profile);
            removed += Remove<ChannelMixer>(profile);
            removed += Remove<SplitToning>(profile);
            removed += Remove<LiftGammaGain>(profile);

            if (removed > 0)
                report.Add("удалено запрещённых ГДД §4.4 эффектов: " + removed);
        }

        private static int Remove<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
                return 0;

            profile.Remove<T>();
            Object.DestroyImmediate(component, true);
            return 1;
        }

        private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet(out T component) ? component : profile.Add<T>(overrides: true);
        }

        private static void Set<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static VolumeProfile LoadOrCreateProfile(string path, List<string> report)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile != null)
                return profile;

            Directory.CreateDirectory(ProfileFolder);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            report.Add("создан " + path);
            return profile;
        }

        #endregion

        #region Сцена

        private static Transform EnsureRoot()
        {
            GameObject existing = GameObject.Find(PostProcessingRoot);
            return existing != null ? existing.transform : new GameObject(PostProcessingRoot).transform;
        }

        private static void EnsureVolume(
            Transform root,
            string name,
            VolumeProfile profile,
            bool isGlobal,
            float priority,
            float radius)
        {
            Transform existing = root.Find(name);
            GameObject volumeObject = existing != null ? existing.gameObject : new GameObject(name);
            volumeObject.transform.SetParent(root, false);

            if (!volumeObject.TryGetComponent(out Volume volume))
                volume = volumeObject.AddComponent<Volume>();

            volume.isGlobal = isGlobal;
            volume.priority = priority;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            if (isGlobal)
                return;

            // Локальный объём живёт на триггере: коллайдер задаёт зону, blendDistance —
            // мягкий вход в неё, иначе переход щёлкает на границе.
            if (!volumeObject.TryGetComponent(out SphereCollider collider))
                collider = volumeObject.AddComponent<SphereCollider>();

            collider.isTrigger = true;
            collider.radius = radius;

            volume.blendDistance = 10f;
        }

        /// <summary>
        /// Камера по ГДД §4.2. Сглаживание — SMAA, а не TAA: TAA размазывает быстро движущиеся
        /// мелкие объекты, а на экране у нас десятки бегущих юнитов и стрелы.
        /// </summary>
        private static void ConfigureCamera(List<string> report)
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                report.Add("камеры с тегом MainCamera в сцене нет — пост-обработку включите вручную");
                return;
            }

            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data))
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.stopNaN = true;
            data.dithering = true;

            EditorUtility.SetDirty(camera);
            report.Add("камера: пост-обработка включена, сглаживание SMAA High, Stop NaN и Dithering");
        }

        /// <summary>
        /// Туман (ГДД §4.8). Начало не ближе 70 м: враги должны быть видны на дистанции,
        /// сопоставимой с дальностью наблюдения юнитов, иначе игрок теряет информацию,
        /// которую сервер ему уже прислал.
        /// </summary>
        private static void ConfigureFog(List<string> report)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 200f;
            RenderSettings.fogColor = new Color(0.62f, 0.66f, 0.72f);

            report.Add("туман: линейный, 70–200 м, цвет под небо у горизонта");
        }

        #endregion

        #region URP Asset

        /// <summary>
        /// Настройки конвейера (ГДД §4.6, §4.7). HDR обязателен: без него ACES и Bloom
        /// работают неверно, и картинка отличается по цвету от той, что видят другие игроки.
        /// </summary>
        private static void ApplyToPipelineAssets(List<string> report)
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");

            if (guids.Length == 0)
            {
                report.Add("UniversalRenderPipelineAsset в проекте не найден — §4.6 настройте вручную");
                return;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);

                if (asset == null)
                    continue;

                bool mobile = path.Contains("Mobile");
                ApplyToPipelineAsset(asset, mobile, path, report);
            }

            ApplyToSsao(report);
        }

        private static void ApplyToPipelineAsset(
            UniversalRenderPipelineAsset asset,
            bool mobile,
            string path,
            List<string> report)
        {
            SerializedObject serialized = new(asset);

            SetBool(serialized, "m_SupportsHDR", true);
            SetInt(serialized, "m_MSAA", mobile ? 2 : 4);
            SetFloat(serialized, "m_RenderScale", mobile ? 0.85f : 1f);

            SetBool(serialized, "m_MainLightShadowsSupported", true);
            SetInt(serialized, "m_MainLightShadowmapResolution", 2048);
            SetBool(serialized, "m_AdditionalLightShadowsSupported", false);
            SetInt(serialized, "m_AdditionalLightsPerObjectLimit", 4);

            // Дальность теней совпадает с дальностью наблюдения юнитов: тень, пропадающая
            // ближе, чем виден юнит, читается как графический сбой.
            SetFloat(serialized, "m_ShadowDistance", mobile ? 50f : 90f);
            SetInt(serialized, "m_ShadowCascadeCount", mobile ? 1 : 3);
            SetFloat(serialized, "m_Cascade3Split0", 0.12f);
            SetFloat(serialized, "m_Cascade3Split1", 0.3f);
            SetFloat(serialized, "m_ShadowDepthBias", 1f);
            SetFloat(serialized, "m_ShadowNormalBias", 1f);
            SetBool(serialized, "m_SoftShadowsSupported", true);

            // Глубина нужна SSAO и мягким частицам, буфер непрозрачных не используется —
            // отключённый, он экономит целый проход.
            SetBool(serialized, "m_RequireDepthTexture", true);
            SetBool(serialized, "m_RequireOpaqueTexture", false);
            SetBool(serialized, "m_UseSRPBatcher", true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            report.Add(Path.GetFileNameWithoutExtension(path) + ": HDR, MSAA " + (mobile ? "2x" : "4x")
                       + ", тени до " + (mobile ? 50 : 90) + " м");
        }

        /// <summary>
        /// SSAO (ГДД §4.7). Главная его польза не в красоте: юниты перестают «парить»
        /// и визуально прижимаются к земле, и на толпе это заметнее любого другого эффекта.
        /// </summary>
        private static void ApplyToSsao(List<string> report)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            int touched = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null || asset.GetType().Name != "ScreenSpaceAmbientOcclusion")
                        continue;

                    SerializedObject serialized = new(asset);
                    SerializedProperty settings = serialized.FindProperty("m_Settings");

                    if (settings == null)
                        continue;

                    SetFloat(settings, "Intensity", 0.5f);
                    SetFloat(settings, "Radius", 0.25f);
                    SetFloat(settings, "Falloff", 50f);
                    SetFloat(settings, "DirectLightingStrength", 0.25f);
                    SetInt(settings, "Samples", 1);
                    SetInt(settings, "BlurQuality", 1);

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    touched++;
                }
            }

            report.Add(touched > 0
                ? "SSAO: интенсивность 0.5, радиус 0.25, затухание 50 м (" + touched + " рендерера)"
                : "SSAO не найден в рендерерах — добавьте Renderer Feature вручную по §4.7");
        }

        private static void SetBool(SerializedObject serialized, string path, bool value)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property != null)
                property.boolValue = value;
        }

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);

            if (property != null)
                property.floatValue = value;
        }

        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);

            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);

            if (property != null)
                property.floatValue = value;
        }

        #endregion
    }
}
