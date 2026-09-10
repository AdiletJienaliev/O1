using System.IO;
using HeathenEngineering.SteamworksIntegration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Warlord.EditorTools.Steam
{
    /// <summary>
    /// Кладёт steam_appid.txt рядом с собранным .exe.
    /// </summary>
    /// <remarks>
    /// Без этого файла Steamworks на старте вызывает RestartAppIfNecessary, тот возвращает
    /// true («меня запустили не из Steam») и приложение закрывается, чтобы Steam перезапустил
    /// его сам. Перезапускает он App ID из настроек — то есть Spacewar, а не нашу сборку,
    /// поэтому окно просто исчезает и снаружи это выглядит вылетом на старте.
    ///
    /// В редакторе файл лежит в корне проекта (его пишет «Warlord/Настройка/15»), но в билд
    /// он не попадает: Unity копирует только Assets. Отсюда постпроцессор — иначе про файл
    /// придётся помнить руками при каждой сборке, а забывается он ровно один раз, зато
    /// обнаруживается уже на чужом компьютере.
    /// </remarks>
    public sealed class WarlordSteamAppIdFile : IPostprocessBuildWithReport
    {
        private const string FileName = "steam_appid.txt";

        /// <summary>Spacewar — тестовое приложение Valve. Годится, пока нет своего App ID.</summary>
        private const uint SpacewarAppId = 480;

        private const string SteamSettingsPath = "Assets/_InternalAssets/Configs/SteamSettings.asset";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null || report.summary.result == BuildResult.Failed)
                return;

            if (!IsStandalone(report.summary.platform))
                return;

            string outputPath = report.summary.outputPath;

            if (string.IsNullOrEmpty(outputPath))
                return;

            string folder = Path.GetDirectoryName(outputPath);

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            string target = Path.Combine(folder, FileName);
            uint appId = ReadAppId();

            File.WriteAllText(target, appId.ToString());

            Debug.Log($"Warlord Steam: рядом со сборкой положен {FileName} с App ID {appId} — {target}");
        }

        private static bool IsStandalone(BuildTarget target) => target is BuildTarget.StandaloneWindows
            or BuildTarget.StandaloneWindows64
            or BuildTarget.StandaloneOSX
            or BuildTarget.StandaloneLinux64;

        /// <summary>
        /// App ID берётся из того же ассета, что и рантайм: файл рядом с exe и настройки
        /// Steamworks обязаны совпадать, иначе Steam снова решит перезапустить сборку.
        /// </summary>
        private static uint ReadAppId()
        {
            SteamSettings settings = AssetDatabase.LoadAssetAtPath<SteamSettings>(SteamSettingsPath);

            if (settings != null && settings.applicationId.m_AppId != 0)
                return settings.applicationId.m_AppId;

            if (File.Exists(FileName) && uint.TryParse(File.ReadAllText(FileName).Trim(), out uint fromFile) && fromFile != 0)
                return fromFile;

            return SpacewarAppId;
        }
    }
}
