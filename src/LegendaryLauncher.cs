using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
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
                var envLegendaryConfigPath = Environment.GetEnvironmentVariable("LEGENDARY_CONFIG_PATH");
                if (!envLegendaryConfigPath.IsNullOrWhiteSpace() && Directory.Exists(envLegendaryConfigPath))
                {
                    legendaryConfigPath = envLegendaryConfigPath;
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

        public static string HeroicLegendaryPath
        {
            get
            {
                var heroicResourcesBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                           @"Programs\heroic\resources\app.asar.unpacked\build\bin");
                var path = Path.Combine(heroicResourcesBasePath, @"win32\");
                if (!Directory.Exists(path))
                {
                    path = Path.Combine(heroicResourcesBasePath, @"x64\win32\");
                }
                return path;
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
            if (!ClientExecPath.IsNullOrEmpty())
            {
                ProcessStarter.StartProcess("cmd", $"/K \"{ClientExecPath}\" -h", Path.GetDirectoryName(ClientExecPath));
            }
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
                var heroicLegendaryConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic", "legendaryConfig", "legendary");
                if (ConfigPath == heroicLegendaryConfigPath)
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
                if (cmd.ExitCode != 0 || (!errorMessage.Contains("old manifest") && (errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))))
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
                            downloadSizeNumber = CommonHelpers.ToDouble(downloadSizeSplittedString[0]);
                            downloadSizeUnit = downloadSizeSplittedString[1];
                            updateInfo.Download_size = CommonHelpers.ToBytes(downloadSizeNumber, downloadSizeUnit);
                        }
                        var installSizeText = "Install size:";
                        if (line.Contains(installSizeText))
                        {
                            var installSizeSplittedString = line.Substring(line.IndexOf(installSizeText) + installSizeText.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            installSizeNumber = CommonHelpers.ToDouble(installSizeSplittedString[0]);
                            installSizeUnit = installSizeSplittedString[1];
                            updateInfo.Disk_size = CommonHelpers.ToBytes(installSizeNumber, installSizeUnit);
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

        public static async Task<LegendaryGameInfo.Manifest> CalculateGameSize(LegendaryGameInfo.Game gameData, List<string> extraContent)
        {
            var gameManifest = await GetGameInfo(gameData);
            var size = new LegendaryGameInfo.Manifest
            {
                Disk_size = 0,
                Download_size = 0
            };
            size.Disk_size = gameManifest.Manifest.Disk_size;
            size.Download_size = gameManifest.Manifest.Download_size;
            if (extraContent.Count > 0)
            {
                size.Disk_size = 0;
                size.Download_size = 0;
                foreach (var tag in extraContent)
                {
                    var tagDo = gameManifest.Manifest.Tag_download_size.FirstOrDefault(t => t.Tag == tag);
                    if (tagDo != null)
                    {
                        size.Download_size += tagDo.Size;
                    }
                    var tagDi = gameManifest.Manifest.Tag_disk_size.FirstOrDefault(t => t.Tag == tag);
                    if (tagDi != null)
                    {
                        size.Disk_size += tagDi.Size;
                    }
                }
            }
            return size;
        }

        public static async Task<LegendaryGameInfo.Manifest> CalculateGameSize(DownloadManagerData.Download installData)
        {
            var gameData = new LegendaryGameInfo.Game
            {
                App_name = installData.gameID,
                Title = installData.name
            };
            var extraContent = installData.downloadProperties.extraContent;
            return await CalculateGameSize(gameData, extraContent);
        }

        public static async Task<LegendaryGameInfo.Rootobject> GetGameInfo(LegendaryGameInfo.Game installData, bool skipRefreshing = false, bool silently = false, bool forceRefreshCache = false)
        {
            var gameID = installData.App_name;
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
                    if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7) || forceRefreshCache)
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
                        manifest.Game.Title = manifest.Game.Title.RemoveTrademarks();
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
                if (result.ExitCode != 0)
                {
                    logger.Error("[Legendary]" + result.StandardError);
                    if (!silently)
                    {
                        if (errorMessage.Contains("Log in failed")
                            || errorMessage.Contains("Login failed")
                            || errorMessage.Contains("No saved credentials"))
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)), installData.Title);
                        }
                        else
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.LegendaryCheckLog)), installData.Title);
                        }
                    }
                    manifest.errorDisplayed = true;
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
                            manifest.Manifest.Download_size = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadSizeSplittedString[0]), downloadSizeSplittedString[1]);
                        }
                        var installSizeText = "Install size:";
                        if (line.Contains(installSizeText))
                        {
                            var installSizeSplittedString = line.Substring(line.IndexOf(installSizeText) + installSizeText.Length).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            manifest.Manifest.Disk_size = CommonHelpers.ToBytes(CommonHelpers.ToDouble(installSizeSplittedString[0]), installSizeSplittedString[1]);
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

        public static async Task<Dictionary<string, LegendarySDLInfo>> GetExtraContentInfo(DownloadManagerData.Download installData, bool includeRequiredSdl = false)
        {
            var logger = LogManager.GetLogger();
            var gameData = new LegendaryGameInfo.Game
            {
                Title = installData.name,
                App_name = installData.gameID,
            };
            var manifest = await GetGameInfo(gameData);
            Dictionary<string, LegendarySDLInfo> extraContentInfo = new Dictionary<string, LegendarySDLInfo>();
            if (manifest.errorDisplayed)
            {
                return extraContentInfo;
            }
            if (manifest.Manifest != null && manifest.Manifest.Install_tags.Count > 1)
            {
                var cacheSDLPath = LegendaryLibrary.Instance.GetCachePath("sdlcache");
                if (!Directory.Exists(cacheSDLPath))
                {
                    Directory.CreateDirectory(cacheSDLPath);
                }
                var cacheSDLFile = Path.Combine(cacheSDLPath, installData.gameID + ".json");
                string content = null;
                if (File.Exists(cacheSDLFile))
                {
                    if (File.GetLastWriteTime(cacheSDLFile) < DateTime.Now.AddDays(-7))
                    {
                        File.Delete(cacheSDLFile);
                    }
                }
                if (!File.Exists(cacheSDLFile))
                {
                    var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync("https://api.legendary.gl/v1/sdl/" + installData.gameID + ".json");
                    if (response.IsSuccessStatusCode)
                    {
                        content = await response.Content.ReadAsStringAsync();
                        if (!Directory.Exists(cacheSDLPath))
                        {
                            Directory.CreateDirectory(cacheSDLPath);
                        }
                        File.WriteAllText(cacheSDLFile, content);
                    }
                    httpClient.Dispose();
                }
                else
                {
                    content = FileSystem.ReadFileAsStringSafe(cacheSDLFile);
                }
                bool correctSdlJson = false;
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out extraContentInfo))
                {
                    correctSdlJson = true;
                    foreach (var sdl in extraContentInfo)
                    {
                        sdl.Value.BaseGameID = installData.gameID;
                    }
                }
                else
                {
                    logger.Error($"An error occurred while reading SDL data for {installData.name}.");
                }
                if (!correctSdlJson)
                {
                    extraContentInfo = new Dictionary<string, LegendarySDLInfo>();
                }
                else
                {
                    if (extraContentInfo.ContainsKey("__required"))
                    {
                        extraContentInfo["__required"].Tags.Add("");
                    }
                    else if (extraContentInfo.Count > 0)
                    {
                        var requiredSdl = new LegendarySDLInfo
                        {
                            Tags = new List<string>
                            {
                                ""
                            }
                        };
                        extraContentInfo.Add("__required", requiredSdl);
                    }
                    if (!includeRequiredSdl && extraContentInfo.ContainsKey("__required"))
                    {
                        extraContentInfo.Remove("__required");
                    }
                }
            }
            if (manifest.Game != null && manifest.Game.Owned_dlc.Count > 0)
            {
                foreach (var dlc in manifest.Game.Owned_dlc.OrderBy(obj => obj.Title))
                {
                    if (!dlc.App_name.IsNullOrEmpty())
                    {
                        var dlcInfo = new LegendarySDLInfo
                        {
                            Name = dlc.Title.RemoveTrademarks(),
                            Is_dlc = true,
                            BaseGameID = installData.gameID
                        };
                        extraContentInfo.Add(dlc.App_name, dlcInfo);
                        var dlcData = new LegendaryGameInfo.Game
                        {
                            Title = dlcInfo.Name,
                            App_name = dlc.App_name
                        };
                        var dlcManifest = await GetGameInfo(dlcData);
                    }
                }
            }
            return extraContentInfo;
        }

        public static async Task<List<string>> GetRequiredSdlsTags(DownloadManagerData.Download installData)
        {
            var extraContentInfo = await GetExtraContentInfo(installData, true);
            var sdls = extraContentInfo.Where(i => i.Value.Is_dlc == false).ToList();
            var requiredSdls = new List<string>();
            if (extraContentInfo.ContainsKey("__required"))
            {
                foreach (var tag in extraContentInfo["__required"].Tags)
                {
                    requiredSdls.AddMissing(tag);
                }
            }
            return requiredSdls;
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

        public static async Task<LauncherVersion> GetVersionInfoContent()
        {
            var newVersionInfoContent = new LauncherVersion();
            var logger = LogManager.GetLogger();
            if (!IsInstalled)
            {
                ShowNotInstalledError();
                return newVersionInfoContent;
            }
            var cacheVersionPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            if (!Directory.Exists(cacheVersionPath))
            {
                Directory.CreateDirectory(cacheVersionPath);
            }
            var cacheVersionFile = Path.Combine(cacheVersionPath, "legendaryVersion.json");
            string content = null;
            if (File.Exists(cacheVersionFile))
            {
                if (File.GetLastWriteTime(cacheVersionFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheVersionFile);
                }
            }
            bool correctJson = false;
            if (File.Exists(cacheVersionFile))
            {
                content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out LauncherVersion versionInfoContent))
                {
                    if (versionInfoContent != null && versionInfoContent.Html_url != null && versionInfoContent.Tag_name != null)
                    {
                        correctJson = true;
                        newVersionInfoContent = versionInfoContent;
                    }
                }
            }
            if (!File.Exists(cacheVersionFile) || !correctJson)
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Vivaldi/5.5.2805.50");
                var repoOwner = "derrod";
                if (LauncherPath == HeroicLegendaryPath)
                {
                    repoOwner = "Heroic-Games-Launcher";
                }
                var response = await httpClient.GetAsync($"https://api.github.com/repos/{repoOwner}/legendary/releases/latest");
                if (response.IsSuccessStatusCode)
                {
                    content = await response.Content.ReadAsStringAsync();
                    if (!Directory.Exists(cacheVersionPath))
                    {
                        Directory.CreateDirectory(cacheVersionPath);
                    }
                    File.WriteAllText(cacheVersionFile, content);
                    newVersionInfoContent = Serialization.FromJson<LauncherVersion>(content);
                }
                else
                {
                    logger.Error("An error occurred while downloading Legendary's version info.");
                }
                httpClient.Dispose();
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

        public static void ShowNotInstalledError()
        {
            var playniteAPI = API.Instance;
            var options = new List<MessageBoxOption>
            {
                new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteInstallGame)),
                new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteOKLabel)),
            };
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled), "Legendary (Epic Games) library integration", MessageBoxImage.Information, options);
            if (result == options[0])
            {
                Playnite.Commands.GlobalCommands.NavigateUrl("https://github.com/hawkeye116477/playnite-legendary-plugin/wiki/Troubleshooting#legendary-launcher-is-not-installed");
            }
        }

        public static bool DefaultPlaytimeSyncEnabled
        {
            get
            {
                var playniteAPI = API.Instance;
                var playtimeSyncEnabled = false;
                if (playniteAPI.ApplicationSettings.PlaytimeImportMode != PlaytimeImportMode.Never)
                {
                    playtimeSyncEnabled = true;
                }
                return playtimeSyncEnabled;
            }
        }

        public static Installed GetInstalledInfo(string gameId)
        {
            var installedAppList = GetInstalledAppList();
            var installedInfo = new Installed();
            if (installedAppList.ContainsKey(gameId))
            {
                installedInfo = installedAppList[gameId];
            }
            return installedInfo;
        }
    }
}
