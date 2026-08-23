using Playnite.SDK;

namespace LegendaryLibraryNS
{
    public class LegendaryTroubleshootingInformation
    {
        public string PlayniteVersion
        {
            get
            {
                var playniteAPI = API.Instance;
                return playniteAPI.ApplicationInfo.ApplicationVersion.ToString();
            }
        }

        public string PluginVersion
        {
            get
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                return fvi.FileVersion;
            }
        }

        public string LauncherVersion { get; set; } = "";
        public string LauncherBinary => LegendaryLauncher.ClientExecPath;
        public string GamesInstallationPath => LegendaryLauncher.GamesInstallationPath;
    }
}