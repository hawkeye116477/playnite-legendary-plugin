using IniParser;
using IniParser.Model;
using LegendaryLibraryNS.Models;
using Microsoft.Win32;
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
        public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "legendary");

        public static string ClientExecPath
        {
            get
            {
                var path = LauncherPath;
                return string.IsNullOrEmpty(path) ? string.Empty : GetExecutablePath(path);
            }
        }

        public static string LauncherPath
        {
            get
            {
                var launcherPath = "";
                var envPath = Environment.GetEnvironmentVariable("PATH")
                                         .Split(';')
                                         .Select(x => Path.Combine(x))
                                         .FirstOrDefault(x => File.Exists(Path.Combine(x, "legendary.exe")));
                if (string.IsNullOrWhiteSpace(envPath) == false)
                {
                    launcherPath = envPath;
                }
                else if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                  @"Programs\heroic\resources\app.asar.unpacked\build\bin\win32\legendary.exe")))
                {
                    launcherPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                @"Programs\heroic\resources\app.asar.unpacked\build\bin\win32\");
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
                var savedSettings = LegendaryLibrary.GetSettings();
                if (savedSettings != null)
                {
                    var savedLauncherPath = LegendaryLibrary.GetSettings().SelectedLauncherPath;
                    if (savedLauncherPath != "" && Directory.Exists(savedLauncherPath) && LegendaryLibrary.GetSettings().UseCustomLauncherPath)
                    {
                        launcherPath = savedLauncherPath;
                    }
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

        public static string DefaultGamesInstallationPath
        {
            get
            {
                var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games");
                if (IniConfig != null && !IniConfig["Legendary"]["install_dir"].IsNullOrEmpty())
                {
                    installPath = Path.GetFullPath(IniConfig["Legendary"]["install_dir"]);
                }
                return installPath;
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

        public static bool IsEOSOverlayEnabled
        {
            get
            {
                bool enabled = false;
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Epic Games\EOS");
                if (key?.GetValueNames().Contains("OverlayPath") == true)
                {
                    enabled = true;
                }
                return enabled;
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\legendary_icon.ico");

        public static void StartClient()
        {
            ProcessStarter.StartProcess(ClientExecPath);
        }

        internal static string GetExecutablePath(string rootPath)
        {
            return Path.Combine(rootPath, "legendary.exe");
        }

        public static Dictionary<string, Installed> GetInstalledAppList()
        {
            var installListPath = Path.Combine(ConfigPath, "installed.json");
            if (!File.Exists(installListPath))
            {
                return new Dictionary<string, Installed>();
            }

            var list = Serialization.FromJson<Dictionary<string, Installed>>(FileSystem.ReadFileAsStringSafe(installListPath));
            return list;
        }

        public static string TokensPath
        {
            get
            {
                return Path.Combine(ConfigPath, "user.json");
            }
        }

        public static IniData IniConfig
        {
            get
            {
                var configIniPath = Path.Combine(ConfigPath, "config.ini");
                IniData data = null;
                if (File.Exists(configIniPath))
                {
                    var parser = new FileIniDataParser();
                    data = parser.ReadFile(Path.Combine(ConfigPath, "config.ini"));
                }
                return data;
            }
        }

        public static string DefaultPreferredCDN
        {
            get
            {
                var cdn = "";
                if (IniConfig != null && !IniConfig["Legendary"]["preferred_cdn"].IsNullOrEmpty())
                {
                    cdn = IniConfig["Legendary"]["preferred_cdn"];
                }
                return cdn;
            }
        }

        public static bool DefaultNoHttps
        {
            get
            {
                bool noHttps = false;
                if (IniConfig != null && !IniConfig["Legendary"]["disable_https"].IsNullOrEmpty())
                {
                    noHttps = Convert.ToBoolean(IniConfig["Legendary"]["disable_https"]);
                }
                return noHttps;
            }
        }

        public static int DefaultMaxWorkers
        {
            get
            {
                int maxWorkers = 0;
                if (IniConfig != null && !IniConfig["Legendary"]["max_workers"].IsNullOrEmpty())
                {
                    maxWorkers = Convert.ToInt32(IniConfig["Legendary"]["max_workers"]);
                }
                return maxWorkers;
            }
        }

        public static int DefaultMaxSharedMemory
        {
            get
            {
                int maxSharedMemory = 0;
                if (IniConfig != null && !IniConfig["Legendary"]["max_memory"].IsNullOrEmpty())
                {
                    maxSharedMemory = Convert.ToInt32(IniConfig["Legendary"]["max_memory"]);
                }
                return maxSharedMemory;
            }
        }
    }
}
