﻿using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryLibraryNS
{
    public class LegendaryInstallController : InstallController
    {
        private IPlayniteAPI playniteAPI = API.Instance;

        public LegendaryInstallController(Game game) : base(game)
        {
            Name = "Install using Legendary client";
        }

        public override void Install(InstallActionArgs args)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
            }

            Window window = null;
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMaximizeButton = false,
                });
            }
            else
            {
                window = new Window
                {
                    Background = System.Windows.Media.Brushes.DodgerBlue
                };
            }
            window.Title = Game.Name;
            var installProperties = new DownloadProperties { downloadAction = DownloadAction.Install };
            var installData = new DownloadManagerData.Download { gameID = Game.GameId, name = Game.Name, downloadProperties = installProperties };
            window.DataContext = installData;
            window.Content = new LegendaryGameInstaller();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.MinWidth = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            if (result == false)
            {
                Game.IsInstalling = false;
            }
        }
    }

    public class LegendaryUninstallController : UninstallController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static readonly ILogger logger = LogManager.GetLogger();

        public LegendaryUninstallController(Game game) : base(game)
        {
            Name = "Uninstall";
        }

        public override async void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
            }
            var result = MessageCheckBoxDialog.ShowMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUninstallGame), ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm).Format(Game.Name), LOC.LegendaryRemoveGameLaunchSettings, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result.Result == false)
            {
                Game.IsUninstalling = false;
            }
            else
            {
                var canContinue = LegendaryLibrary.Instance.StopDownloadManager(true);
                if (!canContinue)
                {
                    Game.IsUninstalling = false;
                    return;
                }
                await LegendaryDownloadManager.WaitUntilLegendaryCloses();
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(new[] { "-y", "uninstall", Game.GameId })
                                   .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                   .AddCommandToLog()
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                if (cmd.StandardError.Contains("has been uninstalled"))
                {
                    if (result.CheckboxChecked)
                    {
                        var gameSettingsFile = Path.Combine(Path.Combine(LegendaryLibrary.Instance.GetPluginUserDataPath(), "GamesSettings", $"{Game.GameId}.json"));
                        if (File.Exists(gameSettingsFile))
                        {
                            File.Delete(gameSettingsFile);
                        }
                    }
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryUninstallSuccess).Format(Game.Name));
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                }
                else
                {
                    logger.Debug("[Legendary] " + cmd.StandardError);
                    logger.Error("[Legendary] exit code: " + cmd.ExitCode);
                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameUninstallError).Format(ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                    Game.IsUninstalling = false;
                }
            }
        }

        public Game GetGame()
        {
            return Game;
        }
    }

    public class LegendaryPlayController : PlayController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static ILogger logger = LogManager.GetLogger();
        private CancellationTokenSource watcherToken;
        private CancellationTokenSource ubisoftWatcherToken;

        public LegendaryPlayController(Game game) : base(game)
        {
            Name = string.Format(ResourceProvider.GetString(LOC.Legendary3P_EpicStartUsingClient), "Legendary");
        }

        public override void Dispose()
        {
            watcherToken?.Dispose();
            watcherToken = null;
            ubisoftWatcherToken?.Dispose();
        }

        public override async void Play(PlayActionArgs args)
        {
            Dispose();
            if (Directory.Exists(Game.InstallDirectory))
            {
                OnGameStarting();
                await LaunchGame();
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        public void OnGameStarting()
        {
            LegendaryCloud.SyncGameSaves(Game.Name, Game.GameId, Game.InstallDirectory, CloudSyncAction.Download);
        }

        public void OnGameClosed(double sessionLength)
        {
            LegendaryCloud.SyncGameSaves(Game.Name, Game.GameId, Game.InstallDirectory, CloudSyncAction.Upload);
            var playtimeSyncEnabled = false;
            if (playniteAPI.ApplicationSettings.PlaytimeImportMode != PlaytimeImportMode.Never)
            {
                playtimeSyncEnabled = LegendaryLibrary.GetSettings().SyncPlaytime;
                var gameSettings = LegendaryGameSettingsView.LoadGameSettings(Game.GameId);
                if (gameSettings?.AutoSyncPlaytime != null)
                {
                    playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                }
            }
            if (playtimeSyncEnabled)
            {
                using (var httpClient = new HttpClient())
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendaryUploadingPlaytime).Format(Game.Name), false);
                    playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
                    {
                        a.ProgressMaxValue = 100;
                        a.CurrentProgressValue = 0;
                        httpClient.DefaultRequestHeaders.Clear();
                        if (File.Exists(LegendaryLauncher.TokensPath))
                        {
                            var tokensContent = FileSystem.ReadFileAsStringSafe(LegendaryLauncher.TokensPath);
                            if (!tokensContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(tokensContent, out OauthResponse userData))
                            {
                                httpClient.DefaultRequestHeaders.Add("Authorization", userData.token_type + " " + userData.access_token);
                                var uri = $"https://library-service.live.use1a.on.epicgames.com/library/api/public/playtime/account/{userData.account_id}";
                                PlaytimePayload playtimePayload = new PlaytimePayload
                                {
                                    artifactId = Game.GameId,
                                    machineId = LegendaryLibrary.GetSettings().SyncPlaytimeMachineId
                                };
                                DateTime now = DateTime.UtcNow;
                                playtimePayload.endTime = now;
                                var totalSeconds = sessionLength;
                                var startTime = now.AddSeconds(-(double)totalSeconds);
                                playtimePayload.startTime = now.AddSeconds(-(double)totalSeconds);
                                var playtimeJson = Serialization.ToJson(playtimePayload);
                                var content = new StringContent(playtimeJson, Encoding.UTF8, "application/json");
                                a.CurrentProgressValue = 1;
                                var result = await httpClient.PutAsync(uri, content);
                                if (!result.IsSuccessStatusCode)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.LegendarySyncError).Format(Game.Name));
                                    logger.Error($"An error occured during uploading playtime to the cloud. Status code: {result.StatusCode}.");
                                }
                            }
                            else
                            {
                                logger.Error("An error occured during reading tokens file.");
                                playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.LegendarySyncError).Format(Game.Name));
                            }
                        }
                        else
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.LegendarySyncError).Format(Game.Name));
                        }
                        a.CurrentProgressValue = 100;
                    }, globalProgressOptions);
                }
            }
        }

        public async Task LaunchGame(bool offline = false)
        {
            Dispose();
            var playArgs = new List<string>();
            playArgs.AddRange(new[] { "launch", Game.GameId });
            var globalSettings = LegendaryLibrary.GetSettings();
            var offlineModeEnabled = globalSettings.LaunchOffline;
            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(Game.GameId);

            if (gameSettings.InstallPrerequisites)
            {
                gameSettings.InstallPrerequisites = false;
                Helpers.SaveJsonSettingsToFile(gameSettings, Game.GameId, "GamesSettings");
                var appList = LegendaryLauncher.GetInstalledAppList();
                if (appList.ContainsKey(Game.GameId))
                {
                    var installedGameInfo = appList[Game.GameId];
                    if (installedGameInfo.Prereq_info != null)
                    {
                        var prereq = installedGameInfo.Prereq_info;
                        var prereqName = "";
                        if (!prereq.name.IsNullOrEmpty())
                        {
                            prereqName = prereq.name;
                        }
                        var prereqPath = "";
                        if (!prereq.path.IsNullOrEmpty())
                        {
                            prereqPath = prereq.path;
                        }
                        var prereqArgs = "";
                        if (!prereq.args.IsNullOrEmpty())
                        {
                            prereqArgs = prereq.args;
                        }
                        if (prereqPath != "")
                        {
                            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendaryInstallingPrerequisites).Format(prereqName), false);
                            playniteAPI.Dialogs.ActivateGlobalProgress((a) =>
                            {
                                try
                                {
                                    ProcessStarter.StartProcessWait(Path.GetFullPath(Path.Combine(installedGameInfo.Install_path, prereqPath)),
                                                                    prereqArgs,
                                                                    "");
                                }
                                catch (Exception ex)
                                {
                                    logger.Error($"Failed to launch prerequisites executable. Error: {ex.Message}");
                                }
                            }, globalProgressOptions);
                        }
                    }
                }
            }

            if (gameSettings?.LaunchOffline != null)
            {
                offlineModeEnabled = (bool)gameSettings.LaunchOffline;
            }

            bool canRunOffline = false;
            if (offlineModeEnabled)
            {
                var appList = LegendaryLauncher.GetInstalledAppList();
                if (appList.ContainsKey(Game.GameId))
                {
                    if (appList[Game.GameId].Can_run_offline)
                    {
                        canRunOffline = true;
                    }
                }
            }

            if (canRunOffline || offline)
            {
                playArgs.Add("--offline");
            }
            else
            {
                bool updateCheckDisabled = false;
                if (globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
                {
                    updateCheckDisabled = true;
                }
                if (gameSettings?.DisableGameVersionCheck != null)
                {
                    updateCheckDisabled = (bool)gameSettings.DisableGameVersionCheck;
                }
                if (updateCheckDisabled)
                {
                    playArgs.Add("--skip-version-check");
                }
            }
            if (gameSettings.StartupArguments?.Any() == true)
            {
                playArgs.AddRange(gameSettings.StartupArguments);
            }
            if (!gameSettings.LanguageCode.IsNullOrEmpty())
            {
                playArgs.AddRange(new[] { "--language", gameSettings.LanguageCode });
            }
            if (!gameSettings.OverrideExe.IsNullOrEmpty())
            {
                playArgs.AddRange(new[] { "--override-exe", gameSettings?.OverrideExe });
            }
            var stdOutBuffer = new StringBuilder();
            var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                         .WithArguments(playArgs)
                         .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                         .AddCommandToLog()
                         .WithValidation(CommandResultValidation.None);
            await foreach (var cmdEvent in cmd.ListenAsync())
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        var monitor = new MonitorDirectory(Game.InstallDirectory);
                        if (monitor.IsTrackable())
                        {
                            if (File.Exists(Path.Combine(Game.InstallDirectory, "UplayLaunch.exe")))
                            {
                                // Borrowed from https://github.com/JosefNemec/PlayniteExtensions/blob/d3b1b50f45aa174751852198172a28a5ae947c6d/source/Libraries/UplayLibrary/UplayGameController.cs#L146
                                logger.Debug($"{Game.Name} requires Ubisoft launcher to run, waiting for it to start properly.");
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
                                                      startupCheck: () => monitor.IsProcessRunning());
                                        return;
                                    }
                                    await Task.Delay(5000);
                                }
                            }
                            else
                            {
                                StartTracking(() => monitor.IsProcessRunning() > 0,
                                              startupCheck: () => monitor.IsProcessRunning());
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
                                if (appList.ContainsKey(Game.GameId))
                                {
                                    if (appList[Game.GameId].Can_run_offline)
                                    {
                                        var tryOfflineResponse = new MessageBoxOption(LOC.LegendaryEnableOfflineMode);
                                        var okResponse = new MessageBoxOption(LOC.Legendary3P_PlayniteOKLabel, true, true);
                                        var offlineConfirm = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)), "", MessageBoxImage.Error,
                                            new List<MessageBoxOption> { tryOfflineResponse, okResponse });
                                        if (offlineConfirm == tryOfflineResponse)
                                        {
                                            watcherToken.Cancel();
                                            await LaunchGame(true);
                                            return;
                                        }
                                        else
                                        {
                                            InvokeOnStopped(new GameStoppedEventArgs());
                                        }
                                    }
                                    else
                                    {
                                        InvokeOnStopped(new GameStoppedEventArgs());
                                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                                    }
                                }
                            }
                            else if (errorMessage.Contains("Game is out of date"))
                            {
                                InvokeOnStopped(new GameStoppedEventArgs());
                                LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                                var gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(Game.Name, Game.GameId);
                                Window window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                                {
                                    ShowMaximizeButton = false,
                                });
                                window.DataContext = gamesToUpdate;
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
                                InvokeOnStopped(new GameStoppedEventArgs());
                                playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                            }
                        }
                        else
                        {
                            stdOutBuffer = null;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public void StartTracking(Func<bool> trackingAction,
                                  Func<int> startupCheck = null,
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
                var maxFailCount = 5;
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
                            InvokeOnStopped(new GameStoppedEventArgs(0));
                            return;
                        }

                        try
                        {
                            var id = startupCheck();
                            if (id > 0)
                            {
                                InvokeOnStarted(new GameStartedEventArgs { StartedProcessId = id });
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
                    failCount = 0;
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (failCount >= maxFailCount)
                    {
                        OnGameClosed(playTimeMs / 1000);
                        InvokeOnStopped(new GameStoppedEventArgs(playTimeMs / 1000));
                        return;
                    }

                    try
                    {
                        trackingWatch.Restart();
                        if (!trackingAction())
                        {
                            OnGameClosed(playTimeMs / 1000);
                            InvokeOnStopped(new GameStoppedEventArgs(playTimeMs / 1000));
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
                    if (trackingWatch.ElapsedMilliseconds > (trackingFrequency + 30_000))
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
        private IPlayniteAPI playniteAPI = API.Instance;
        private static ILogger logger = LogManager.GetLogger();
        public async Task<Dictionary<string, UpdateInfo>> CheckGameUpdates(string gameTitle, string gameId)
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
                bool correctJson = false;
                var overlayVersionInfo = new OverlayVersion.Rootobject();
                if (File.Exists(cacheVersionFile))
                {
                    var content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
                    if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out overlayVersionInfo))
                    {
                        if (overlayVersionInfo != null && overlayVersionInfo.Data != null && overlayVersionInfo.Data.BuildVersion != null)
                        {
                            correctJson = true;
                        }
                    }
                }
                if (!correctJson)
                {
                    var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                       .WithArguments(new[] { "status", "--json" })
                                       .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                       .AddCommandToLog()
                                       .WithValidation(CommandResultValidation.None)
                                       .ExecuteBufferedAsync();
                    var errorMessage = cmd.StandardError;
                    if (cmd.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                    {
                        logger.Error("[Legendary]" + cmd.StandardError);
                    }
                    else
                    {
                        var content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
                        if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out overlayVersionInfo))
                        {
                            if (overlayVersionInfo != null && overlayVersionInfo.Data != null && overlayVersionInfo.Data.BuildVersion != null)
                            {
                                correctJson = true;
                            }
                        }
                    }
                }
                if (correctJson)
                {
                    newVersion = overlayVersionInfo.Data.BuildVersion;
                    var overlayInstallInfo = new Installed();
                    var overlayInstallFile = Path.Combine(LegendaryLauncher.ConfigPath, "overlay_install.json");
                    var overlayInstallContent = FileSystem.ReadFileAsStringSafe(overlayInstallFile);
                    if (!overlayInstallContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(overlayInstallContent, out overlayInstallInfo))
                    {
                        if (overlayInstallInfo != null && overlayInstallInfo.Version != null)
                        {
                            if (overlayInstallInfo.Version == newVersion)
                            {
                                return gamesToUpdate;
                            }
                        }
                    }
                    var result = await LegendaryLauncher.GetUpdateSizes("eos-overlay");
                    if (result.Download_size != 0)
                    {
                        var updateInfo = new UpdateInfo
                        {
                            Version = newVersion,
                            Title = gameTitle,
                            Download_size = result.Download_size,
                            Disk_size = result.Disk_size,
                        };
                        gamesToUpdate.Add(gameId, updateInfo);
                    }
                }
                else
                {
                    logger.Error($"An error occured during checking {gameTitle} updates.");
                }
                return gamesToUpdate;
            }
            var newGameInfo = await LegendaryLauncher.GetGameInfo(gameId, false, true);
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
                                    var newDlcInfo = await LegendaryLauncher.GetGameInfo(dlc.App_name, false, true);
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
                var gameID = game.Value.App_name;
                var gameSettings = LegendaryGameSettingsView.LoadGameSettings(gameID);
                bool canUpdate = true;
                if (gameSettings.DisableGameVersionCheck == true)
                {
                    canUpdate = false;
                }
                if (canUpdate)
                {
                    LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                    var gameToUpdate = await legendaryUpdateController.CheckGameUpdates(game.Value.Title, gameID);
                    if (gameToUpdate.Count > 0)
                    {
                        foreach (var singleGame in gameToUpdate)
                        {
                            gamesToUpdate.Add(singleGame.Key, singleGame.Value);
                        }
                    }
                }
            }
            if (LegendaryLauncher.IsEOSOverlayInstalled)
            {
                LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                var overlayToUpdate = await legendaryUpdateController.CheckGameUpdates(ResourceProvider.GetString(LOC.LegendaryEOSOverlay), "eos-overlay");
                if (overlayToUpdate.Count > 0)
                {
                    gamesToUpdate.Add("eos-overlay", overlayToUpdate["eos-overlay"]);
                }
            }
            return gamesToUpdate;
        }

        public void UpdateGame(Dictionary<string, UpdateInfo> gamesToUpdate, string gameTitle = "", bool silently = false, DownloadProperties downloadProperties = null)
        {
            var updateTasks = new List<DownloadManagerData.Download>();
            if (gamesToUpdate.Count > 0)
            {
                bool canUpdate = true;
                if (canUpdate)
                {
                    if (silently)
                    {
                        var playniteApi = API.Instance;
                        playniteApi.Notifications.Add(new NotificationMessage("LegendaryGamesUpdates", ResourceProvider.GetString(LOC.LegendaryGamesUpdatesUnderway), NotificationType.Info));
                    }
                    if (downloadProperties == null)
                    {
                        var settings = LegendaryLibrary.GetSettings();
                        downloadProperties = new DownloadProperties()
                        {
                            downloadAction = DownloadAction.Update,
                            enableReordering = settings.EnableReordering,
                            maxWorkers = settings.MaxWorkers,
                            maxSharedMemory = settings.MaxSharedMemory,
                        };
                    }
                    LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
                    foreach (var gameToUpdate in gamesToUpdate)
                    {
                        var downloadData = new DownloadManagerData.Download { gameID = gameToUpdate.Key, downloadProperties = downloadProperties };
                        var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameToUpdate.Key);
                        if (wantedItem != null)
                        {
                            if (wantedItem.status == DownloadStatus.Completed)
                            {
                                downloadManager.downloadManagerData.downloads.Remove(wantedItem);
                                downloadManager.SaveData();
                                wantedItem = null;
                            }
                        }
                        if (wantedItem != null)
                        {
                            if (!silently)
                            {
                                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            var updateTask = new DownloadManagerData.Download
                            {
                                gameID = gameToUpdate.Key,
                                name = gameToUpdate.Value.Title,
                                downloadSize = Helpers.FormatSize(gameToUpdate.Value.Download_size),
                                installSize = Helpers.FormatSize(gameToUpdate.Value.Disk_size),
                                downloadProperties = downloadProperties
                            };
                            updateTasks.Add(updateTask);
                        }
                    }
                    if (updateTasks.Count > 0)
                    {
                        downloadManager.EnqueueMultipleJobs(updateTasks, silently);
                    }
                }
            }
            else if (!silently)
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryNoUpdatesAvailable), gameTitle);
            }
        }
    }
}
