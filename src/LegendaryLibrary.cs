using System.IO;
using System.IO.Compression;
using System.Windows;
using CommonPlugin;
using CommonPlugin.Enums;
using CommonPlugin.Resources;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

public class LegendaryLibrary : Plugin
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    public static LegendaryLibrary Instance { get; private set; } = null!;
    public CommonHelpers CommonHelpers { get; set; } = null!;
    public LegendaryDownloadLogic UnifiedDownloadLogic { get; set; } = null!;
    public DownloadManagerData PluginDownloadData { get; set; } = null!;
    public const string PluginId = "hawkeye116477.LegendaryLibrary";

    public LegendaryLibrarySettings Settings { get; set; } = null!;

    public static IPlayniteApi PlayniteApi { get; private set; } = null!;
    public const string LibraryName = "Legendary (Epic)";
    public const string ShortPluginName = "Legendary";
    public IUnifiedDownloadManagerApi UnifiedDownloadManagerApi { get; set; } = null!;


    public LegendaryLibrary()
    {
        XamlId = ShortPluginName;
        LibrarySettings = new LibrarySupport
        {
            LibraryName = LibraryName,
            ClientName = "Legendary",
            CanCloseOriginalClient = false,
            CanOpenOriginalClient = true,
            ProvidesStoreMetadata = true,
            CanImportPlaytime = false,
            CanImportPlaySessions = false,
            HasCustomGameImport = true,
        };
        AchievementsSettings = new AchievementsSupport
        {
            SupportedLibraries = [PluginId],
        };
    }

    public override async Task InitializeAsync(InitializeArgs args)
    {
        Instance = this;
        PlayniteApi = args.Api;
        CommonHelpers = new CommonHelpers(PlayniteApi);
        Settings = LegendaryLibrarySettingsViewModel.LoadPluginSettings(PlayniteApi.UserDataDir);
        Load3PLocalization();
        CommonHelpers.LoadNeededResources();
        PluginDownloadData = LoadSavedDownloadData();
        UnifiedDownloadLogic = new LegendaryDownloadLogic();
    }

    public override async Task PostInitializationAsync(PostInitializationArgs args)
    {
        var result = await PlayniteApi.CallPluginAsync(new PluginCallRequestAsyncArgs(
            UnifiedDownloadManagerSharedProperties.Id,
            UnifiedDownloadManagerSharedProperties.GetApi));
        if (result is { Success: true, Value: IUnifiedDownloadManagerApi udmApi })
        {
            UnifiedDownloadManagerApi = udmApi;
        }
    }

    public override async Task<object?> OnPluginCallRequestAsync(PluginCallRequestAsyncArgs args)
    {
        return args.CallId == UnifiedDownloadManagerSharedProperties.GetDownloadLogic ? UnifiedDownloadLogic : null;
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
        var correctJson = false;
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
        var commonHelpers = Instance.CommonHelpers;
        commonHelpers.SaveJsonSettingsToFile(PluginDownloadData, "", "downloads", true);
    }

    public static LegendaryLibrarySettings GetSettings()
    {
        return Instance.Settings;
    }


    //public override async Task<List<ImportableGame>> GetGamesAsync(LibraryGetGamesArgs args)
    //{
    //    const string importErrorMessageId = $"{PluginId}_libImportError";
    //    var allGames = new List<ImportableGame>();
    //    var installedGames = new Dictionary<string, ImportableGame>();
    //    Exception? importError = null;

    //    if (Settings.ImportInstalledGames)
    //    {
    //        try
    //        {
    //            installedGames = GetInstalledGames();
    //            Logger.Debug($"Found {installedGames.Count} installed Epic games.");
    //            allGames.AddRange(installedGames.Values.ToList());
    //        }
    //        catch (Exception e)
    //        {
    //            Logger.Error(e, "Failed to import installed Epic games.");
    //            importError = e;
    //        }
    //    }

    //    if (Settings.ConnectAccount)
    //    {
    //        try
    //        {
    //            var libraryGames = await GetLibraryGames(args.CancelToken);
    //            Logger.Debug($"Found {libraryGames.Count} library Epic games.");

    //            if (!Settings.ImportUninstalledGames)
    //            {
    //                libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.Key)).ToDictionary();
    //            }

    //            foreach (var game in libraryGames)
    //            {
    //                if (installedGames.TryGetValue(game.Key, out var installed))
    //                {
    //                    installed.PlayTime = game.Value.PlayTime;
    //                    installed.LastPlayedDate = game.Value.LastPlayedDate;
    //                    installed.Name = game.Value.Name;
    //                }
    //                else
    //                {
    //                    allGames.Add(game.Value);
    //                }
    //            }
    //        }
    //        catch (Exception e)
    //        {
    //            Logger.Error(e, "Failed to import linked account Epic games details.");
    //            importError = e;
    //        }
    //    }

    //    if (importError != null)
    //    {
    //        PlayniteApi.Notifications.Add(new NotificationMessage(
    //            importErrorMessageId,
    //            LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLibraryImportError,
    //                new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LibraryName }) +
    //            Environment.NewLine + importError.Message,
    //            NotificationSeverity.Error,
    //            async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(LegendaryLibrary.PluginId)));
    //    }
    //    else
    //    {
    //        PlayniteApi.Notifications.Remove(importErrorMessageId);
    //    }

    //    return allGames;
    //}

    public override async Task<List<Game>> ImportGamesAsync(ImportGamesArgs args)
    {
        var addedGames = new List<Game>();
        var allGames = await LegendaryGames.GetAllGames(args.CancelToken);
        foreach (var newGame in allGames)
        {
            bool gameIsExcluded = false;
            if (args.Exclusions?.Count > 0 && args.Exclusions.FirstOrDefault(g => g.GameId == newGame.GameId) != null)
            {
                gameIsExcluded = true;
            }

            if (gameIsExcluded)
            {
                continue;
            }

            var existingGame =
                PlayniteApi.Library.Games.FirstOrDefault(a =>
                    a.LibraryGameId == newGame.GameId && a.LibraryId == PluginId);
            if (existingGame == null)
            {
                Logger.Info($"Adding new game {newGame.GameId} from {LibraryName} plugin.");
                try
                {
                    if (newGame.PlayTime != 0)
                    {
                        var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(newGame.GameId);
                        var playtimeSyncEnabled = GetSettings() is { SyncPlaytime: true };
                        if (gameSettings.AutoSyncPlaytime != null)
                        {
                            playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                        }

                        if (!playtimeSyncEnabled)
                        {
                            newGame.PlayTime = 0;
                        }
                    }

                    var importedGame = await PlayniteApi.Library.ImportGameAsync(newGame);
                    addedGames.Add(importedGame);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import game into database.");
                }
            }
            else
            {
                if (!existingGame.OverrideInstallState)
                {
                    if (existingGame.InstallState != newGame.InstallState)
                    {
                        existingGame.InstallState = newGame.InstallState;
                    }

                    if (!string.Equals(existingGame.InstallDirectory, newGame.InstallDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        existingGame.InstallDirectory = newGame.InstallDirectory;
                    }
                }

                var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(existingGame.LibraryGameId!);
                var playtimeSyncEnabled = GetSettings() is { SyncPlaytime: true };
                if (gameSettings.AutoSyncPlaytime != null)
                {
                    playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                }

                if (playtimeSyncEnabled && Settings?.PlayTimeImportMode == PlayTimeImportMode.Always && newGame.PlayTime > 0)
                {
                    if (existingGame.PlayTime != newGame.PlayTime)
                    {
                        existingGame.PlayTime = newGame.PlayTime;
                    }

                    // The LastPlayedDate value of the newGame is only applied if newer than
                    // the existing game, to prevent cases of DRM free games being launched without
                    // the client or offline, which would prevent the date from being updated in the service
                    if (newGame.LastPlayedDate != null &&
                        (existingGame.LastPlayedDate == null || newGame.LastPlayedDate > existingGame.LastPlayedDate))
                    {
                        existingGame.LastPlayedDate = newGame.LastPlayedDate;
                    }
                }

                if (existingGame.InstallState != InstallState.Installed && newGame.InstallSize > 0 &&
                    existingGame.InstallSize != newGame.InstallSize)
                {
                    existingGame.InstallSize = newGame.InstallSize;
                }

                await PlayniteApi.Library.Games.UpdateAsync(existingGame);
            }
        }

        return addedGames;
    }

    public static string GetCachePath(string dirName)
    {
        return Path.Combine(PlayniteApi.UserDataDir, "cache", dirName);
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
            { "launcherName", (FluentString)ShortPluginName },
            { "pluginShortName", (FluentString)ShortPluginName },
            { "originalPluginShortName", (FluentString)"Epic" },
            { "updatesSourceName", (FluentString)"Epic Games" }
        };
        LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
    }

    public async Task<bool> StopDownloadManager(bool displayConfirm = false)
    {
        var unifiedDownloadManagerApi = Instance.UnifiedDownloadManagerApi;
        var allDownloads = unifiedDownloadManagerApi.Downloads;
        var runningAndQueuedDownloads = allDownloads?.Where(i =>
                                                          i.Status == UnifiedDownloadStatus.Running ||
                                                          i.Status == UnifiedDownloadStatus.Queued)
                                                     .ToList();
        if (runningAndQueuedDownloads?.Count > 0)
        {
            if (displayConfirm)
            {
                var stopConfirm = await PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonInstanceNotice), "", MessageBoxButtons.YesNo,
                    MessageBoxSeverity.Question);
                if (stopConfirm == MessageBoxResult.No)
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
        if (globalSettings.GamesUpdatePolicy != UpdatePolicy.Never)
        {
            var nextGamesUpdateTime = globalSettings.NextGamesUpdateTime;
            var udmInstalled = PlayniteApi.Addons.Plugins.Any(plugin =>
                plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
            if (nextGamesUpdateTime != 0 && udmInstalled && LegendaryLauncher.IsInstalled)
            {
                DateTimeOffset now = DateTime.UtcNow;
                if (now.ToUnixTimeSeconds() >= nextGamesUpdateTime)
                {
                    var installedGamesIds = LegendaryLauncher.GetInstalledAppList()
                                                             .Select(x => x.Key)
                                                             .ToList();
                    if (LegendaryLauncher.IsEosOverlayInstalled)
                    {
                        installedGamesIds.Add("eos-overlay");
                    }

                    LegendaryGames.ClearSpecificGamesCache(installedGamesIds);
                    globalSettings.NextGamesUpdateTime =
                        GetNextUpdateCheckTime(globalSettings.GamesUpdatePolicy);
                    SavePluginSettings(globalSettings);
                    var legendaryUpdateController = new LegendaryUpdateController();
                    var gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates();
                    if (gamesUpdates.Count > 0)
                    {
                        var successUpdates = gamesUpdates.Where(i => i.Value.Status == UpdateStatus.Available)
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
                                    ShowMaximizeButton = false
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
                        else if (gamesUpdates.Any(i => i.Value.Status == UpdateStatus.Error))
                        {
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                "LegendaryGamesUpdateCheckFail",
                                $"{LibraryName} {Environment.NewLine}{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage)}",
                                NotificationSeverity.Error));
                            Logger.Error("Failed to check for games updates");
                        }
                    }
                }
            }
        }

        if (globalSettings.LauncherUpdatePolicy != UpdatePolicy.Never &&
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
        if (settings.AutoClearCache != ClearCacheTime.Never)
        {
            var nextClearingTime = settings.NextClearingTime;
            if (nextClearingTime != 0)
            {
                DateTimeOffset now = DateTime.UtcNow;
                if (now.ToUnixTimeSeconds() >= nextClearingTime)
                {
                    LegendaryGames.ClearCache();
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
        updateTime = frequency switch
        {
            UpdatePolicy.PlayniteLaunch => now,
            UpdatePolicy.Day => now.AddDays(1),
            UpdatePolicy.Week => now.AddDays(7),
            UpdatePolicy.Month => now.AddMonths(1),
            UpdatePolicy.ThreeMonths => now.AddMonths(3),
            UpdatePolicy.SixMonths => now.AddMonths(6),
            _ => updateTime
        };

        return updateTime?.ToUnixTimeSeconds() ?? 0;
    }

    public static long GetNextClearingTime(ClearCacheTime frequency)
    {
        DateTimeOffset? clearingTime = null;
        DateTimeOffset now = DateTime.UtcNow;
        clearingTime = frequency switch
        {
            ClearCacheTime.Day => now.AddDays(1),
            ClearCacheTime.Week => now.AddDays(7),
            ClearCacheTime.Month => now.AddMonths(1),
            ClearCacheTime.ThreeMonths => now.AddMonths(3),
            ClearCacheTime.SixMonths => now.AddMonths(6),
            _ => clearingTime
        };

        return clearingTime?.ToUnixTimeSeconds() ?? 0;
    }

    public override ICollection<MenuItemDescriptor> GetGameMenuItemDescriptors(GetGameMenuItemDescriptorsArgs args)
    {
        return
        [
            new MenuItemDescriptor($"gameMenu.{PluginId}", ShortPluginName),
        ];
    }

    public override ICollection<MenuItemImpl> GetGameMenuItems(GetGameMenuItemsArgs args)
    {
        var menuItems = new List<MenuItemImpl>();
        if (args.ItemId != $"gameMenu.{PluginId}")
        {
            return menuItems;
        }

        var legendaryGames = args.Games.Where(i => i.LibraryId == PluginId).ToList();
        if (legendaryGames.Count <= 0)
        {
            return menuItems;
        }
        
        var installedLegendaryGames =
            legendaryGames.Where(i => i.InstallState == InstallState.Installed).ToList();

        if (legendaryGames.Count == 1)
        {
            var game = legendaryGames.First();
            if (game.InstallState == InstallState.Installed)
            {
                menuItems.Add(new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCheckForUpdates),
                    async _ => { await LegendaryGameMenuActions.OpenCheckForGamesUpdatesWindow(game); },
                    icon: CommonIcons.UpdateIcon
                ));
            }
            else if (game.InstallState == InstallState.Uninstalled)
            {
                menuItems.Add(
                    new MenuItemImpl(
                        LocalizationManager.Instance.GetString(LOC.CommonImportInstalledGame),
                        async _ => { await LegendaryGameMenuActions.OpenImportGameWindow(game); },
                        icon: CommonIcons.ImportGameIcon)
                );
            }

            menuItems.Add(
                new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.CommonManageDlcs),
                    async _ => { await LegendaryGameMenuActions.OpenDlcManagerWindow(game); },
                    icon: CommonIcons.InstallIcon)
            );

            if (game.InstallState == InstallState.Installed)
            {
                menuItems.Add(new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.CommonMove),
                    async _ => { await LegendaryGameMenuActions.OpenMoveGameWindow(game); }
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
                    _ =>
                    {
                        LegendaryGameMenuActions.OpenInstallerWindow(notInstalledLegendaryGames);
                    }, icon: CommonIcons.InstallIcon
                ));
            }

            if (installedLegendaryGames.Count > 0)
            {
                menuItems.Add(new MenuItemImpl(
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                    async _ => { await LegendaryUninstallController.LaunchUninstaller(installedLegendaryGames); },
                    icon: CommonIcons.UninstallIcon
                ));
            }
        }

        if (installedLegendaryGames.Count > 0)
        {
            menuItems.Add(new MenuItemImpl(
                LocalizationManager.Instance.GetString(LOC.CommonRepair),
                _ =>
                {
                    LegendaryGameMenuActions.OpenRepairWindow(installedLegendaryGames);
                }, 
                icon: CommonIcons.RepairIcon
            ));
        }

        return menuItems;
    }

    public override ICollection<MenuItemDescriptor>? GetAppMenuItemDescriptors(GetAppMenuItemDescriptorsArgs args)
    {
        return
        [
            new MenuItemDescriptor($"appMenu.{PluginId}.Items", ShortPluginName),
        ];
    }

    public override ICollection<MenuItemImpl> GetAppMenuItems(GetAppMenuItemsArgs args)
    {
        var menuItems = new List<MenuItemImpl>();
        var childMenuItems = new List<MenuItemImpl>();
        if (args.ItemId == $"appMenu.{PluginId}.Items")
        {
            childMenuItems.Add(new MenuItemImpl(LocalizationManager.Instance.GetString(LOC.CommonCheckForGamesUpdatesButton),
                async _ =>
                {
                    if (!LegendaryLauncher.IsInstalled)
                    {
                        await LegendaryLauncher.ShowNotInstalledError();
                        return;
                    }

                    var gamesUpdates = new Dictionary<string, UpdateInfo>();
                    var legendaryUpdateController = new LegendaryUpdateController();
                    var updateCheckProgressOptions =
                        new GlobalProgressOptions(
                                LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates),
                                false)
                            { IsIndeterminate = true };
                    await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions,
                        async _ => { gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates(); }
                    );
                    
                    var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                    {
                        ShowMaximizeButton = false
                    });
                    window.DataContext = gamesUpdates;
                    window.Title =
                        $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                    window.Content = new LegendaryUpdater();
                    window.Owner = PlayniteApi.GetLastActiveWindow();
                    window.SizeToContent = SizeToContent.WidthAndHeight;
                    window.MinWidth = 600;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.ShowDialog();
                },
                icon: CommonIcons.UpdateIcon
            ));
            childMenuItems.Add(new MenuItemImpl(LocalizationManager.Instance.GetString(LOC.CommonFinishInstallation),
                async _ =>
                {
                    var installedAppList = LegendaryLauncher.GetInstalledAppList();
                    var gamesToCompleteInstall = new Dictionary<string, Installed>();

                    foreach (var game in installedAppList)
                    {
                        var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(game.Key);
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

                        await PlayniteApi.Dialogs.ShowBlockingProgressAsync(installProgressOptions, progress =>
                            {
                                progress.SetProgressMaxValue(gamesToCompleteInstall.Count);
                                var current = 0;
                                foreach (var game in gamesToCompleteInstall)
                                {
                                    progress.SetText(
                                        $"{LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation)} ({game.Value.Title})");
                                    LegendaryGames.CompleteGameInstallation(game.Key);
                                    current++;
                                    progress.SetCurrentProgressValue(current);
                                }
                            }
                        );
                    }
                    else
                    {
                        await PlayniteApi.Dialogs.ShowMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonNoFinishNeeded));
                    }
                }, icon: CommonIcons.FinishInstallationIcon));
            menuItems.Add(new MenuItemImpl(ShortPluginName, childMenuItems));
        }

        return menuItems;
    }

    public override async Task OpenClientAsync(OpenClientArgs args)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
        }
        LegendaryLauncher.StartClient();
    }

    public override async Task<GameEditSessionHandler?> GetGameEditHandlerAsync(GetGameEditHandlerArgs args)
    {
        if (args.Games.Count == 1 && args.Games[0].LibraryId == PluginId && LegendaryLauncher.IsInstalled)
        {
            return new LegendaryGameEditSessionHandler(args.Games[0]);
        }

        return null;
    }

    public override async Task<List<ImportableAchievements>> GetAchievementsAsync(GetAchievementsArgs args)
    {
        var achievementsList = new List<ImportableAchievements>();
        var clientApi = new EpicAccountClient(PlayniteApi);
        var tokens = clientApi.LoadTokens();
        if (!await clientApi.GetIsUserLoggedIn() || tokens == null)
        {
            throw new Exception("User is not authenticated.");
        }

        var legendaryGames = args.Games.Where(i => i.LibraryId == PluginId).ToList();
        if (legendaryGames.Count <= 0)
        {
            return achievementsList;
        }

        int gamesWithAchievements = 0;
        foreach (var game in legendaryGames)
        {
            if (args.CancelToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var importableAchievements = await clientApi.GetAchievements(game.LibraryGameId!, tokens, args.CancelToken);
                if (importableAchievements.Count > 0)
                {
                    gamesWithAchievements += 1;
                    Logger.Debug($"Found {importableAchievements.Count} achievements for {game.Name}.");
                }

                achievementsList.Add(new ImportableAchievements(game.Id, importableAchievements));
            }
            catch (Exception ex)
            {
                Logger.Debug(ex);
            }
        }

        if (gamesWithAchievements > 1)
        {
            Logger.Debug($"Found {gamesWithAchievements} games with achievements.");
        }

        return achievementsList;
    }

    public override async Task<CollectDiagnosticDataArgsAsyncResult?> CollectDiagnosticDataArgsAsync(CollectDiagnosticDataArgs args)
    {
        var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp",
            "Playnite", PluginId, "Logs");
        try
        {
            if (Directory.Exists(logsPath))
            {
                Directory.Delete(logsPath, true);
            }
        }
        catch (Exception)
        {
            // ignored
        }

        Directory.CreateDirectory(logsPath);
        var zipPath = Path.Combine(logsPath, $"{PluginId}.zip");
        try
        {
            Directory.CreateDirectory(logsPath);
            await File.WriteAllTextAsync(Path.Combine(logsPath, "Readme.txt"),
                $"To report a bug, please fill form at: \n" +
                $"<https://github.com/hawkeye116477/playnite-legendary-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&legendaryV={LegendaryTroubleshootingInformation.PluginVersion}&playniteV={LegendaryTroubleshootingInformation.PlayniteVersion}&launcherV={await LegendaryTroubleshootingInformation.GetLauncherVersion()}> \n" +
                $"and attach generated zip file.");

            var pluginLogFiles = Directory.GetFiles(PlayniteApi.UserDataDir, "plugin*.log", SearchOption.TopDirectoryOnly);
            var playniteLogFiles = Directory.GetFiles(PlayniteApi.AppInfo.ConfigurationDirectory, "playnite*.log",
                SearchOption.TopDirectoryOnly);
            var files = new List<string>();
            files.AddRange(pluginLogFiles);
            files.AddRange(playniteLogFiles);

            await using var zipArchive = await ZipFile.OpenAsync(zipPath, ZipArchiveMode.Update);
            foreach (var singleFile in files)
            {
                await using var source = new FileStream(singleFile, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                await source.CopyToAsync(await zipArchive.CreateEntry(Path.GetFileName(singleFile)).OpenAsync());
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex);
        }

        var newResults = new CollectDiagnosticDataArgsAsyncResult
        {
            ResultFile = zipPath
        };
        return newResults;
    }
}