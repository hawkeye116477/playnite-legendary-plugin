using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var installProperties = new DownloadProperties { downloadAction = (int)Enums.DownloadAction.Install };
            var installData = new DownloadManagerData.Download { gameID = Game.GameId, downloadProperties = installProperties };
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
            else
            {
                _ = (Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                {
                    var installedAppList = LegendaryLauncher.GetInstalledAppList();
                    if (installedAppList.ContainsKey(Game.GameId))
                    {
                        var installInfo = new GameInstallationData
                        {
                            InstallDirectory = installedAppList[Game.GameId].Install_path
                        };
                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    }
                }));
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
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
            }

            Dispose();
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm), Game.Name),
                ResourceProvider.GetString(LOC.Legendary3P_PlayniteUninstallGame),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                Game.IsUninstalling = false;
            }
            else
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(new[] { "-y", "uninstall", Game.GameId })
                                   .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                if (cmd.StandardError.Contains("has been uninstalled"))
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                }
                else
                {
                    logger.Debug("[Legendary] " + cmd.StandardError);
                    logger.Error("[Legendary] exit code: " + cmd.ExitCode);
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
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public LegendaryPlayController(Game game) : base(game)
        {
            Name = string.Format(ResourceProvider.GetString(LOC.Legendary3P_EpicStartUsingClient), "Legendary");
        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override async void Play(PlayActionArgs args)
        {
            Dispose();
            if (Directory.Exists(Game.InstallDirectory))
            {
                await LaunchGame();
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
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
                if (gameSettings?.DisableGameVersionCheck == null)
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
            procMon = new ProcessMonitor();
            procMon.TreeStarted += (_, treeArgs) =>
            {
                stopWatch = Stopwatch.StartNew();
                InvokeOnStarted(new GameStartedEventArgs { StartedProcessId = treeArgs.StartedId });
            };
            procMon.TreeDestroyed += (_, __) =>
            {
                stopWatch.Stop();
                InvokeOnStopped(new GameStoppedEventArgs { SessionLength = Convert.ToUInt64(stopWatch.Elapsed.TotalSeconds) });
            };
            var stdOutBuffer = new StringBuilder();
            var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                         .WithArguments(playArgs)
                         .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                         .WithValidation(CommandResultValidation.None);
            await foreach (var cmdEvent in cmd.ListenAsync())
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        Task watchGameProcess = procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
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
                            if (errorMessage.Contains("login failed") || errorMessage.Contains("No saved credentials"))
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
                                            await LaunchGame(true);
                                            return;
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
                                await legendaryUpdateController.UpdateGame(Game.Name, Game.GameId);
                            }
                            else
                            {
                                InvokeOnStopped(new GameStoppedEventArgs());
                                playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameStartError), errorMessage));
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

    public class LegendaryUpdateController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static ILogger logger = LogManager.GetLogger();
        public Dictionary<string, Installed> CheckGameUpdates(string gameTitle, string gameId)
        {
            var newGameInfo = LegendaryLauncher.GetGameInfo(gameId);
            var gamesToUpdate = new Dictionary<string, Installed>();
            if (newGameInfo.Game != null)
            {
                var installedAppList = LegendaryLauncher.GetInstalledAppList();
                if (installedAppList.ContainsKey(gameId))
                {
                    var oldGameInfo = installedAppList[gameId];
                    if (oldGameInfo.Version != newGameInfo.Game.Version)
                    {
                        var updateInfo = new Installed
                        {
                            Version = newGameInfo.Game.Version,
                            Title = newGameInfo.Game.Title
                        };
                        gamesToUpdate.Add(oldGameInfo.App_name, updateInfo);
                    }
                    // We need to also check for DLCs updates (see https://github.com/derrod/legendary/issues/506)
                    if (newGameInfo.Game.Owned_dlc.Length > 1)
                    {
                        foreach (var dlc in newGameInfo.Game.Owned_dlc)
                        {
                            if (installedAppList.ContainsKey(dlc.App_name))
                            {
                                var oldDlcInfo = installedAppList[dlc.App_name];
                                var newDlcInfo = LegendaryLauncher.GetGameInfo(dlc.App_name);
                                if (newDlcInfo.Game != null)
                                {
                                    if (oldDlcInfo.Version != newDlcInfo.Game.Version)
                                    {
                                        var updateDlcInfo = new Installed
                                        {
                                            Version = newDlcInfo.Game.Version,
                                            Title = newDlcInfo.Game.Title
                                        };
                                        gamesToUpdate.Add(oldDlcInfo.App_name, updateDlcInfo);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdateCheckFailMessage), gameTitle);
            }
            return gamesToUpdate;
        }

        public async Task UpdateGame(string gameTitle, string gameId, bool silently = false)
        {
            var gamesToUpdate = CheckGameUpdates(gameTitle, gameId);
            if (gamesToUpdate.Count > 0)
            {
                bool canUpdate = true;
                if (!silently)
                {
                    var options = new List<MessageBoxOption>
                    {
                        new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdaterInstallUpdate)),
                        new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteCancelLabel)),
                    };
                    var result = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryNewVersionAvailable), gameTitle, gamesToUpdate.First().Value.Version), ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                    if (result != options[0])
                    {
                        canUpdate = false;
                    }
                }
                if (canUpdate)
                {
                    foreach (var gameToUpdate in gamesToUpdate)
                    {
                        var downloadProperties = new DownloadProperties() { downloadAction = (int)DownloadAction.Update };
                        var downloadData = new DownloadManagerData.Download { gameID = gameToUpdate.Key, downloadProperties = downloadProperties };
                        LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
                        var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameToUpdate.Key);
                        if (wantedItem != null)
                        {
                            if (wantedItem.status == (int)DownloadStatus.Completed)
                            {
                                downloadManager.downloadManagerData.downloads.Remove(wantedItem);
                                downloadManager.SaveData();
                                wantedItem = null;
                            }
                        }
                        if (wantedItem != null)
                        {
                            playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            if (!silently)
                            {
                                var messagesSettings = LegendaryMessagesSettings.LoadSettings();
                                if (!messagesSettings.DontShowDownloadManagerWhatsUpMsg)
                                {
                                    var okResponse = new MessageBoxOption(LOC.Legendary3P_PlayniteOKLabel, true, true);
                                    var dontShowResponse = new MessageBoxOption(LOC.Legendary3P_PlayniteDontShowAgainTitle);
                                    var response = playniteAPI.Dialogs.ShowMessage(LOC.LegendaryDownloadManagerWhatsUp, "", MessageBoxImage.Information, new List<MessageBoxOption> { okResponse, dontShowResponse });
                                    if (response == dontShowResponse)
                                    {
                                        messagesSettings.DontShowDownloadManagerWhatsUpMsg = true;
                                        LegendaryMessagesSettings.SaveSettings(messagesSettings);
                                    }
                                }
                            }
                            await downloadManager.EnqueueJob(gameToUpdate.Key, gameToUpdate.Value.Title, "", "", downloadProperties);
                        }
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
