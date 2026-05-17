using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite;
using Playnite.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;

namespace LegendaryLibraryNS;

public class LegendaryInstallController(Game game) : InstallController("legendary_install",
    "Install using Legendary client", game.LibraryGameId!)
{
    public override async Task InstallAsync(InstallActionArgs args)
    {
        var installProperties = new DownloadProperties { DownloadAction = DownloadAction.Install };
        var installData = new List<DownloadManagerData.Download>
        {
            new() { GameId = game.LibraryGameId!, Name = game.Name, DownloadProperties = installProperties }
        };

        LaunchInstaller(installData);
        await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
    }

    public static void LaunchInstaller(List<DownloadManagerData.Download> installData)
    {
        var playniteApi = LegendaryLibrary.PlayniteApi;
        var window = playniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = installData;
        window.Content = new LegendaryGameInstaller();
        window.Owner = playniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var title = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame);
        if (installData.Count == 1)
        {
            title = installData[0].Name;
        }

        window.Title = title;
        window.ShowDialog();
    }
}

public class LegendaryUninstallController(Game game) : UninstallController("legendary_uninstall",
    "Uninstall using Legendary client", game.LibraryGameId!)
{
    private static readonly ILogger Logger = LogManager.GetLogger();

    public override async Task UninstallAsync(UninstallActionArgs args)
    {
        var games = new List<Game>
        {
            game
        };
        await LaunchUninstaller(games);
        await GameUninstallationCancelledAsync(new GameUninstallCancelledArgs());
    }

    public static async Task LaunchUninstaller(List<Game> games)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var playniteApi = LegendaryLibrary.PlayniteApi;
        var gamesCombined = string.Join(", ", games.Select(item => item.Name));

        var responses = new List<MessageBoxResponse>
        {
            new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteYesLabel)),
            new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteNoLabel))
        };

        var removeGameLaunchSettingsCheckbox =
            new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonRemoveGameLaunchSettings), false);

        var result = await playniteApi.Dialogs.ShowMessageAsync(
            LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm,
                new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)gamesCombined }),
            LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
            MessageBoxSeverity.Question, responses, [removeGameLaunchSettingsCheckbox]);
        if (result?.Title == LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteYesLabel))
        {
            var canContinue = await LegendaryLibrary.Instance.StopDownloadManager(true);
            if (!canContinue)
            {
                return;
            }

            var uninstalledGames = new List<Game>();
            var notUninstalledGames = new List<Game>();
            var globalProgressOptions =
                new GlobalProgressOptions($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstalling)}... ", false);
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async (a) =>
            {
                a.SetProgressMaxValue(games.Count);

                var counter = 0;
                foreach (var game in games)
                {
                    a.SetText($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstalling)} {game.Name}... ");
                    await LegendaryDownloadLogic.WaitUntilLegendaryCloses();
                    var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                       .WithArguments(["-y", "uninstall", game.LibraryGameId!])
                                       .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                                       .AddCommandToLog()
                                       .WithValidation(CommandResultValidation.None)
                                       .ExecuteBufferedAsync();
                    if (cmd.StandardError.Contains("has been uninstalled"))
                    {
                        if (removeGameLaunchSettingsCheckbox.IsSelected)
                        {
                            var gameSettingsFile = Path.Combine(Path.Combine(playniteApi.UserDataDir, "GamesSettings",
                                $"{game.LibraryGameId}.json"));
                            if (File.Exists(gameSettingsFile))
                            {
                                File.Delete(gameSettingsFile);
                            }
                        }

                        try
                        {
                            if (Directory.Exists(game.InstallDirectory))
                            {
                                Directory.Delete(game.InstallDirectory, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug(ex.Message);
                        }

                        game.InstallState = InstallState.Uninstalled;
                        game.InstallDirectory = "";
                        //game.Version = "";
                        await playniteApi.Library.Games.UpdateAsync(game);
                        uninstalledGames.Add(game);
                    }
                    else
                    {
                        notUninstalledGames.Add(game);
                        Logger.Debug("[Legendary] " + cmd.StandardError);
                        Logger.Error("[Legendary] exit code: " + cmd.ExitCode);
                    }

                    counter += 1;
                    a.SetCurrentProgressValue(counter);
                }
            });
            if (uninstalledGames.Count > 0)
            {
                var uninstalledGamesList = uninstalledGames[0].Name;
                if (uninstalledGames.Count > 1)
                {
                    uninstalledGamesList = string.Join(", ", uninstalledGames.Select(item => item.Name));
                }

                await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonUninstallSuccess,
                    new Dictionary<string, IFluentType>
                        { ["appName"] = (FluentString)uninstalledGamesList, ["count"] = (FluentNumber)uninstalledGames.Count }));
            }

            if (notUninstalledGames.Count > 0)
            {
                if (notUninstalledGames.Count == 1)
                {
                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameUninstallError,
                            new Dictionary<string, IFluentType>
                                { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }),
                        notUninstalledGames[0].Name);
                }
                else
                {
                    var notUninstalledGamesCombined = string.Join(", ", notUninstalledGames.Select(item => item.Name));
                    await playniteApi.Dialogs.ShowMessageAsync(
                        $"{LocalizationManager.Instance.GetString(LOC.CommonUninstallError, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)notUninstalledGamesCombined, ["count"] = (FluentNumber)notUninstalledGames.Count })} {LocalizationManager.Instance.GetString(LOC.CommonCheckLog)}");
                }
            }
        }
    }
}

public class LegendaryPlayController(Game game) : PlayController(game.LibraryGameId!,
    LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicStartUsingClient,
        new Dictionary<string, IFluentType> { ["var0"] = (FluentString)"Legendary" }))
{
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private static ILogger logger = LogManager.GetLogger();
    private CancellationTokenSource? watcherToken;
    private CancellationTokenSource? ubisoftWatcherToken;

    public override async ValueTask DisposeAsync()
    {
        if (watcherToken != null)
        {
            await watcherToken.CancelAsync();
            watcherToken?.Dispose();
            watcherToken = null;
        }

        if (ubisoftWatcherToken != null)
        {
            await ubisoftWatcherToken.CancelAsync();
            ubisoftWatcherToken?.Dispose();
            ubisoftWatcherToken = null;
        }
    }

    public override async Task PlayAsync(PlayActionArgs args)
    {
        await DisposeAsync();
        if (Directory.Exists(game.InstallDirectory) && LegendaryLauncher.IsInstalled)
        {
            await OnGameStarting();
            await LaunchGame();
        }
        else
        {
            await GameStoppedAsync(null!);
            if (!LegendaryLauncher.IsInstalled)
            {
                await LegendaryLauncher.ShowNotInstalledError();
            }
        }
    }

    public async Task OnGameStarting()
    {
        await LegendaryCloud.SyncGameSaves(game, CloudSyncAction.Download);
    }

    public async Task OnGameClosed(double sessionLength)
    {
        await LegendaryCloud.SyncGameSaves(game, CloudSyncAction.Upload);
        var playtimeSyncEnabled = false;
        var playtimeImportEnabled = false;
        if (playtimeImportEnabled)
        {
            playtimeSyncEnabled = LegendaryLibrary.GetSettings() is { SyncPlaytime: true };
            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.LibraryGameId!);
            if (gameSettings.AutoSyncPlaytime != null)
            {
                playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
            }
        }

        if (playtimeSyncEnabled)
        {
            var now = DateTime.UtcNow;
            var totalSeconds = sessionLength;
            var startTime = now.AddSeconds(-totalSeconds);
            var legendaryCloud = new LegendaryCloud();
            await legendaryCloud.UploadPlaytime(startTime, now, game);
        }
    }

    public async Task LaunchGame(bool offline = false)
    {
        await DisposeAsync();
        var playArgs = new List<string>();
        playArgs.AddRange(["launch", game.LibraryGameId!]);
        playArgs.Add("--skip-version-check");
        var globalSettings = LegendaryLibrary.GetSettings();
        var offlineModeEnabled = globalSettings is { LaunchOffline: true };
        var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.LibraryGameId!);

        if (gameSettings.InstallPrerequisites)
        {
            var installProgressOptions =
                new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation), false);
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(installProgressOptions,
                async (a) => { LegendaryLauncher.CompleteGameInstallation(game.LibraryGameId!); });
        }

        if (gameSettings.LaunchOffline != null)
        {
            offlineModeEnabled = (bool)gameSettings.LaunchOffline;
        }

        var canRunOffline = false;
        if (offlineModeEnabled)
        {
            var appList = LegendaryLauncher.GetInstalledAppList();
            if (appList.ContainsKey(game.LibraryGameId!))
            {
                if (appList[game.LibraryGameId!].Can_run_offline)
                {
                    canRunOffline = true;
                }
            }
        }

        if (canRunOffline || offline)
        {
            playArgs.Add("--offline");
        }

        if (gameSettings.StartupArguments is { Count: > 0 })
        {
            playArgs.AddRange(gameSettings.StartupArguments);
        }

        if (!string.IsNullOrEmpty(gameSettings.LanguageCode))
        {
            playArgs.AddRange(["--language", gameSettings.LanguageCode]);
        }

        if (!string.IsNullOrEmpty(gameSettings.OverrideExe))
        {
            playArgs.AddRange(["--override-exe", gameSettings.OverrideExe]);
        }

        StringBuilder stdOutBuffer = new();
        var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                     .WithArguments(playArgs)
                     .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                     .AddCommandToLog()
                     .WithValidation(CommandResultValidation.None);
        await foreach (var cmdEvent in cmd.ListenAsync())
        {
            switch (cmdEvent)
            {
                case StartedCommandEvent started:
                    if (game.InstallDirectory != null)
                    {
                        var monitor = new MonitorDirectory(game.InstallDirectory);
                        if (monitor.IsTrackable())
                        {
                            if (File.Exists(Path.Combine(game.InstallDirectory, "UplayLaunch.exe")))
                            {
                                // Borrowed from https://github.com/JosefNemec/PlayniteExtensions/blob/d3b1b50f45aa174751852198172a28a5ae947c6d/source/Libraries/UplayLibrary/UplayGameController.cs#L146
                                logger.Debug($"{game.Name} requires Ubisoft launcher to run, waiting for it to start properly.");
                                // Solves issues with game process being started/shutdown multiple times during startup via Ubisoft Connect
                                ubisoftWatcherToken = new CancellationTokenSource();
                                while (true)
                                {
                                    if (ubisoftWatcherToken.IsCancellationRequested)
                                    {
                                        return;
                                    }

                                    if (ProcessExtensions.IsRunning("UbisoftGameLauncher"))
                                    {
                                        StartTracking(() => monitor.IsProcessRunning() > 0,
                                            () => monitor.IsProcessRunning());
                                        return;
                                    }

                                    await Task.Delay(5000);
                                }
                            }
                            else
                            {
                                StartTracking(() => monitor.IsProcessRunning() > 0,
                                    () => monitor.IsProcessRunning());
                            }
                        }
                    }

                    break;
                case StandardErrorCommandEvent stdErr:
                    stdOutBuffer.AppendLine(stdErr.Text);
                    break;
                case ExitedCommandEvent exited:
                    if (exited.ExitCode != 0)
                    {
                        var errorMessage = stdOutBuffer.ToString();
                        logger.Debug("[Legendary] " + errorMessage);
                        logger.Error("[Legendary] exit code: " + exited.ExitCode);
                        if (errorMessage.Contains("Failed to establish a new connection")
                            || errorMessage.Contains("Log in failed")
                            || errorMessage.Contains("Login failed")
                            || errorMessage.Contains("No saved credentials"))
                        {
                            var appList = LegendaryLauncher.GetInstalledAppList();
                            if (appList.ContainsKey(game.LibraryGameId!))
                            {
                                if (appList[game.LibraryGameId!].Can_run_offline)
                                {
                                    var tryOfflineResponse =
                                        new MessageBoxResponse(LocalizationManager.Instance.GetString(LOC.LegendaryEnableOfflineMode));
                                    var okResponse =
                                        new MessageBoxResponse(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel),
                                            true, true);
                                    var message = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameStartError,
                                        new Dictionary<string, IFluentType>
                                        {
                                            ["var0"] = (FluentString)LocalizationManager.Instance.GetString(
                                                LOC.ThirdPartyPlayniteLoginRequired)
                                        });
                                    var offlineConfirm = await playniteApi.Dialogs.ShowMessageAsync(message, "",
                                        MessageBoxSeverity.Error, [tryOfflineResponse, okResponse], []);
                                    if (offlineConfirm == tryOfflineResponse)
                                    {
                                        if (watcherToken != null)
                                        {
                                            await watcherToken.CancelAsync();
                                        }

                                        await LaunchGame(true);
                                        return;
                                    }
                                }
                                else
                                {
                                    await GameStoppedAsync(new GameStoppedArgs(0));
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameStartError,
                                            new Dictionary<string, IFluentType>
                                            {
                                                ["var0"] = (FluentString)LocalizationManager.Instance.GetString(
                                                    LOC.ThirdPartyPlayniteLoginRequired)
                                            }));
                                }
                            }
                        }
                        else
                        {
                            await GameStoppedAsync(new GameStoppedArgs(0));
                            await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(
                                LOC.ThirdPartyPlayniteGameStartError,
                                new Dictionary<string, IFluentType>
                                    { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                        }
                    }
                    else
                    {
                        stdOutBuffer = new StringBuilder();
                    }

                    break;
            }
        }
    }

    private void StartTracking(
        Func<bool> trackingAction,
        Func<int>? startupCheck = null,
        int trackingFrequency = 2000,
        int trackingStartDelay = 0)
    {
        if (watcherToken != null)
        {
            throw new Exception("Game is already being tracked.");
        }

        watcherToken = new CancellationTokenSource();
        Task.Run(async () =>
        {
            ulong playTimeMs = 0;
            var trackingWatch = new Stopwatch();
            const int maxFailCount = 5;
            var failCount = 0;

            if (trackingStartDelay > 0)
            {
                await Task.Delay(trackingStartDelay, watcherToken.Token).ContinueWith(task => { });
            }

            if (startupCheck != null)
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (failCount >= maxFailCount)
                    {
                        await GameStoppedAsync(new GameStoppedArgs(0));
                        return;
                    }

                    try
                    {
                        var id = startupCheck();
                        if (id > 0)
                        {
                            await GameStartedAsync(new GameStartedArgs(id));
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        failCount++;
                        logger.Error(e, "Game startup tracking iteration failed.");
                    }

                    await Task.Delay(trackingFrequency, watcherToken.Token).ContinueWith(task => { });
                }
            }

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                if (failCount >= maxFailCount)
                {
                    var playTimeS = playTimeMs / 1000;
                    await GameStoppedAsync(new GameStoppedArgs((uint)playTimeS));
                    await OnGameClosed(playTimeS);
                    return;
                }

                try
                {
                    trackingWatch.Restart();
                    if (!trackingAction())
                    {
                        var playTimeS = playTimeMs / 1000;
                        await GameStoppedAsync(new GameStoppedArgs((uint)playTimeS));
                        await OnGameClosed(playTimeS);
                        return;
                    }
                }
                catch (Exception e)
                {
                    failCount++;
                    logger.Error(e, "Game tracking iteration failed.");
                }

                await Task.Delay(trackingFrequency, watcherToken.Token).ContinueWith(task => { });
                trackingWatch.Stop();
                if (trackingWatch.ElapsedMilliseconds > trackingFrequency + 30_000)
                {
                    // This is for cases where system is put into sleep or hibernation.
                    // Realistically speaking, one tracking interation should never take 30+ seconds,
                    // but lets use that as safe value in case this runs super slowly on some weird PCs.
                    continue;
                }

                playTimeMs += (ulong)trackingWatch.ElapsedMilliseconds;
            }
        });
    }
}

public class LegendaryUpdateController
{
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private static ILogger logger = LogManager.GetLogger();

    public async Task<Dictionary<string, UpdateInfo>> CheckGameUpdates(string gameTitle, string gameId, bool forceRefreshCache = false)
    {
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();

        if (gameId == "eos-overlay")
        {
            var cacheVersionFile = Path.Combine(LegendaryLauncher.ConfigPath, "overlay_version.json");
            var newVersion = "";
            if (File.Exists(cacheVersionFile))
            {
                if (File.GetLastWriteTime(cacheVersionFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheVersionFile);
                }
            }

            var correctJson = false;
            var overlayVersionInfo = new OverlayVersion.Rootobject();
            if (File.Exists(cacheVersionFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
                if (!content.IsNullOrWhiteSpace() &&
                    Serialization.TryFromJson(content, out OverlayVersion.Rootobject? newOverlayVersionInfo))
                {
                    if (newOverlayVersionInfo is { Data: not null })
                    {
                        overlayVersionInfo = newOverlayVersionInfo;
                        correctJson = true;
                    }
                }
            }

            if (!correctJson)
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(["status", "--json"])
                                   .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                                   .AddCommandToLog()
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                var errorMessage = cmd.StandardError;
                if (cmd.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") ||
                    errorMessage.Contains("Error"))
                {
                    logger.Error("[Legendary]" + cmd.StandardError);
                }
                else
                {
                    var content = cmd.StandardOutput;
                    if (!content.IsNullOrWhiteSpace() &&
                        Serialization.TryFromJson(content, out OverlayVersion.Rootobject? newOverlayVersionInfo))
                    {
                        if (newOverlayVersionInfo is { Data.BuildVersion: not null })
                        {
                            overlayVersionInfo = newOverlayVersionInfo;
                            correctJson = true;
                        }
                    }
                }
            }

            if (correctJson)
            {
                newVersion = overlayVersionInfo.Data?.BuildVersion;
                if (newVersion == null)
                {
                    return gamesToUpdate;
                }

                var overlayInstallFile = Path.Combine(LegendaryLauncher.ConfigPath, "overlay_install.json");
                var overlayInstallContent = FileSystem.ReadFileAsStringSafe(overlayInstallFile);
                if (!overlayInstallContent.IsNullOrWhiteSpace() &&
                    Serialization.TryFromJson(overlayInstallContent, out Installed? overlayInstallInfo))
                {
                    if (overlayInstallInfo is { Version: not null })
                    {
                        if (overlayInstallInfo.Version != newVersion)
                        {
                            var result = await LegendaryLauncher.GetUpdateSizes("eos-overlay");
                            if (result.Download_size != 0)
                            {
                                var updateInfo = new UpdateInfo
                                {
                                    Version = newVersion,
                                    Title = gameTitle,
                                    Download_size = result.Download_size,
                                    Disk_size = result.Disk_size,
                                    Install_path = overlayInstallInfo.Install_path
                                };
                                gamesToUpdate.Add(gameId, updateInfo);
                            }
                        }
                    }
                }
            }
            else
            {
                logger.Error($"An error occured during checking {gameTitle} updates.");
            }

            return gamesToUpdate;
        }

        var newGameData = new LegendaryGameInfo.Game
        {
            Title = gameTitle,
            App_name = gameId
        };
        var newGameInfo = await LegendaryLauncher.GetGameInfo(newGameData, false, true, forceRefreshCache);
        if (newGameInfo.Game != null)
        {
            var installedAppList = LegendaryLauncher.GetInstalledAppList();
            if (installedAppList.ContainsKey(gameId))
            {
                var oldGameInfo = installedAppList[gameId];
                if (oldGameInfo.Version != newGameInfo.Game.Version)
                {
                    var resultUpdateSizes = await LegendaryLauncher.GetUpdateSizes(gameId);
                    if (resultUpdateSizes.Download_size != 0)
                    {
                        var updateInfo = new UpdateInfo
                        {
                            Version = newGameInfo.Game.Version,
                            Title = newGameInfo.Game.Title,
                            Download_size = resultUpdateSizes.Download_size,
                            Disk_size = resultUpdateSizes.Disk_size,
                            Install_path = oldGameInfo.Install_path
                        };
                        gamesToUpdate.Add(oldGameInfo.App_name, updateInfo);
                    }
                }

                // We need to also check for DLCs updates (see https://github.com/derrod/legendary/issues/506)
                if (newGameInfo.Game.Owned_dlc.Count > 0)
                {
                    foreach (var dlc in newGameInfo.Game.Owned_dlc)
                    {
                        if (!dlc.App_name.IsNullOrEmpty())
                        {
                            if (installedAppList.ContainsKey(dlc.App_name))
                            {
                                var oldDlcInfo = installedAppList[dlc.App_name];
                                var dlcData = new LegendaryGameInfo.Game
                                {
                                    Title = dlc.Title.RemoveTrademarks(),
                                    App_name = dlc.App_name
                                };
                                var newDlcInfo = await LegendaryLauncher.GetGameInfo(dlcData, false, true, forceRefreshCache);
                                if (newDlcInfo.Game != null)
                                {
                                    if (oldDlcInfo.Version != newDlcInfo.Game.Version)
                                    {
                                        var resultDlcUpdateSizes = await LegendaryLauncher.GetUpdateSizes(gameId);
                                        if (resultDlcUpdateSizes.Download_size != 0)
                                        {
                                            var updateDlcInfo = new UpdateInfo
                                            {
                                                Version = newDlcInfo.Game.Version,
                                                Title = newDlcInfo.Game.Title,
                                                Download_size = resultDlcUpdateSizes.Download_size,
                                                Disk_size = resultDlcUpdateSizes.Disk_size
                                            };
                                            gamesToUpdate.Add(oldDlcInfo.App_name, updateDlcInfo);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            logger.Error($"An error occured during checking {gameTitle} updates.");
            var updateInfo = new UpdateInfo
            {
                Version = "0",
                Title = gameTitle,
                Download_size = 0,
                Disk_size = 0,
                Success = false
            };
            gamesToUpdate.Add(gameId, updateInfo);
        }

        return gamesToUpdate;
    }

    public async Task<Dictionary<string, UpdateInfo>> CheckAllGamesUpdates()
    {
        var appList = LegendaryLauncher.GetInstalledAppList();
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();
        foreach (var game in appList.Where(item => item.Value.Is_dlc == false).OrderBy(item => item.Value.Title))
        {
            var gameId = game.Value.App_name;
            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(gameId);
            var canUpdate = gameSettings.DisableGameVersionCheck != true;
            if (canUpdate)
            {
                LegendaryUpdateController legendaryUpdateController = new();
                var gameToUpdate = await legendaryUpdateController.CheckGameUpdates(game.Value.Title, gameId);
                if (gameToUpdate.Count > 0)
                {
                    foreach (var singleGame in gameToUpdate)
                    {
                        gamesToUpdate.Add(singleGame.Key, singleGame.Value);
                    }
                }
            }
        }

        if (LegendaryLauncher.IsEosOverlayInstalled)
        {
            var legendaryUpdateController = new LegendaryUpdateController();
            var overlayToUpdate = await legendaryUpdateController.CheckGameUpdates(
                LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                    new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" }), "eos-overlay");
            if (overlayToUpdate.Count > 0)
            {
                gamesToUpdate.Add("eos-overlay", overlayToUpdate["eos-overlay"]);
            }
        }

        return gamesToUpdate;
    }

    public async Task UpdateGame(
        Dictionary<string, UpdateInfo> gamesToUpdate, string gameTitle = "", bool silently = false,
        DownloadProperties? downloadProperties = null)
    {
        var unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(playniteApi);
        var updateTasks = new List<DownloadManagerData.Download>();
        if (gamesToUpdate.Count > 0)
        {
            var canUpdate = true;
            if (canUpdate)
            {
                if (silently)
                {
                    playniteApi.Notifications.Add(new NotificationMessage("LegendaryGamesUpdates",
                        LocalizationManager.Instance.GetString(LOC.CommonGamesUpdatesUnderway), NotificationSeverity.Info));
                }

                foreach (var gameToUpdate in gamesToUpdate)
                {
                    var wantedUnifiedItem = unifiedDownloadManagerApi.GetTask(gameToUpdate.Key, LegendaryLibrary.PluginId);

                    var settings = LegendaryLibrary.GetSettings();
                    var newDownloadProperties = new DownloadProperties
                    {
                        DownloadAction = DownloadAction.Update,
                        EnableReordering = settings is { EnableReordering: true },
                        MaxWorkers = settings!.MaxWorkers,
                        MaxSharedMemory = settings.MaxSharedMemory
                    };
                    if (downloadProperties != null)
                    {
                        newDownloadProperties = downloadProperties.GetClone();
                    }

                    newDownloadProperties.InstallPath = gameToUpdate.Value.Install_path;

                    var updateTask = new DownloadManagerData.Download
                    {
                        GameId = gameToUpdate.Key,
                        Name = gameToUpdate.Value.Title,
                        DownloadSizeNumber = gameToUpdate.Value.Download_size,
                        InstallSizeNumber = gameToUpdate.Value.Disk_size,
                        DownloadProperties = newDownloadProperties
                    };
                    updateTask.DownloadProperties.InstallPath = Directory.GetParent(gameToUpdate.Value.Install_path)?.FullName!;
                    updateTask.FullInstallPath = gameToUpdate.Value.Install_path;
                    updateTasks.Add(updateTask);
                }

                if (updateTasks.Count > 0)
                {
                    var downloadLogic = (LegendaryDownloadLogic)LegendaryLibrary.Instance.UnifiedDownloadLogic;
                    await downloadLogic.AddTasks(updateTasks, silently);
                }
            }
        }
        else if (!silently)
        {
            await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), gameTitle);
        }
    }
}