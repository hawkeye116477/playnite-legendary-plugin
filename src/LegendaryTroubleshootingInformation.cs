using System;
using System.Threading.Tasks;

namespace LegendaryLibraryNS;

public static class LegendaryTroubleshootingInformation
{
    public static string PlayniteVersion => LegendaryLibrary.PlayniteApi.AppInfo.ApplicationVersion.ToString();

    public static string? PluginVersion
    {
        get
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
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
    public static string GamesInstallationPath => LegendaryLauncher.GamesInstallationPath;
}