using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Models;
using Microsoft.Win32;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
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
                var originalLegendaryinstallListPath = Path.Combine(legendaryConfigPath, "installed.json");
                var heroicLegendaryInstallListPath = Path.Combine(heroicLegendaryConfigPath, "installed.json");
                if (File.Exists(heroicLegendaryInstallListPath))
                {
                    if (File.Exists(originalLegendaryinstallListPath))
                    {
                        if (File.GetLastWriteTime(heroicLegendaryInstallListPath) > File.GetLastWriteTime(originalLegendaryinstallListPath))
                        {
                            legendaryConfigPath = heroicLegendaryConfigPath;
                        }
                    }
                    else
                    {
                        legendaryConfigPath = heroicLegendaryConfigPath;
                    }
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

        public static string HeroicLegendaryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    @"Programs\heroic\resources\app.asar.unpacked\build\bin\win32\");

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
                else if (File.Exists(Path.Combine(HeroicLegendaryPath, "legendary.exe")))
                {
                    launcherPath = HeroicLegendaryPath;
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

        public static async Task<LegendaryGameInfo.Rootobject> GetGameInfo(string gameID, bool skipRefreshing = false)
        {
            GlobalProgressOptions metadataProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.Legendary3P_PlayniteProgressMetadata), false);
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
                if (!skipRefreshing)
                {
                    if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7))
                    {
                        var metadataFile = Path.Combine(ConfigPath, "metadata", gameID + ".json");
                        if (File.Exists(metadataFile))
                        {
                            File.Delete(metadataFile);
                        }
                        File.Delete(cacheInfoFile);
                    }
                }
            }
            if (File.Exists(cacheInfoFile))
            {
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(cacheInfoFile), out manifest))
                {
                    if (manifest != null && manifest.Manifest != null && manifest.Game != null)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                var result = await Cli.Wrap(ClientExecPath)
                                      .WithArguments(new[] { "info", gameID, "--json" })
                                      .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                var errorMessage = result.StandardError;
                if (result.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                {
                    logger.Error("[Legendary]" + result.StandardError);
                    if (result.StandardError.Contains("Failed to establish a new connection")
                        || result.StandardError.Contains("Log in failed")
                        || result.StandardError.Contains("Login failed")
                        || result.StandardError.Contains("No saved credentials"))
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
            return manifest;
        }

        public static async Task<string> GetLauncherVersion()
        {
            var version = "0";
            if (IsInstalled)
            {
                var versionCmd = await Cli.Wrap(ClientExecPath)
                                          .WithArguments(new[] { "-V" })
                                          .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                          .WithValidation(CommandResultValidation.None)
                                          .ExecuteBufferedAsync();
                if (versionCmd.StandardOutput.Contains("version"))
                {
                    version = Regex.Match(versionCmd.StandardOutput, @"\d+(\.\d+)+").Value;
                }
            }
            return version;
        }

        public static async Task<LauncherVersion.Rootobject> GetVersionInfoContent()
        {
            var newVersionInfoContent = new LauncherVersion.Rootobject();
            var logger = LogManager.GetLogger();
            if (!IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
            }
            var cacheVersionPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            var cacheVersionFile = Path.Combine(cacheVersionPath, "legendaryVersion.json");
            string content = null;
            if (File.Exists(cacheVersionFile))
            {
                if (File.GetLastWriteTime(cacheVersionFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheVersionFile);
                }
            }
            if (!File.Exists(cacheVersionFile))
            {
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync("https://api.legendary.gl/v1/version.json");
                if (response.IsSuccessStatusCode)
                {
                    content = await response.Content.ReadAsStringAsync();
                    if (!Directory.Exists(cacheVersionPath))
                    {
                        Directory.CreateDirectory(cacheVersionPath);
                    }
                    File.WriteAllText(cacheVersionFile, content);
                }
                httpClient.Dispose();
            }
            else
            {
                content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
            }
            if (content.IsNullOrEmpty())
            {
                logger.Error("An error occurred while downloading Legendary's version info.");
            }
            var versionInfoContent = new LauncherVersion.Rootobject();
            if (Serialization.TryFromJson(content, out versionInfoContent))
            {
                newVersionInfoContent = versionInfoContent;
            }
            return newVersionInfoContent;
        }

    }
}
