using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryLauncher
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public static string GameUninstallCommand = "-y uninstall {0}";
        public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "legendary");

        public static string ClientExecPath
        {
            get
            {
                var path = LauncherPath;
                return string.IsNullOrEmpty(path) ? string.Empty : GetExecutablePath(path);
            }
        }

        public static string PortalConfigPath
        {
            get
            {
                return null;
            }
        }

        public static string DefaultLauncherPath
        {
            get
            {
                var launcherPath = "";
                var envPath = Environment.GetEnvironmentVariable("PATH")
                    .Split(';')
                    .Select(x => Path.Combine(x))
                    .Where(x => File.Exists(Path.Combine(x, "legendary.exe"))).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(envPath) == false)
                {
                    launcherPath = envPath;
                }
                else if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\heroic\resources\app.asar.unpacked\build\bin\win32\legendary.exe")))
                {
                    launcherPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\heroic\resources\app.asar.unpacked\build\bin\win32\");
                }
                else
                {
                    var pf64 = Environment.GetEnvironmentVariable("ProgramW6432");
                    if (string.IsNullOrEmpty(pf64))
                    {
                        pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    }
                    launcherPath = Path.Combine(pf64, "Legendary");
                }
                return launcherPath;
            }
        }

        public static string LauncherPath
        {
            get
            {
                var launcherPath = DefaultLauncherPath;
                var savedLauncherPath = LegendaryLibrary.GetSettings().SelectedLauncherPath;
                if (savedLauncherPath != "" && Directory.Exists(savedLauncherPath) && LegendaryLibrary.GetSettings().UseCustomLauncherPath)
                {
                    launcherPath = savedLauncherPath;
                }
                return launcherPath;
            }
        }

        public static bool IsInstalled
        {
            get
            {
                var path = LauncherPath;
                return !string.IsNullOrEmpty(path) && Directory.Exists(path);
            }
        }

        public static bool IsInstalledInDefaultPath
        {
            get
            {
                var path = DefaultLauncherPath;
                return !string.IsNullOrEmpty(path) && Directory.Exists(path);
            }
        }

        public static string DefaultGamesInstallationPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games");
            }
        }

        public static bool IsEOSOverlayInstalled
        {
            get
            {
                var installed = false;
                var overlayInfoFile = Path.Combine(ConfigPath, "overlay_install.json");
                if (File.Exists(overlayInfoFile))
                {
                    installed = true;
                }
                return installed;
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\legendary_icon.ico");

        public static void StartClient()
        {
            ProcessStarter.StartProcess(ClientExecPath, string.Empty);
        }

        internal static string GetExecutablePath(string rootPath)
        {
            return Path.Combine(rootPath, "legendary.exe");
        }

        public static Dictionary<string, Installed> GetInstalledAppList()
        {
            var installListPath = Path.Combine(ConfigPath, "installed.json");
            var list = Serialization.FromJson<Dictionary<string, Installed>>(FileSystem.ReadFileAsStringSafe(installListPath));
            return list;
        }
    }
}
