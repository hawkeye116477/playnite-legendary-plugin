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
                var playArgs = new List<string>();
                playArgs.AddRange(new[] { "launch", Game.GameId });
                var gamesSettings = LegendaryGameSettingsView.LoadSavedGamesSettings();
                var globalSettings = LegendaryLibrary.GetSettings();
                var offlineModeEnabled = globalSettings.LaunchOffline;
                var gameSettings = new GameSettings();
                if (gamesSettings.ContainsKey(Game.GameId))
                {
                    gameSettings = gamesSettings[Game.GameId];
                }
                if (gameSettings?.LaunchOffline != null)
                {
                    offlineModeEnabled = (bool)gameSettings.LaunchOffline;
                }
                if (offlineModeEnabled)
                {
                    bool canRunOffline = false;
                    var appList = LegendaryLauncher.GetInstalledAppList();
                    if (appList.ContainsKey(Game.GameId))
                    {
                        if (appList[Game.GameId].Can_run_offline)
                        {
                            canRunOffline = true;
                        }
                    }
                    if (offlineModeEnabled && canRunOffline)
                    {
                        playArgs.Add("--offline");
                    }
                }
                else
                {
                    bool updateCheckDisabled = globalSettings.DisableGameVersionCheck;
                    if (gameSettings?.DisableGameVersionCheck != null)
                    {
                        updateCheckDisabled = (bool)gameSettings.DisableGameVersionCheck;
                    }
                    if (updateCheckDisabled)
                    {
                        playArgs.Add("--skip-version-check");
                    }
                }
                if (gameSettings?.StartupArguments != null)
                {
                    playArgs.AddRange(gameSettings.StartupArguments);
                }
                if (gameSettings?.LanguageCode != null)
                {
                    playArgs.AddRange(new[] { "--language", gameSettings.LanguageCode } );
                }
                if (gameSettings?.OverrideExe != null)
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
                                InvokeOnStopped(new GameStoppedEventArgs());
                                var errorMessage = stdOutBuffer.ToString();
                                logger.Debug("[Legendary] " + errorMessage);
                                logger.Error("[Legendary] exit code: " + exited.ExitCode);
                                if (errorMessage.Contains("login failed") || errorMessage.Contains("No saved credentials"))
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                                }
                                else if (errorMessage.Contains("Game is out of date"))
                                {
                                    LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                                    await legendaryUpdateController.UpdateGame(Game.Name, Game.GameId);
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameStartError), errorMessage));
                                }
                                return;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }
    }

    public class LegendaryUpdateController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static ILogger logger = LogManager.GetLogger();
        public async Task UpdateGame(string gameTitle, string gameId)
        {

            var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                               .WithArguments(new[] { "list-installed", "--check-updates" })
                               .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                               .WithValidation(CommandResultValidation.None)
                               .ExecuteBufferedAsync();
            if (cmd.StandardError.Contains("login failed") || cmd.StandardError.Contains("No saved credentials"))
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdateCheckFailMessage), ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired));
            }
            else if (cmd.StandardError.Contains("Error"))
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdateCheckFailMessage), gameTitle);
                logger.Error($"[Legendary] {cmd.StandardError}");
            }
            else
            {
                var options = new List<MessageBoxOption>
                    {
                        new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdaterInstallUpdate)),
                        new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteCancelLabel)),
                    };
                var newVersion = Regex.Match(cmd.StandardOutput, @$"\* {Regex.Escape(gameTitle)}.*\s+\-> Update available! Installed:.*, Latest: (\S+.)", RegexOptions.Multiline).Groups[1].Value.Trim();
                if (!newVersion.IsNullOrEmpty())
                {
                    var result = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryNewVersionAvailable), gameTitle, newVersion), ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                    if (result == options[0])
                    {
                        var downloadProperties = new DownloadProperties() { downloadAction = (int)DownloadAction.Update };
                        var downloadData = new DownloadManagerData.Download { gameID = gameId, downloadProperties = downloadProperties };
                        LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
                        var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameId);
                        if (wantedItem != null)
                        {
                            playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            var messagesSettings = LegendaryMessagesSettings.LoadSettings();
                            if (!messagesSettings.DontShowDownloadManagerWhatsUpMsg)
                            {
                                var okResponse = new MessageBoxOption("LOCOKLabel", true, true);
                                var dontShowResponse = new MessageBoxOption("LOCDontShowAgainTitle");
                                var response = playniteAPI.Dialogs.ShowMessage(LOC.LegendaryDownloadManagerWhatsUp, "", MessageBoxImage.Information, new List<MessageBoxOption> { okResponse, dontShowResponse });
                                if (response == dontShowResponse)
                                {
                                    messagesSettings.DontShowDownloadManagerWhatsUpMsg = true;
                                    LegendaryMessagesSettings.SaveSettings(messagesSettings);
                                }
                            }
                            await downloadManager.EnqueueJob(gameId, gameTitle, "", "", downloadProperties);
                        }
                    }
                }
                else
                {
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryNoUpdatesAvailable), gameTitle);
                }
            }
        }
    }
}
