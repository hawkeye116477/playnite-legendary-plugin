using System.Diagnostics;
using System.Reflection;

namespace LegendaryLibraryNS;

public static class LegendaryTroubleshootingInformation
{
    public static string PlayniteVersion => LegendaryLibrary.PlayniteApi.AppInfo.ApplicationVersion.ToString();

    public static string? PluginVersion
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.ProductVersion;
        }
    }

    public static async Task<string> GetLauncherVersion()
    {
        var launcherVersion = await LegendaryLauncher.GetLauncherVersion();
        if (launcherVersion.IsNullOrWhiteSpace())
        {
            launcherVersion = "Not%20installed";
        }

        return launcherVersion;
    }

    public static string LauncherBinary => LegendaryLauncher.ClientExecPath;
    public static string GamesInstallationPath => LegendaryGames.InstallationPath;
}