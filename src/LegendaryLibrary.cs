using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite;
using Playnite.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommonPlugin.Resources;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;

namespace LegendaryLibraryNS
{
    public class LegendaryLibrary : Plugin, IUnifiedDownloadProvider
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        public static LegendaryLibrary? Instance { get; private set; }
        public CommonHelpers CommonHelpers { get; set; }
        public IUnifiedDownloadLogic UnifiedDownloadLogic { get; set; }
        public DownloadManagerData? PluginDownloadData { get; set; }
        public const string PluginId = "hawkeye116477.LegendaryLibrary";
        public static IPlayniteApi PlayniteApi { get; private set; } = null!;
        public LegendaryLibrarySettings Settings { get; set; } = new();
        private static readonly SpecImportableProperty PcSpecProperty = new("pc_windows");
        public const string LibraryName = "Legendary (Epic)";

        public LegendaryLibrary()
        {
            Instance = this;
            XamlId = "Legendary";
            LibrarySettings = new LibrarySupport
            {
                LibraryName = LibraryName,
                ClientName = "Comet",
                CanCloseOriginalClient = false,
                CanOpenOriginalClient = false,
                ProvidesStoreMetadata = true,
                CanImportPlaytime = true,
                CanImportPlaySessions = true,
            };
        }

        public override async Task InitializeAsync(InitializeArgs args)
        {
            PlayniteApi = args.Api;
            CommonHelpers = new CommonHelpers(PlayniteApi);
            Settings = LegendaryLibrarySettingsViewModel.LoadPluginSettings(PlayniteApi.UserDataDir);
            Load3PLocalization();
            CommonHelpers.LoadNeededResources();
            UnifiedDownloadLogic = new LegendaryDownloadLogic();
            PluginDownloadData = LoadSavedDownloadData();
            await Task.CompletedTask;
        }

        public void SavePluginSettings(LegendaryLibrarySettings settings)
        {
            var settingsFile = Path.Combine(PlayniteApi.UserDataDir, "settings.json");
            FileSystem.WriteStringToFile(settingsFile, Serialization.ToJson(settings, true));
        }

        private static DownloadManagerData LoadSavedDownloadData()
        {
            var downloadData = new DownloadManagerData();

            var dataDir = PlayniteApi.UserDataDir;
            var dataFile = Path.Combine(dataDir, "downloads.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() &&
                    Serialization.TryFromJson(content, out DownloadManagerData? newPluginDownloadData))
                {
                    if (newPluginDownloadData is { Downloads: not null })
                    {
                        correctJson = true;
                        downloadData = newPluginDownloadData;
                    }
                }
            }

            if (!correctJson)
            {
                downloadData = new DownloadManagerData
                {
                    Downloads = []
                };
            }

            return downloadData;
        }

        public void SaveDownloadData()
        {
            var commonHelpers = Instance?.CommonHelpers;
            if (PluginDownloadData != null)
            {
                commonHelpers?.SaveJsonSettingsToFile(PluginDownloadData, "", "downloads", true);
            }
        }

        public static LegendaryLibrarySettings? GetSettings()
        {
            return Instance?.Settings ?? null;
        }

        internal Dictionary<string, ImportableGame> GetInstalledGames()
        {
            var games = new Dictionary<string, ImportableGame>();
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
                    Logger.Error($"Epic game {gameName} installation directory {installLocation} not detected.");
                    continue;
                }

                var game = new ImportableGame(gameName, PluginId, app.App_name)
                {
                    Source = new IdImportableProperty("epic", "Epic"),
                    InstallState = InstallState.Installed,
                    //Version = app.Version,
                    //InstallSize = (ulong?)app.Install_size,
                    InstallDirectory = installLocation,
                    Platforms = [PcSpecProperty]
                };

                game.Name = game.Name.RemoveTrademarks();
                games.Add(game.GameId, game);
            }

            return games;
        }

        private async Task<Dictionary<string, ImportableGame>> GetLibraryGames(CancellationToken cancelToken)
        {
            var cacheDir = GetCachePath("catalogcache");
            var games = new Dictionary<string, ImportableGame>();
            var accountApi = new EpicAccountClient(PlayniteApi);
            var assets = await accountApi.GetLibraryItems();
            if (assets.Count <= 0)
            {
                Logger.Warn("Found no assets on Epic accounts.");
            }

            var playtimeItems = await accountApi.GetPlaytimeItems();
            if (assets.Count > 0)
            {
                foreach (var gameAsset in assets.Where(a => a.@namespace != "ue"))
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var cacheFile =
                        Paths.GetSafePathName(
                            $"{gameAsset.@namespace}_{gameAsset.catalogItemId}_{gameAsset.buildVersion}.json");
                    cacheFile = Path.Combine(cacheDir, cacheFile);
                    var catalogItem =
                        await accountApi.GetCatalogItem(gameAsset.@namespace, gameAsset.catalogItemId, cacheFile);

                    if (catalogItem == null)
                    {
                        continue;
                    }

                    if (catalogItem.categories?.Any(a => a.path == "applications") != true)
                    {
                        continue;
                    }

                    if ((catalogItem.mainGameItem != null) &&
                        (catalogItem.categories?.Any(a => a.path == "addons/launchable") == false))
                    {
                        continue;
                    }

                    if (catalogItem.categories?.Any(a =>
                            a.path == "digitalextras" || a.path == "plugins" || a.path == "plugins/engine") == true)
                    {
                        continue;
                    }

                    if (!GetSettings().ImportEALauncherGames)
                    {
                        if ((catalogItem?.customAttributes?.ThirdPartyManagedApp != null) &&
                            (catalogItem?.customAttributes?.ThirdPartyManagedApp.value.ToLower() == "the ea app" ||
                             catalogItem?.customAttributes?.ThirdPartyManagedApp.value.ToLower() == "origin"))
                        {
                            continue;
                        }
                    }

                    if (!GetSettings().ImportUbisoftLauncherGames)
                    {
                        if (catalogItem?.customAttributes?.PartnerLinkType is { value: "ubisoft" })
                        {
                            continue;
                        }
                    }

                    var newGame = new ImportableGame(catalogItem!.title.RemoveTrademarks(), PluginId, gameAsset.appName)
                    {
                        Source = new IdImportableProperty("epic", "Epic"),
                        Platforms = [PcSpecProperty]
                    };

                    var gameSettings = LegendaryGameSettingsView.LoadGameSettings(gameAsset.appName);
                    var playtimeSyncEnabled = GetSettings() is { SyncPlaytime: true };
                    if (gameSettings.AutoSyncPlaytime != null)
                    {
                        playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                    }

                    if (playtimeSyncEnabled)
                    {
                        var playtimeItem = playtimeItems?.FirstOrDefault(x => x.artifactId == gameAsset.appName);
                        if (playtimeItem != null)
                        {
                            newGame.PlayTime = (uint)(playtimeItem.totalTime);
                        }
                    }

                    games.TryAdd(newGame.GameId, newGame);
                }
            }

            return games;
        }

        public override async Task<List<ImportableGame>> GetGamesAsync(LibraryGetGamesArgs args)
        {
            const string importErrorMessageId = $"{PluginId}_libImportError";
            var allGames = new List<ImportableGame>();
            var installedGames = new Dictionary<string, ImportableGame>();
            Exception? importError = null;

            if (Settings.ImportInstalledGames)
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

            if (Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = await GetLibraryGames(args.CancelToken);
                    Logger.Debug($"Found {libraryGames.Count} library Epic games.");

                    if (!Settings.ImportUninstalledGames)
                    {
                        libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.Key)).ToDictionary();
                    }

                    foreach (var game in libraryGames)
                    {
                        if (installedGames.TryGetValue(game.Key, out var installed))
                        {
                            installed.PlayTime = game.Value.PlayTime;
                            installed.LastPlayedDate = game.Value.LastPlayedDate;
                            installed.Name = game.Value.Name;
                        }
                        else
                        {
                            allGames.Add(game.Value);
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
                    importErrorMessageId,
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLibraryImportError,
                        new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LibraryName }) +
                    Environment.NewLine + importError.Message,
                    NotificationSeverity.Error,
                    async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(LegendaryLibrary.PluginId)));
            }
            else
            {
                PlayniteApi.Notifications.Remove(importErrorMessageId);
            }

            return allGames;
        }

        public string GetCachePath(string dirName)
        {
            return Path.Combine(PlayniteApi.UserDataDir, dirName);
        }

        public override async Task<List<InstallController>> GetInstallActionsAsync(GetInstallActionsArgs args)
        {
            if (args.Game.LibraryId != PluginId)
            {
                return await base.GetInstallActionsAsync(args);
            }

            return [new LegendaryInstallController(args.Game)];
        }

        public override async Task<List<UninstallController>> GetUninstallActionsAsync(GetUninstallActionsArgs args)
        {
            if (args.Game.LibraryId != PluginId)
            {
                return await base.GetUninstallActionsAsync(args);
            }

            return [new LegendaryUninstallController(args.Game)];
        }

        public override async Task<List<PlayController>> GetPlayActionsAsync(GetPlayActionsArgs args)
        {
            if (args.Game.LibraryId != PluginId)
            {
                return await base.GetPlayActionsAsync(args);
            }

            return [new LegendaryPlayController(args.Game)];
        }

        public override async Task<MetadataProvider?> GetMetadataProviderAsync(GetMetadataProviderArgs args)
        {
            return new EpicMetadataProvider();
        }

        private static void Load3PLocalization()
        {
            var currentLanguage = PlayniteApi.Settings.Language;
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

        public async Task<bool> StopDownloadManager(bool displayConfirm = false)
        {
            var unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(PlayniteApi);
            var allDownloads = unifiedDownloadManagerApi.GetAllDownloads();
            var runningAndQueuedDownloads = allDownloads.Where(i =>
                                                             i.Status == UnifiedDownloadStatus.Running ||
                                                             i.Status == UnifiedDownloadStatus.Queued)
                                                        .ToList();
            if (runningAndQueuedDownloads.Count > 0)
            {
                if (displayConfirm)
                {
                    var stopConfirm = await PlayniteApi.Dialogs.ShowMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.CommonInstanceNotice), "", MessageBoxButtons.YesNo,
                        MessageBoxSeverity.Question);
                    if (stopConfirm == Playnite.MessageBoxResult.No)
                    {
                        return false;
                    }
                }

                await unifiedDownloadManagerApi.PauseAllTasks(PluginId);
            }

            return true;
        }

        public override async Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
        {
            return new LegendaryLibrarySettingsViewModel(this);
        }

        public override async Task OnApplicationStartupAsync(OnApplicationStartupArgs args)
        {
            var globalSettings = GetSettings();
            if (globalSettings != null && globalSettings.GamesUpdatePolicy != UpdatePolicy.Never)
            {
                var nextGamesUpdateTime = globalSettings.NextGamesUpdateTime;
                var udmInstalled = PlayniteApi.Addons.Plugins.Any(plugin =>
                    plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
                if (nextGamesUpdateTime != 0 && udmInstalled)
                {
                    DateTimeOffset now = DateTime.UtcNow;
                    if (now.ToUnixTimeSeconds() >= nextGamesUpdateTime)
                    {
                        var installedGamesIds = LegendaryLauncher.GetInstalledAppList()
                                                                 .Select(x => x.Key)
                                                                 .ToList();
                        if (LegendaryLauncher.IsEOSOverlayInstalled)
                        {
                            installedGamesIds.Add("eos-overlay");
                        }

                        LegendaryLauncher.ClearSpecificGamesCache(installedGamesIds);
                        globalSettings.NextGamesUpdateTime =
                            GetNextUpdateCheckTime(globalSettings.GamesUpdatePolicy);
                        SavePluginSettings(globalSettings);
                        LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                        var gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates();
                        if (gamesUpdates.Count > 0)
                        {
                            var successUpdates = gamesUpdates.Where(i => i.Value.Success)
                                                             .ToDictionary(i => i.Key, i => i.Value);
                            if (successUpdates.Count > 0)
                            {
                                if (globalSettings.AutoUpdateGames)
                                {
                                    await legendaryUpdateController.UpdateGame(successUpdates, "", true);
                                }
                                else
                                {
                                    var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                                    {
                                        ShowMaximizeButton = false,
                                    });
                                    window.DataContext = successUpdates;
                                    window.Title =
                                        $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                                    window.Content = new LegendaryUpdater();
                                    window.Owner = PlayniteApi.GetLastActiveWindow();
                                    window.SizeToContent = SizeToContent.WidthAndHeight;
                                    window.MinWidth = 600;
                                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                    window.ShowDialog();
                                }
                            }
                            else
                            {
                                PlayniteApi.Notifications.Add(new NotificationMessage(
                                    "LegendaryGamesUpdateCheckFail",
                                    $"{LibraryName} {Environment.NewLine}{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage)}",
                                    NotificationSeverity.Error));
                            }
                        }
                    }
                }
            }

            if (globalSettings != null && globalSettings.LauncherUpdatePolicy != UpdatePolicy.Never &&
                LegendaryLauncher.IsInstalled)
            {
                var nextLauncherUpdateTime = globalSettings.NextLauncherUpdateTime;
                if (nextLauncherUpdateTime != 0)
                {
                    DateTimeOffset now = DateTime.UtcNow;
                    if (now.ToUnixTimeSeconds() >= nextLauncherUpdateTime)
                    {
                        globalSettings.NextLauncherUpdateTime =
                            GetNextUpdateCheckTime(globalSettings.LauncherUpdatePolicy);
                        SavePluginSettings(globalSettings);
                        await LegendaryLauncher.CheckForUpdates(false);
                    }
                }
            }
        }

        public override async Task OnApplicationShutdownAsync(OnApplicationShutdownArgs args)
        {
            var settings = GetSettings();
            if (settings != null && settings.AutoClearCache != ClearCacheTime.Never)
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

            SaveDownloadData();
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

        public override ICollection<MenuItemImpl>? GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var menuItems = new List<MenuItemImpl>();
            var legendaryGames = args.Games.Where(i => i.LibraryId == PluginId).ToList();
            if (legendaryGames.Count <= 0)
            {
                return menuItems;
            }

            var legendaryGameMenuActions = new LegendaryGameMenuActions(PlayniteApi, legendaryGames);
            if (legendaryGames.Count == 1)
            {
                var game = legendaryGames.First();
                if (game.InstallState == InstallState.Installed)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonLauncherSettings),
                        legendaryGameMenuActions.OpenLauncherSettingsWindow,
                        icon: CommonIcons.ModifyLaunchSettingsIcon
                    ));
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCheckForUpdates),
                        async () => { await legendaryGameMenuActions.OpenCheckForGamesUpdatesWindow(); },
                        icon: CommonIcons.UpdateIcon
                    ));
                }
                else if (game.InstallState == InstallState.Uninstalled)
                {
                    menuItems.Add(
                        new MenuItemImpl(
                            LocalizationManager.Instance.GetString(LOC.CommonImportInstalledGame),
                            async () => { await legendaryGameMenuActions.OpenImportGameWindow(); },
                            icon: CommonIcons.ImportGameIcon)
                    );
                }

                menuItems.Add(
                    new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonManageDlcs),
                        legendaryGameMenuActions.OpenDlcManagerWindow,
                        icon: CommonIcons.InstallIcon)
                );

                if (game.InstallState == InstallState.Installed)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonMove),
                        async () => { await legendaryGameMenuActions.OpenMoveGameWindow(); }
                      , icon: CommonIcons.MoveIcon)
                    );
                }
            }
            else
            {
                var notInstalledLegendaryGames =
                    legendaryGames.Where(i => i.InstallState == InstallState.Uninstalled).ToList();
                if (notInstalledLegendaryGames.Count > 0)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame),
                        () =>
                        {
                            var installData = new List<DownloadManagerData.Download>();
                            foreach (var notInstalledLegendaryGame in notInstalledLegendaryGames)
                            {
                                var installProperties = new DownloadProperties
                                    { DownloadAction = DownloadAction.Install };
                                installData.Add(new DownloadManagerData.Download
                                {
                                    GameId = notInstalledLegendaryGame.LibraryGameId,
                                    Name = notInstalledLegendaryGame.Name,
                                    DownloadProperties = installProperties
                                });
                            }

                            LegendaryInstallController.LaunchInstaller(installData);
                        }, icon: CommonIcons.InstallIcon
                    ));
                }

                var installedLegendaryGames =
                    legendaryGames.Where(i => i.InstallState == InstallState.Installed).ToList();
                if (installedLegendaryGames.Count > 0)
                {
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonRepair),
                        () =>
                        {
                            var installData = new List<DownloadManagerData.Download>();
                            foreach (var game in installedLegendaryGames)
                            {
                                var installProperties = new DownloadProperties
                                    { DownloadAction = DownloadAction.Repair };
                                installData.Add(new DownloadManagerData.Download
                                {
                                    GameId = game.LibraryGameId, Name = game.Name,
                                    DownloadProperties = installProperties
                                });
                            }

                            var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                            {
                                ShowMaximizeButton = false,
                            });
                            window.DataContext = installData;
                            window.Content = new LegendaryGameInstaller();
                            window.Owner = PlayniteApi.GetLastActiveWindow();
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
                        }, icon: CommonIcons.RepairIcon
                    ));
                    menuItems.Add(new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                        async () => { await LegendaryUninstallController.LaunchUninstaller(installedLegendaryGames); },
                        icon: CommonIcons.UninstallIcon
                    ));
                }
            }

            return menuItems;
        }

        public override ICollection<MenuItemImpl>? GetAppMenuItems(GetAppMenuItemsArgs args)
        {
            var items = new List<MenuItemImpl>
            {
                new(LocalizationManager.Instance.GetString(LOC.CommonCheckForGamesUpdatesButton),
                    async () =>
                    {
                        if (!LegendaryLauncher.IsInstalled)
                        {
                            await LegendaryLauncher.ShowNotInstalledError();
                            return;
                        }

                        var gamesUpdates = new Dictionary<string, UpdateInfo>();
                        LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                        GlobalProgressOptions updateCheckProgressOptions =
                            new GlobalProgressOptions(
                                LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates),
                                false) { IsIndeterminate = true };
                        await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions,
                            async (a) => { gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates(); }
                        );

                        var checkedGames = new List<Game>();

                        var appList = LegendaryLauncher.GetInstalledAppList();
                        foreach (var game in appList.OrderBy(item => item.Value.Title))
                        {
                            var gameId = game.Value.App_name;
                            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(gameId);
                            bool canUpdate = gameSettings.DisableGameVersionCheck != true;

                            if (canUpdate)
                            {
                                var checkedGame = new Game
                                {
                                    Id = game.Key,
                                    Name = game.Value.Title
                                };
                                checkedGames.Add(checkedGame);
                            }
                        }

                        if (LegendaryLauncher.IsEOSOverlayInstalled)
                        {
                            var checkedGame = new Game
                            {
                                Id = "eos-overlay",
                                Name = LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                                    new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" })
                            };
                            checkedGames.Add(checkedGame);
                        }

                        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                        {
                            ShowMaximizeButton = false,
                        });
                        window.DataContext = gamesUpdates;
                        window.Title =
                            $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                        window.Content = new LegendaryUpdater(checkedGames);
                        window.Owner = PlayniteApi.GetLastActiveWindow();
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                        window.MinWidth = 600;
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        window.ShowDialog();
                    },
                    icon: CommonIcons.UpdateIcon
                ),
                new(LocalizationManager.Instance.GetString(LOC.CommonFinishInstallation),
                    async () =>
                    {
                        var installedAppList = LegendaryLauncher.GetInstalledAppList();
                        var gamesToCompleteInstall = new Dictionary<string, Installed>();

                        foreach (var game in installedAppList)
                        {
                            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.Key);
                            if (gameSettings.InstallPrerequisites)
                            {
                                gamesToCompleteInstall.Add(game.Key, game.Value);
                            }
                        }

                        if (gamesToCompleteInstall.Count != 0)
                        {
                            var installProgressOptions =
                                new GlobalProgressOptions(
                                        LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation), false)
                                    { IsIndeterminate = false };

                            await PlayniteApi.Dialogs.ShowBlockingProgressAsync(installProgressOptions, (progress) =>
                                {
                                    progress.SetProgressMaxValue(gamesToCompleteInstall.Count);
                                    int current = 0;
                                    foreach (var game in gamesToCompleteInstall)
                                    {
                                        progress.SetText(
                                            $"{LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation)} ({game.Value.Title})");
                                        LegendaryLauncher.CompleteGameInstallation(game.Key);
                                        current++;
                                        progress.SetCrrentProgressValue(current);
                                    }
                                }
                            );
                        }
                        else
                        {
                            await PlayniteApi.Dialogs.ShowMessageAsync(
                                LocalizationManager.Instance.GetString(LOC.CommonNoFinishNeeded));
                        }
                    }, icon: CommonIcons.FinishInstallationIcon)
            };
            return items;
        }

        public override async Task OpenClientAsync(OpenClientArgs args)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                await PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled),
                    "Legendary (Epic Games) library integration", MessageBoxButtons.OK, MessageBoxSeverity.Error);
            }

            LegendaryLauncher.StartClient();
        }
    }
}