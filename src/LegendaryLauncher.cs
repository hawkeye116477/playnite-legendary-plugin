using CliWrap;
using CliWrap.Buffered;
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
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryLauncher
    {
        public static string ConfigPath
        {
            get
            {
                var legendaryConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "legendary");
                var heroicLegendaryConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic", "legendaryConfig", "legendary");
                if (Directory.Exists(heroicLegendaryConfigPath) && LauncherPath.Contains("heroic"))
                {
                    legendaryConfigPath = heroicLegendaryConfigPath;
                }
                return legendaryConfigPath;
            }
        }

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
                    if (!File.Exists(Path.Combine(launcherPath, "legendary.exe")))
                    {
                        var playniteAPI = API.Instance;
                        if (playniteAPI.ApplicationInfo.IsPortable)
                        {
                            launcherPath = Path.Combine(playniteAPI.Paths.ApplicationPath, "Legendary");
                        }
                    }
                }
                var savedSettings = LegendaryLibrary.GetSettings();
                if (savedSettings != null)
                {
                    var savedLauncherPath = savedSettings.SelectedLauncherPath;
                    var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
                    if (savedLauncherPath != "")
                    {
                        if (savedLauncherPath.Contains(playniteDirectoryVariable))
                        {
                            var playniteAPI = API.Instance;
                            savedLauncherPath = savedLauncherPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
                        }
                        if (Directory.Exists(savedLauncherPath))
                        {
                            launcherPath = savedLauncherPath;
                        }
                    }
                }
                if (!File.Exists(Path.Combine(launcherPath, "legendary.exe")))
                {
                    launcherPath = "";
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

        public static string GamesInstallationPath
        {
            get
            {
                var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games");
                if (IniConfig != null && !IniConfig["Legendary"]["install_dir"].IsNullOrEmpty())
                {
                    var newInstallPath = "";
                    bool installPathIsValid = true;
                    try
                    {
                        newInstallPath = Path.GetFullPath(IniConfig["Legendary"]["install_dir"]);
                    }
                    catch
                    {
                        installPathIsValid = false;
                    }
                    if (installPathIsValid)
                    {
                        installPath = newInstallPath;
                    }
                }
                var playniteAPI = API.Instance;
                if (playniteAPI.ApplicationInfo.IsPortable)
                {
                    var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
                    installPath = Path.Combine(playniteDirectoryVariable, "Games");
                }
                var savedSettings = LegendaryLibrary.GetSettings();
                if (savedSettings != null)
                {
                    var savedGamesInstallationPath = savedSettings.GamesInstallationPath;
                    if (savedGamesInstallationPath != "")
                    {
                        installPath = savedGamesInstallationPath;
                    }
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

        public static Dictionary<string, string> DefaultEnvironmentVariables
        {
            get
            {
                var envDict = new Dictionary<string, string>();
                if (ConfigPath.Contains("heroic"))
                {
                    envDict.Add("LEGENDARY_CONFIG_PATH", ConfigPath);
                }
                return envDict;
            }
        }

        public static LegendaryGameInfo.Rootobject GetGameInfo(string gameID)
        {
            GlobalProgressOptions metadataProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoadingLabel), false);
            var manifest = new LegendaryGameInfo.Rootobject();
            var playniteAPI = API.Instance;
            var logger = LogManager.GetLogger();
            var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            var cacheInfoFile = Path.Combine(cacheInfoPath, gameID + ".json");
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }
            bool correctJson = false;
            if (File.Exists(cacheInfoFile))
            {
                if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheInfoFile);
                }
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(cacheInfoFile), out manifest))
                {
                    if (manifest != null && manifest.Manifest != null)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
                {
                    if (!correctJson)
                    {
                        var result = await Cli.Wrap(ClientExecPath)
                                          .WithArguments(new[] { "info", gameID, "--json" })
                                          .WithValidation(CommandResultValidation.None)
                                          .ExecuteBufferedAsync();
                        if (result.ExitCode != 0)
                        {
                            logger.Error("[Legendary]" + result.StandardError);
                            if (result.StandardError.Contains("Log in failed"))
                            {
                                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                            }
                            else
                            {
                                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                            }
                        }
                        else
                        {
                            File.WriteAllText(cacheInfoFile, result.StandardOutput);
                            manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>(result.StandardOutput);
                        }
                    }
                }, metadataProgressOptions);
            }
            return manifest;
        }

    }
}
