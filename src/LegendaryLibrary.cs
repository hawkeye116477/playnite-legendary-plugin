using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryLibraryNS
{
    [LoadPlugin]
    public class LegendaryLibrary : LibraryPluginBase<LegendaryLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public static LegendaryLibrary Instance { get; set; }
        public static bool LegendaryGameInstaller { get; internal set; }
        private LegendaryDownloadManager LegendaryDownloadManager;
        private SidebarItem downloadManagerSidebarItem;
        public CommonHelpers commonHelpers { get; set; }

        public LegendaryLibrary(IPlayniteAPI api) : base(
            "Legendary (Epic)",
            Guid.Parse("EAD65C3B-2F8F-4E37-B4E6-B3DE6BE540C6"),
            new LibraryPluginProperties { CanShutdownClient = false, HasSettings = true },
            new LegendaryClient(),
            LegendaryLauncher.Icon,
            (_) => new LegendaryLibrarySettingsView(),
            api)
        {
            Instance = this;
            commonHelpers = new CommonHelpers(Instance);
            SettingsViewModel = new LegendaryLibrarySettingsViewModel(this, api);
            Load3pLocalization();
            commonHelpers.LoadNeededResources();
            LegendaryDownloadManager = new LegendaryDownloadManager();
        }

        public static LegendaryLibrarySettings GetSettings()
        {
            return Instance.SettingsViewModel?.Settings ?? null;
        }

        public static SidebarItem GetPanel()
        {
            if (Instance.downloadManagerSidebarItem == null)
            {
                Instance.downloadManagerSidebarItem = new SidebarItem
                {
                    Title = LocalizationManager.Instance.GetString(LOC.CommonPanel),
                    Icon = LegendaryLauncher.Icon,
                    Type = SiderbarItemType.View,
                    Opened = () => GetLegendaryDownloadManager(),
                    ProgressValue = 0,
                    ProgressMaximum = 100,
                };
            }
            return Instance.downloadManagerSidebarItem;
        }

        public static LegendaryDownloadManager GetLegendaryDownloadManager()
        {
            return Instance.LegendaryDownloadManager;
        }

        internal Dictionary<string, GameMetadata> GetInstalledGames()
        {
            var games = new Dictionary<string, GameMetadata>();
            var appList = LegendaryLauncher.GetInstalledAppList();

            foreach (KeyValuePair<string, Installed> d in appList)
            {
                var app = d.Value;

                if (app.App_name.StartsWith("UE_"))
                {
                    continue;
                }

                // DLC
                if (app.Is_dlc && app.Executable.IsNullOrEmpty())
                {
                    continue;
                }

                var installLocation = app.Install_path;
                var gameName = app?.Title ?? Path.GetFileName(installLocation);
                if (installLocation.IsNullOrEmpty())
                {
                    continue;
                }

                installLocation = Paths.FixSeparators(installLocation);
                if (!Directory.Exists(installLocation))
                {
                    logger.Error($"Epic game {gameName} installation directory {installLocation} not detected.");
                    continue;
                }

                var game = new GameMetadata()
                {
                    Source = new MetadataNameProperty("Epic"),
                    GameId = app.App_name,
                    Name = gameName,
                    Version = app.Version,
                    InstallSize = (ulong?)app.Install_size,
                    InstallDirectory = installLocation,
                    IsInstalled = true,
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                game.Name = game.Name.RemoveTrademarks();
                games.Add(game.GameId, game);
            }

            return games;
        }

        internal async Task<List<GameMetadata>> GetLibraryGames(CancellationToken cancelToken)
        {
            var cacheDir = GetCachePath("catalogcache");
            var games = new List<GameMetadata>();
            var accountApi = new EpicAccountClient(PlayniteApi);
            var assets = await accountApi.GetAssets();
            if (!assets?.Any() == true)
            {
                Logger.Warn("Found no assets on Epic accounts.");
            }

            if (GetSettings().ImportEALauncherGames)
            {
                var ignoreList = new List<string>();
                foreach (var gameAsset in assets)
                {
                    ignoreList.Add(gameAsset.appName);
                }
                var nonAssets = await accountApi.GetLibraryItems(ignoreList);
                assets.AddRange(nonAssets);
            }

            var playtimeItems = await accountApi.GetPlaytimeItems();
            foreach (var gameAsset in assets.Where(a => a.@namespace != "ue"))
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                var cacheFile = Paths.GetSafePathName($"{gameAsset.@namespace}_{gameAsset.catalogItemId}_{gameAsset.buildVersion}.json");
                cacheFile = Path.Combine(cacheDir, cacheFile);
                var catalogItem = accountApi.GetCatalogItem(gameAsset.@namespace, gameAsset.catalogItemId, cacheFile);
                if (catalogItem?.categories?.Any(a => a.path == "applications") != true)
                {
                    continue;
                }

                if ((catalogItem?.mainGameItem != null) && (catalogItem.categories?.Any(a => a.path == "addons/launchable") == false))
                {
                    continue;
                }

                if (catalogItem?.categories?.Any(a => a.path == "digitalextras") == true)
                {
                    continue;
                }

                if (!GetSettings().ImportEALauncherGames)
                {
                    if ((catalogItem?.customAttributes?.ThirdPartyManagedApp != null) && (catalogItem?.customAttributes?.ThirdPartyManagedApp.value.ToLower() == "the ea app" || catalogItem?.customAttributes?.ThirdPartyManagedApp.value.ToLower() == "origin"))
                    {
                        continue;
                    }
                }

                if (!GetSettings().ImportUbisoftLauncherGames)
                {
                    if ((catalogItem?.customAttributes?.PartnerLinkType != null) && (catalogItem?.customAttributes.PartnerLinkType.value == "ubisoft"))
                    {
                        continue;
                    }
                }

                var newGame = new GameMetadata
                {
                    Source = new MetadataNameProperty("Epic"),
                    GameId = gameAsset.appName,
                    Name = catalogItem.title.RemoveTrademarks(),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                var gameSettings = LegendaryGameSettingsView.LoadGameSettings(gameAsset.appName);
                var playtimeSyncEnabled = GetSettings().SyncPlaytime;
                if (gameSettings.AutoSyncPlaytime != null)
                {
                    playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                }
                if (playtimeSyncEnabled)
                {
                    var playtimeItem = playtimeItems?.FirstOrDefault(x => x.artifactId == gameAsset.appName);
                    if (playtimeItem != null)
                    {
                        newGame.Playtime = playtimeItem.totalTime;
                    }
                }

                games.Add(newGame);
            }

            return games;
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameMetadata>();
            var installedGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;

            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    installedGames = GetInstalledGames();
                    Logger.Debug($"Found {installedGames.Count} installed Epic games.");
                    allGames.AddRange(installedGames.Values.ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import installed Epic games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames(args.CancelToken).GetAwaiter().GetResult();
                    Logger.Debug($"Found {libraryGames.Count} library Epic games.");

                    if (!SettingsViewModel.Settings.ImportUninstalledGames)
                    {
                        libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.GameId)).ToList();
                    }

                    foreach (var game in libraryGames)
                    {
                        if (installedGames.TryGetValue(game.GameId, out var installed))
                        {
                            installed.Playtime = game.Playtime;
                            installed.LastActivity = game.LastActivity;
                            installed.Name = game.Name;
                        }
                        else
                        {
                            allGames.Add(game);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import linked account Epic games details.");
                    importError = e;
                }
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLibraryImportError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)Name }) +
                    Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return allGames;
        }

        public string GetCachePath(string dirName)
        {
            return Path.Combine(GetPluginUserDataPath(), dirName);
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new LegendaryInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new LegendaryUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }
            yield return new LegendaryPlayController(args.Game);
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new EpicMetadataProvider(PlayniteApi);
        }

        public void Load3pLocalization()
        {
            var currentLanguage = PlayniteApi.ApplicationSettings.Language;
            LocalizationManager.Instance.SetLanguage(currentLanguage);
            var commonFluentArgs = new Dictionary<string, IFluentType>
            {
                { "launcherName", (FluentString)"Legendary" },
                { "pluginShortName", (FluentString)"Legendary" },
                { "originalPluginShortName", (FluentString)"Epic" },
                { "updatesSourceName", (FluentString)"Epic Games" }
            };
            LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return downloadManagerSidebarItem;
        }

        public bool StopDownloadManager(bool displayConfirm = false)
        {
            LegendaryDownloadManager downloadManager = GetLegendaryDownloadManager();
            var runningAndQueuedDownloads = downloadManager.downloadManagerData.downloads.Where(i => i.status == DownloadStatus.Running
                                                                                                     || i.status == DownloadStatus.Queued).ToList();
            if (runningAndQueuedDownloads.Count > 0)
            {
                if (displayConfirm)
                {
                    var stopConfirm = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonInstanceNotice), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (stopConfirm == MessageBoxResult.No)
                    {
                        return false;
                    }
                }
                foreach (var download in runningAndQueuedDownloads)
                {
                    if (download.status == DownloadStatus.Running)
                    {
                        downloadManager.gracefulInstallerCTS?.Cancel();
                        downloadManager.gracefulInstallerCTS?.Dispose();
                        downloadManager.forcefulInstallerCTS?.Dispose();
                    }
                    download.status = DownloadStatus.Paused;
                }
                downloadManager.SaveData();
            }
            return true;
        }

        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var globalSettings = GetSettings();
            if (globalSettings != null)
            {
                if (globalSettings.GamesUpdatePolicy != UpdatePolicy.Never)
                {
                    var nextGamesUpdateTime = globalSettings.NextGamesUpdateTime;
                    if (nextGamesUpdateTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextGamesUpdateTime)
                        {
                            globalSettings.NextGamesUpdateTime = GetNextUpdateCheckTime(globalSettings.GamesUpdatePolicy);
                            SavePluginSettings(globalSettings);
                            LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                            var gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates();
                            if (gamesUpdates.Count > 0)
                            {
                                var successUpdates = gamesUpdates.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                                if (successUpdates.Count > 0)
                                {
                                    if (globalSettings.AutoUpdateGames)
                                    {
                                        await legendaryUpdateController.UpdateGame(successUpdates, "", true);
                                    }
                                    else
                                    {
                                        Window window = null;
                                        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen && PlayniteApi.ApplicationInfo.ApplicationVersion.Minor < 36)
                                        {
                                            window = new Window();
                                        }
                                        else
                                        {
                                            window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                                            {
                                                ShowMaximizeButton = false,
                                            });
                                        }
                                        window.DataContext = successUpdates;
                                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                                        window.Content = new LegendaryUpdater();
                                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                                        window.SizeToContent = SizeToContent.WidthAndHeight;
                                        window.MinWidth = 600;
                                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                        window.ShowDialog();
                                    }
                                }
                                else
                                {
                                    PlayniteApi.Notifications.Add(new NotificationMessage("LegendaryGamesUpdateCheckFail",
                                                                                          $"{Name} {Environment.NewLine}{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage)}",
                                                                                          NotificationType.Error));
                                }
                            }
                        }
                    }
                }
                if (globalSettings.LauncherUpdatePolicy != UpdatePolicy.Never && LegendaryLauncher.IsInstalled)
                {
                    var nextLauncherUpdateTime = globalSettings.NextLauncherUpdateTime;
                    if (nextLauncherUpdateTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextLauncherUpdateTime)
                        {
                            globalSettings.NextLauncherUpdateTime = GetNextUpdateCheckTime(globalSettings.LauncherUpdatePolicy);
                            SavePluginSettings(globalSettings);
                            var versionInfoContent = await LegendaryLauncher.GetVersionInfoContent();
                            if (versionInfoContent.Tag_name != null)
                            {
                                var newVersion = new Version(versionInfoContent.Tag_name);
                                var oldVersion = new Version(await LegendaryLauncher.GetLauncherVersion());
                                if (oldVersion.CompareTo(newVersion) < 0)
                                {
                                    var options = new List<MessageBoxOption>
                                    {
                                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonViewChangelog), true),
                                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel), false, true),
                                    };
                                    var launcherFluentArgs = new Dictionary<string, IFluentType>
                                    {
                                        ["appName"] = (FluentString)"Legendary Launcher",
                                        ["appVersion"] = (FluentString)newVersion.ToString()
                                    };
                                    var result = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNewVersionAvailable, launcherFluentArgs), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                                    if (result == options[0])
                                    {
                                        var changelogURL = versionInfoContent.Html_url;
                                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            StopDownloadManager();
            LegendaryDownloadManager downloadManager = GetLegendaryDownloadManager();
            var settings = GetSettings();
            if (settings != null)
            {
                if (settings.AutoRemoveCompletedDownloads != ClearCacheTime.Never)
                {
                    var nextRemovingCompletedDownloadsTime = settings.NextRemovingCompletedDownloadsTime;
                    if (nextRemovingCompletedDownloadsTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextRemovingCompletedDownloadsTime)
                        {
                            foreach (var downloadItem in downloadManager.downloadManagerData.downloads.ToList())
                            {
                                if (downloadItem.status == DownloadStatus.Completed)
                                {
                                    downloadManager.downloadManagerData.downloads.Remove(downloadItem);
                                    downloadManager.downloadsChanged = true;
                                }
                            }
                            settings.NextRemovingCompletedDownloadsTime = GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                            SavePluginSettings(settings);
                        }
                    }
                    else
                    {
                        settings.NextRemovingCompletedDownloadsTime = GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                        SavePluginSettings(settings);
                    }
                }

                if (settings.AutoClearCache != ClearCacheTime.Never)
                {
                    var nextClearingTime = settings.NextClearingTime;
                    if (nextClearingTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextClearingTime)
                        {
                            LegendaryLauncher.ClearCache();
                            settings.NextClearingTime = GetNextClearingTime(settings.AutoClearCache);
                            SavePluginSettings(settings);
                        }
                    }
                    else
                    {
                        settings.NextClearingTime = GetNextClearingTime(settings.AutoClearCache);
                        SavePluginSettings(settings);
                    }
                }
            }
            downloadManager.SaveData();
        }

        public static long GetNextUpdateCheckTime(UpdatePolicy frequency)
        {
            DateTimeOffset? updateTime = null;
            DateTimeOffset now = DateTime.UtcNow;
            switch (frequency)
            {
                case UpdatePolicy.PlayniteLaunch:
                    updateTime = now;
                    break;
                case UpdatePolicy.Day:
                    updateTime = now.AddDays(1);
                    break;
                case UpdatePolicy.Week:
                    updateTime = now.AddDays(7);
                    break;
                case UpdatePolicy.Month:
                    updateTime = now.AddMonths(1);
                    break;
                case UpdatePolicy.ThreeMonths:
                    updateTime = now.AddMonths(3);
                    break;
                case UpdatePolicy.SixMonths:
                    updateTime = now.AddMonths(6);
                    break;
                default:
                    break;
            }
            return updateTime?.ToUnixTimeSeconds() ?? 0;
        }

        public static long GetNextClearingTime(ClearCacheTime frequency)
        {
            DateTimeOffset? clearingTime = null;
            DateTimeOffset now = DateTime.UtcNow;
            switch (frequency)
            {
                case ClearCacheTime.Day:
                    clearingTime = now.AddDays(1);
                    break;
                case ClearCacheTime.Week:
                    clearingTime = now.AddDays(7);
                    break;
                case ClearCacheTime.Month:
                    clearingTime = now.AddMonths(1);
                    break;
                case ClearCacheTime.ThreeMonths:
                    clearingTime = now.AddMonths(3);
                    break;
                case ClearCacheTime.SixMonths:
                    clearingTime = now.AddMonths(6);
                    break;
                default:
                    break;
            }
            return clearingTime?.ToUnixTimeSeconds() ?? 0;
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var legendaryGames = args.Games.Where(i => i.PluginId == Id).ToList();
            if (legendaryGames.Count > 0)
            {
                if (legendaryGames.Count == 1)
                {
                    Game game = legendaryGames.FirstOrDefault();
                    if (game.IsInstalled)
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.CommonLauncherSettings),
                            Icon = "ModifyLaunchSettingsIcon",
                            Action = (args) =>
                            {
                                if (!LegendaryLauncher.IsInstalled)
                                {
                                    LegendaryLauncher.ShowNotInstalledError();
                                    return;
                                }
                                Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                                {
                                    ShowMaximizeButton = false
                                });
                                window.DataContext = game;
                                window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonLauncherSettings)} - {game.Name}";
                                window.Content = new LegendaryGameSettingsView();
                                window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                                window.SizeToContent = SizeToContent.WidthAndHeight;
                                window.MinWidth = 600;
                                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                window.ShowDialog();
                            }
                        };
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCheckForUpdates),
                            Icon = "UpdateDbIcon",
                            Action = (args) =>
                            {
                                if (!LegendaryLauncher.IsInstalled)
                                {
                                    LegendaryLauncher.ShowNotInstalledError();
                                    return;
                                }

                                LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                                var gamesToUpdate = new Dictionary<string, UpdateInfo>();
                                GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false) { IsIndeterminate = true };
                                PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                                {
                                    gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(game.Name, game.GameId);
                                }, updateCheckProgressOptions);
                                if (gamesToUpdate.Count > 0)
                                {
                                    var successUpdates = gamesToUpdate.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                                    if (successUpdates.Count > 0)
                                    {
                                        Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                                        {
                                            ShowMaximizeButton = false,
                                        });
                                        window.DataContext = successUpdates;
                                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                                        window.Content = new LegendaryUpdater();
                                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                                        window.SizeToContent = SizeToContent.WidthAndHeight;
                                        window.MinWidth = 600;
                                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                        window.ShowDialog();
                                    }
                                    else
                                    {
                                        PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage), game.Name);
                                    }
                                }
                                else
                                {
                                    PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), game.Name);
                                }
                            }
                        };
                    }
                    else
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.CommonImportInstalledGame),
                            Icon = "AddGameIcon",
                            Action = async (args) =>
                            {
                                if (!LegendaryLauncher.IsInstalled)
                                {
                                    LegendaryLauncher.ShowNotInstalledError();
                                    return;
                                }

                                var path = PlayniteApi.Dialogs.SelectFolder();
                                if (path != "")
                                {
                                    bool canContinue = StopDownloadManager(true);
                                    if (!canContinue)
                                    {
                                        return;
                                    }
                                    await LegendaryDownloadManager.WaitUntilLegendaryCloses();
                                    GlobalProgressOptions importProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonImportingGame, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }), false) { IsIndeterminate = true };
                                    PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                                    {
                                        var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                                 .WithArguments(new[] { "-y", "import", game.GameId, path })
                                                                 .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                                                 .AddCommandToLog()
                                                                 .WithValidation(CommandResultValidation.None)
                                                                 .ExecuteBufferedAsync();
                                        logger.Debug("[Legendary] " + importCmd.StandardError);
                                        if (importCmd.StandardError.Contains("has been imported"))
                                        {
                                            var installedAppList = LegendaryLauncher.GetInstalledAppList();
                                            if (installedAppList.ContainsKey(game.GameId))
                                            {
                                                var installedGameInfo = installedAppList[game.GameId];
                                                game.InstallDirectory = installedGameInfo.Install_path;
                                                game.Version = installedGameInfo.Version;
                                                game.InstallSize = (ulong?)installedGameInfo.Install_size;
                                                game.IsInstalled = true;
                                            }
                                            PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonImportFinished));
                                        }
                                        else
                                        {
                                            PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.LegendaryGameImportFailure, new Dictionary<string, IFluentType> { ["reason"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                                        }
                                    }, importProgressOptions);
                                }
                            }
                        };
                    }
                    yield return new GameMenuItem
                    {
                        Description = LocalizationManager.Instance.GetString(LOC.CommonManageDlcs),
                        Icon = "AddonsIcon",
                        Action = (args) =>
                        {
                            if (!LegendaryLauncher.IsInstalled)
                            {
                                LegendaryLauncher.ShowNotInstalledError();
                                return;
                            }

                            Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                            {
                                ShowMaximizeButton = false,
                            });
                            window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonManageDlcs)} - {game.Name}";
                            window.DataContext = game;
                            window.Content = new LegendaryDlcManager();
                            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                            window.SizeToContent = SizeToContent.WidthAndHeight;
                            window.MinWidth = 600;
                            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            window.ShowDialog();
                        }
                    };
                    if (game.IsInstalled)
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.CommonMove),
                            Icon = "MoveIcon",
                            Action = (args) =>
                            {
                                if (!LegendaryLauncher.IsInstalled)
                                {
                                    LegendaryLauncher.ShowNotInstalledError();
                                    return;
                                }

                                var newPath = PlayniteApi.Dialogs.SelectFolder();
                                if (newPath != "")
                                {
                                    var oldPath = game.InstallDirectory;
                                    if (Directory.Exists(oldPath) && Directory.Exists(newPath))
                                    {
                                        string sepChar = Path.DirectorySeparatorChar.ToString();
                                        string altChar = Path.AltDirectorySeparatorChar.ToString();
                                        if (!oldPath.EndsWith(sepChar) && !oldPath.EndsWith(altChar))
                                        {
                                            oldPath += sepChar;
                                        }
                                        var folderName = Path.GetFileName(Path.GetDirectoryName(oldPath));
                                        newPath = Path.Combine(newPath, folderName);
                                        var moveFluentArgs = new Dictionary<string, IFluentType>
                                        {
                                            ["appName"] = (FluentString)game.Name,
                                            ["path"] = (FluentString)newPath
                                        };
                                        var moveConfirm = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMoveConfirm, moveFluentArgs), LocalizationManager.Instance.GetString(LOC.CommonMove), MessageBoxButton.YesNo, MessageBoxImage.Question);
                                        if (moveConfirm == MessageBoxResult.Yes)
                                        {
                                            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMovingGame, moveFluentArgs), false);
                                            PlayniteApi.Dialogs.ActivateGlobalProgress((a) =>
                                            {
                                                a.ProgressMaxValue = 3;
                                                a.CurrentProgressValue = 0;
                                                _ = (Application.Current.Dispatcher?.BeginInvoke((Action)async delegate
                                                {
                                                    try
                                                    {
                                                        bool canContinue = StopDownloadManager(true);
                                                        if (!canContinue)
                                                        {
                                                            return;
                                                        }
                                                        await LegendaryDownloadManager.WaitUntilLegendaryCloses();
                                                        Directory.Move(oldPath, newPath);
                                                        a.CurrentProgressValue = 1;
                                                        var rewriteResult = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                                                     .WithArguments(new[] { "move", game.GameId, newPath, "--skip-move" })
                                                                                     .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                                                                     .AddCommandToLog()
                                                                                     .ExecuteBufferedAsync();
                                                        var errorMessage = rewriteResult.StandardError;
                                                        if (rewriteResult.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                                                        {
                                                            logger.Error($"[Legendary] {errorMessage}");
                                                            logger.Error($"[Legendary] exit code: {rewriteResult.ExitCode}");
                                                        }
                                                        a.CurrentProgressValue = 2;
                                                        game.InstallDirectory = newPath;
                                                        PlayniteApi.Database.Games.Update(game);
                                                        a.CurrentProgressValue = 3;
                                                        PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMoveGameSuccess, moveFluentArgs));
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        a.CurrentProgressValue = 3;
                                                        PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMoveGameError, moveFluentArgs));
                                                        logger.Error(e.Message);
                                                    }
                                                }));
                                            }, globalProgressOptions);
                                        }
                                    }
                                }
                            }
                        };
                    }
                }

                var notInstalledLegendaryGames = legendaryGames.Where(i => i.IsInstalled == false).ToList();
                if (notInstalledLegendaryGames.Count > 0)
                {
                    if (legendaryGames.Count > 1)
                    {
                        var installData = new List<DownloadManagerData.Download>();
                        foreach (var notInstalledLegendaryGame in notInstalledLegendaryGames)
                        {
                            var installProperties = new DownloadProperties { downloadAction = DownloadAction.Install };
                            installData.Add(new DownloadManagerData.Download { gameID = notInstalledLegendaryGame.GameId, name = notInstalledLegendaryGame.Name, downloadProperties = installProperties });
                        }
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame),
                            Icon = "InstallIcon",
                            Action = (args) =>
                            {
                                LegendaryInstallController.LaunchInstaller(installData);
                            }
                        };
                    }
                }
                var installedLegendaryGames = legendaryGames.Where(i => i.IsInstalled).ToList();
                if (installedLegendaryGames.Count > 0)
                {
                    yield return new GameMenuItem
                    {
                        Description = LocalizationManager.Instance.GetString(LOC.CommonRepair),
                        Icon = "RepairIcon",
                        Action = (args) =>
                        {
                            if (!LegendaryLauncher.IsInstalled)
                            {
                                LegendaryLauncher.ShowNotInstalledError();
                                return;
                            }

                            Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                            {
                                ShowMaximizeButton = false,
                            });

                            var installData = new List<DownloadManagerData.Download>();
                            foreach (var game in installedLegendaryGames)
                            {
                                var installProperties = new DownloadProperties { downloadAction = DownloadAction.Repair };
                                installData.Add(new DownloadManagerData.Download { gameID = game.GameId, name = game.Name, downloadProperties = installProperties });
                            }
                            window.DataContext = installData;
                            window.Content = new LegendaryGameInstaller();
                            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                            window.SizeToContent = SizeToContent.WidthAndHeight;
                            window.MinWidth = 600;
                            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            var title = LocalizationManager.Instance.GetString(LOC.CommonRepair);
                            if (installedLegendaryGames.Count == 1)
                            {
                                title = installedLegendaryGames[0].Name;
                            }
                            window.Title = title;
                            window.ShowDialog();
                        }
                    };
                    if (legendaryGames.Count > 1)
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                            Icon = "UninstallIcon",
                            Action = (args) =>
                            {
                                LegendaryUninstallController.LaunchUninstaller(installedLegendaryGames);
                            }
                        };
                    }
                }
            }
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = LocalizationManager.Instance.GetString(LOC.CommonCheckForGamesUpdatesButton),
                MenuSection = $"@{Instance.Name}",
                Icon = "UpdateDbIcon",
                Action = (args) =>
                {
                    if (!LegendaryLauncher.IsInstalled)
                    {
                        LegendaryLauncher.ShowNotInstalledError();
                        return;
                    }

                    var gamesUpdates = new Dictionary<string, UpdateInfo>();
                    LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                    GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false) { IsIndeterminate = true };
                    PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                    {
                        gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates();
                    }, updateCheckProgressOptions);
                    if (gamesUpdates.Count > 0)
                    {
                        var successUpdates = gamesUpdates.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                        if (successUpdates.Count > 0)
                        {
                            Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                            {
                                ShowMaximizeButton = false,
                            });
                            window.DataContext = successUpdates;
                            window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                            window.Content = new LegendaryUpdater();
                            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                            window.SizeToContent = SizeToContent.WidthAndHeight;
                            window.MinWidth = 600;
                            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            window.ShowDialog();
                        }
                        else
                        {
                            PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage));
                        }
                    }
                    else
                    {
                        PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable));
                    }
                }
            };

            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                yield return new MainMenuItem
                {
                    Description = LocalizationManager.Instance.GetString(LOC.CommonDownloadManager),
                    MenuSection = $"@{Instance.Name}",
                    Icon = "InstallIcon",
                    Action = (args) =>
                    {
                        Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                        {
                            ShowMaximizeButton = true,
                        });
                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonPanel)}";
                        window.Content = GetLegendaryDownloadManager();
                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                        window.ShowDialog();
                    }
                };
            }
        }

    }
}
