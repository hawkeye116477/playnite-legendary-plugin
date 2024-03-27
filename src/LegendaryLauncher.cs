using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Models;
using Microsoft.Win32;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

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
            var list = new Dictionary<string, Installed>();
            if (File.Exists(installListPath))
            {
                var content = FileSystem.ReadFileAsStringSafe(installListPath);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out Dictionary<string, Installed> nonEmptyList))
                {
                    list = nonEmptyList;
                }
            }
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

        public static async Task<UpdateInfo> GetUpdateSizes(string gameID)
        {
            var updateInfo = new UpdateInfo();
            var cacheUpdateInfoPath = LegendaryLibrary.Instance.GetCachePath("updateinfocache");
            var cacheUpdateInfoFile = Path.Combine(cacheUpdateInfoPath, gameID + ".json");
            if (File.Exists(cacheUpdateInfoFile))
            {
                if (File.GetLastWriteTime(cacheUpdateInfoFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheUpdateInfoFile);
                }
            }
            bool correctJson = false;
            if (File.Exists(cacheUpdateInfoFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(cacheUpdateInfoFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out updateInfo))
                {
                    if (updateInfo != null && updateInfo.Download_size != 0)
                    {
                        correctJson = true;
                    }
                }
            }

            if (!correctJson)
            {
                BufferedCommandResult cmd;
                if (gameID == "eos-overlay")
                {
                    cmd = await Cli.Wrap(ClientExecPath)
                                   .WithArguments(new[] { "eos-overlay", "update" })
                                   .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                   .WithStandardInputPipe(PipeSource.FromString("n"))
                                   .AddCommandToLog()
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                }
                else
                {
                    cmd = await Cli.Wrap(ClientExecPath)
                                   .WithArguments(new[] { "update", gameID })
                                   .WithStandardInputPipe(PipeSource.FromString("n"))
                                   .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                   .AddCommandToLog()
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                }
                var errorMessage = cmd.StandardError;
                if (cmd.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                {
                    var logger = LogManager.GetLogger();
                    logger.Error("[Legendary]" + cmd.StandardError);
                }
                else if (!cmd.StandardError.Contains("up to date"))
                {
                    double downloadSizeNumber = 0;
                    double installSizeNumber = 0;
                    string downloadSizeUnit = "B";
                    string installSizeUnit = "B";
                    string[] lines = cmd.StandardError.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var downloadSizeText = "Download size:";
                        if (line.Contains(downloadSizeText))
                        {
                            var downloadSizeSplittedString = line.Substring(line.IndexOf(downloadSizeText) + downloadSizeText.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            downloadSizeNumber = double.Parse(downloadSizeSplittedString[0], CultureInfo.InvariantCulture);
                            downloadSizeUnit = downloadSizeSplittedString[1];
                            updateInfo.Download_size = Helpers.ToBytes(downloadSizeNumber, downloadSizeUnit);
                        }
                        var installSizeText = "Install size:";
                        if (line.Contains(installSizeText))
                        {
                            var installSizeSplittedString = line.Substring(line.IndexOf(installSizeText) + installSizeText.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            installSizeNumber = double.Parse(installSizeSplittedString[0], CultureInfo.InvariantCulture);
                            installSizeUnit = installSizeSplittedString[1];
                            updateInfo.Disk_size = Helpers.ToBytes(installSizeNumber, installSizeUnit);
                        }
                    }
                    if (updateInfo.Download_size != 0 && updateInfo.Disk_size != 0)
                    {
                        if (!Directory.Exists(cacheUpdateInfoPath))
                        {
                            Directory.CreateDirectory(cacheUpdateInfoPath);
                        }
                        File.WriteAllText(cacheUpdateInfoFile, Serialization.ToJson(updateInfo));
                    }
                }
            }
            return updateInfo;
        }

        public static async Task<LegendaryGameInfo.Rootobject> GetGameInfo(string gameID, bool skipRefreshing = false, bool silently = false)
        {
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
                var content = FileSystem.ReadFileAsStringSafe(cacheInfoFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out manifest))
                {
                    if (manifest != null && manifest.Manifest != null && manifest.Game != null)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                BufferedCommandResult result;
                if (gameID == "eos-overlay")
                {
                    result = await Cli.Wrap(ClientExecPath)
                                      .WithArguments(new[] { "eos-overlay", "install" })
                                      .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                      .WithStandardInputPipe(PipeSource.FromString("n"))
                                      .AddCommandToLog()
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                }
                else
                {
                    result = await Cli.Wrap(ClientExecPath)
                                      .WithArguments(new[] { "info", gameID, "--json" })
                                      .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                      .AddCommandToLog()
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                }
                var errorMessage = result.StandardError;
                if (result.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                {
                    logger.Error("[Legendary]" + result.StandardError);
                    if (!silently)
                    {
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
                }
                else if (gameID == "eos-overlay")
                {
                    manifest.Game = new LegendaryGameInfo.Game
                    {
                        App_name = gameID,
                        Title = ResourceProvider.GetString(LOC.LegendaryEOSOverlay)
                    };
                    manifest.Manifest = new LegendaryGameInfo.Manifest();
                    string[] lines = result.StandardError.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var downloadSizeText = "Download size:";
                        if (line.Contains(downloadSizeText))
                        {
                            var downloadSizeSplittedString = line.Substring(line.IndexOf(downloadSizeText) + downloadSizeText.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            manifest.Manifest.Download_size = Helpers.ToBytes(double.Parse(downloadSizeSplittedString[0], CultureInfo.InvariantCulture), downloadSizeSplittedString[1]);
                        }
                        var installSizeText = "Install size:";
                        if (line.Contains(installSizeText))
                        {
                            var installSizeSplittedString = line.Substring(line.IndexOf(installSizeText) + installSizeText.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            manifest.Manifest.Disk_size = Helpers.ToBytes(double.Parse(installSizeSplittedString[0], CultureInfo.InvariantCulture), installSizeSplittedString[1]);
                        }
                    }
                    File.WriteAllText(cacheInfoFile, Serialization.ToJson(manifest));
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
                                          .AddCommandToLog()
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
            if (content.IsNullOrWhiteSpace())
            {
                logger.Error("An error occurred while downloading Legendary's version info.");
            }
            else if (Serialization.TryFromJson(content, out LauncherVersion.Rootobject versionInfoContent))
            {
                newVersionInfoContent = versionInfoContent;
            }
            return newVersionInfoContent;
        }

        public static bool IsEaAppInstalled
        {
            get
            {
                var logger = LogManager.GetLogger();
                var launcherPath = "";
                try
                {
                    using (var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Electronic Arts\EA Desktop", false))
                    {
                        if (regKey != null)
                        {
                            var launcherPathObj = regKey.GetValue("ClientPath");
                            if (launcherPathObj != null)
                            {
                                launcherPath = launcherPathObj.ToString();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to get launcher path. Error: {ex.Message}");
                }
                return !string.IsNullOrEmpty(launcherPath) && File.Exists(launcherPath);
            }
        }

        public static void ClearCache()
        {
            var cacheDirs = new List<string>()
            {
                LegendaryLibrary.Instance.GetCachePath("catalogcache"),
                LegendaryLibrary.Instance.GetCachePath("infocache"),
                LegendaryLibrary.Instance.GetCachePath("sdlcache"),
                LegendaryLibrary.Instance.GetCachePath("updateinfocache"),
                Path.Combine(ConfigPath, "metadata")
            };
            foreach (var cacheDir in cacheDirs)
            {
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                }
            }
        }

        public static void ShowCheckAllGamesUpdatesDialog()
        {
            IPlayniteAPI playniteAPI = API.Instance;
            if (!IsInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
                return;
            }

            var gamesUpdates = new Dictionary<string, UpdateInfo>();
            LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
            GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendaryCheckingForUpdates), false) { IsIndeterminate = true };
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates();
            }, updateCheckProgressOptions);
            if (gamesUpdates.Count > 0)
            {
                var successUpdates = gamesUpdates.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                if (successUpdates.Count > 0)
                {
                    Window window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                    {
                        ShowMaximizeButton = false,
                    });
                    window.DataContext = successUpdates;
                    window.Title = $"{ResourceProvider.GetString(LOC.Legendary3P_PlayniteExtensionsUpdates)}";
                    window.Content = new LegendaryUpdater();
                    window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
                    window.SizeToContent = SizeToContent.WidthAndHeight;
                    window.MinWidth = 600;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.ShowDialog();
                }
                else
                {
                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdateCheckFailMessage));
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryNoUpdatesAvailable));
            }
        }
    }
}
