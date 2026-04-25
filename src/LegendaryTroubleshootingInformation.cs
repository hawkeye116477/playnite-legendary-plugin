using Playnite;

namespace LegendaryLibraryNS
{
    public class LegendaryTroubleshootingInformation
    {
        public static string PlayniteVersion => LegendaryLibrary.PlayniteApi.AppInfo.ApplicationVersion.ToString();

        public static string? PluginVersion
        {
            get
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                return fvi.FileVersion;
            }
        }
        public string LauncherVersion { get; set; } = "";
        public string LauncherBinary => LegendaryLauncher.ClientExecPath;
        public string GamesInstallationPath => LegendaryLauncher.GamesInstallationPath;
    }
}
