using System.Collections.Generic;
using System.IO;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using HeathenEngineering.SteamworksIntegration;
using Steamworks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Warlord.Configs;
using Warlord.EditorTools.UI;
using Warlord.Networking;
using Warlord.Networking.Steam;

namespace Warlord.EditorTools.Steam
{
    /// <summary>
    /// Сборка сетевой части сцены под два режима сразу: прямое подключение и Steam.
    /// </summary>
    /// <remarks>
    /// Руками это десяток перетаскиваний в инспекторе, из которых любое можно забыть, а
    /// заметить только на чужом компьютере. Пункт идемпотентен: повторный запуск приводит
    /// сцену к тому же состоянию, а не плодит вторые транспорты.
    /// </remarks>
    public static class WarlordSteamSetup
    {
        private const string ConfigFolder = "Assets/_InternalAssets/Configs";
        private const string SteamSettingsPath = ConfigFolder + "/SteamSettings.asset";
        private const string AppIdFile = "steam_appid.txt";

        /// <summary>Spacewar — тестовое приложение Valve. Годится, пока нет своего App ID.</summary>
        private const uint SpacewarAppId = 480;

        [MenuItem("Warlord/Настройка/15. Сеть: Multipass, Steam-транспорт и лобби", priority = 14)]
        public static void SetupNetworking()
        {
            NetworkManager manager = Object.FindAnyObjectByType<NetworkManager>();

            if (manager == null)
            {
                Debug.LogError("Warlord Steam: в открытой сцене нет NetworkManager");
                return;
            }

            List<string> report = new();

            SteamSettings settings = EnsureSteamSettings(report);
            Multipass multipass = EnsureTransports(manager, out Tugboat direct, out SteamTransport steam, report);
            SteamSession session = EnsureSession(manager, settings, report);

            WireBootstrap(manager, multipass, direct, steam, session, report);

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Warlord Steam: сеть собрана.\n" + string.Join("\n", report), manager);
        }

        /// <summary>
        /// App ID берётся из steam_appid.txt: это тот же файл, по которому его читает сам
        /// клиент Steam при запуске из редактора, и держать число в двух местах — верный
        /// способ однажды получить лобби в чужой игре.
        /// </summary>
        private static SteamSettings EnsureSteamSettings(List<string> report)
        {
            SteamSettings settings = AssetDatabase.LoadAssetAtPath<SteamSettings>(SteamSettingsPath);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SteamSettings>();
                Directory.CreateDirectory(ConfigFolder);
                AssetDatabase.CreateAsset(settings, SteamSettingsPath);
                report.Add("создан " + SteamSettingsPath);
            }

            uint appId = ReadAppId();

            if (settings.applicationId.m_AppId != appId)
            {
                settings.applicationId = new AppId_t(appId);
                EditorUtility.SetDirty(settings);
                report.Add("App ID выставлен в " + appId);
            }

            return settings;
        }

        private static uint ReadAppId()
        {
            if (File.Exists(AppIdFile) && uint.TryParse(File.ReadAllText(AppIdFile).Trim(), out uint fromFile) && fromFile != 0)
                return fromFile;

            File.WriteAllText(AppIdFile, SpacewarAppId.ToString());
            return SpacewarAppId;
        }

        private static Multipass EnsureTransports(NetworkManager manager, out Tugboat direct, out SteamTransport steam, List<string> report)
        {
            GameObject host = manager.gameObject;

            Multipass multipass = Ensure<Multipass>(host, report);
            direct = Ensure<Tugboat>(host, report);
            steam = Ensure<SteamTransport>(host, report);

            // Сервер поднимает NetworkBootstrap по индексу нужного транспорта: с общими
            // действиями Multipass поднимал бы оба сразу и в режиме адреса требовал Steam.
            if (multipass.GlobalServerActions)
            {
                multipass.GlobalServerActions = false;
                report.Add("Multipass: общие действия сервера выключены");
            }

            using (Bind bind = new(multipass))
                bind.Refs("_transports", direct, steam);

            // Свойство NetworkManager.TransportManager заполняется только в Awake, поэтому
            // в редакторе менеджер транспорта берётся как обычный компонент.
            TransportManager transportManager = Ensure<TransportManager>(host, report);

            if (transportManager.Transport != multipass)
            {
                transportManager.Transport = multipass;
                EditorUtility.SetDirty(transportManager);
                report.Add("TransportManager переключён на Multipass");
            }

            EditorUtility.SetDirty(multipass);
            return multipass;
        }

        private static SteamSession EnsureSession(NetworkManager manager, SteamSettings settings, List<string> report)
        {
            SteamSession session = Object.FindAnyObjectByType<SteamSession>(FindObjectsInactive.Include);

            if (session == null)
            {
                session = manager.gameObject.AddComponent<SteamSession>();
                report.Add("добавлен SteamSession");
            }

            using (Bind bind = new(session))
            {
                bind.Ref("config", LoadSingle<GameConfig>())
                    .Ref("settings", settings);
            }

            EditorUtility.SetDirty(session);
            return session;
        }

        private static void WireBootstrap(
            NetworkManager manager,
            Multipass multipass,
            Tugboat direct,
            SteamTransport steam,
            SteamSession session,
            List<string> report)
        {
            NetworkBootstrap bootstrap = Object.FindAnyObjectByType<NetworkBootstrap>(FindObjectsInactive.Include);

            if (bootstrap == null)
            {
                bootstrap = manager.gameObject.AddComponent<NetworkBootstrap>();
                report.Add("добавлен NetworkBootstrap");
            }

            using (Bind bind = new(bootstrap))
            {
                bind.Ref("config", LoadSingle<GameConfig>())
                    .Ref("multipass", multipass)
                    .Ref("directTransport", direct)
                    .Ref("steamTransport", steam)
                    .Ref("platformSource", session);
            }

            EditorUtility.SetDirty(bootstrap);
            report.Add("NetworkBootstrap связан с транспортами и сеансом Steam");
        }

        private static T Ensure<T>(GameObject host, List<string> report) where T : Component
        {
            if (host.TryGetComponent(out T existing))
                return existing;

            report.Add("добавлен " + typeof(T).Name);
            return host.AddComponent<T>();
        }

        private static T LoadSingle<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            if (guids.Length == 0)
            {
                Debug.LogError("Warlord Steam: в проекте не найден ассет " + typeof(T).Name);
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
