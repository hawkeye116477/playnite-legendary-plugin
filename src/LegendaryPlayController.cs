using System.Diagnostics;
using System.IO;
using System.Text;
using CliWrap;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;
using Playnite.Common;

namespace LegendaryLibraryNS;

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

    private async Task OnGameStarting()
    {
        await LegendaryCloud.SyncGameSaves(game, CloudSyncAction.Download);
    }

    private async Task OnGameClosed(double sessionLength)
    {
        await LegendaryCloud.SyncGameSaves(game, CloudSyncAction.Upload);
        var playtimeSyncEnabled = false;
        var playtimeImportEnabled = false;
        if (playtimeImportEnabled)
        {
            playtimeSyncEnabled = LegendaryLibrary.GetSettings() is { SyncPlaytime: true };
            var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(game.LibraryGameId!);
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

    private async Task LaunchGame(bool offline = false)
    {
        await DisposeAsync();
        var playArgs = new List<string>();
        playArgs.AddRange(["launch", game.LibraryGameId!]);
        playArgs.Add("--skip-version-check");
        var globalSettings = LegendaryLibrary.GetSettings();
        var offlineModeEnabled = globalSettings is { LaunchOffline: true };
        var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(game.LibraryGameId!);

        if (gameSettings.InstallPrerequisites)
        {
            var installProgressOptions =
                new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation), false);
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(installProgressOptions,
                async a => { LegendaryGames.CompleteGameInstallation(game.LibraryGameId!); });
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
            foreach (var userArg in gameSettings.StartupArguments)
            {
                if (userArg.Contains('{'))
                {
                    playArgs.Add(playniteApi.ExpandVariables(game, userArg, false));
                }
                else
                {
                    playArgs.Add(userArg);
                }
            }
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
                     .WithEnvironmentVariables(LegendaryLauncher.GetDefaultEnvironmentVariables())
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
                                            monitor.IsProcessRunning);
                                        return;
                                    }

                                    await Task.Delay(5000);
                                }
                            }

                            StartTracking(() => monitor.IsProcessRunning() > 0,
                                monitor.IsProcessRunning);
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