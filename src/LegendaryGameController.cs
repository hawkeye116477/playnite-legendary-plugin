using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Models;
using Playnite;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryLibraryNS
{
    public class LegendaryInstallController : InstallController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static LegendaryInstallController Instance { get; set; }

        public LegendaryInstallController(Game game) : base(game)
        {
            Name = "Install using Legendary client";
        }

        public override void Install(InstallActionArgs args)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception("Legendary Launcher is not installed.");
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
                    var installed = LegendaryLauncher.GetInstalledAppList();
                    if (installed != null)
                    {
                        foreach (KeyValuePair<string, Installed> app in installed)
                        {
                            if (app.Value.App_name == Game.GameId)
                            {
                                var installInfo = new GameInstallationData
                                {
                                    InstallDirectory = app.Value.Install_path
                                };

                                InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                                return;
                            }
                        }
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
                throw new Exception("Legendary Launcher is not installed.");
            }

            Dispose();
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm), Game.Name),
                ResourceProvider.GetString("LOCUninstallGame"),
                MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                Game.IsUninstalling = false;
            }
            else
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(new[] { "-y", "uninstall", Game.GameId })
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
            Name = string.Format(ResourceProvider.GetString(LOC.EpicStartUsingClient), "Legendary");
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
                if (LegendaryLibrary.GetSettings().LaunchOffline)
                {
                    bool canRunOffline = false;
                    var appList = LegendaryLauncher.GetInstalledAppList();
                    foreach (KeyValuePair<string, Installed> d in appList)
                    {
                        var app = d.Value;
                        if (app.App_name == Game.GameId)
                        {
                            if (app.Can_run_offline && !LegendaryLibrary.GetSettings().OnlineList.Contains(app.App_name))
                            {
                                canRunOffline = true;
                            }
                            break;
                        }
                    }
                    if (LegendaryLibrary.GetSettings().LaunchOffline && canRunOffline)
                    {
                        playArgs.Add("--offline");
                    }
                }
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(playArgs)
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                if (cmd.ExitCode != 0)
                {
                    InvokeOnStopped(new GameStoppedEventArgs());
                    logger.Error("[Legendary] " + cmd.StandardError);
                    if (cmd.StandardError.Contains("login failed"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString("LOCGameStartError"), ResourceProvider.GetString("LOCLoginRequired")));
                    }
                    else
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString("LOCGameStartError"), ResourceProvider.GetString(cmd.StandardError)));
                    }
                    return;
                }
                void gameStarted(int processId)
                {
                    stopWatch = Stopwatch.StartNew();
                    InvokeOnStarted(new GameStartedEventArgs { StartedProcessId = processId });
                }
                procMon = new ProcessMonitor();
                procMon.TreeStarted += (_, treeArgs) => gameStarted(treeArgs.StartedId);
                procMon.TreeDestroyed += (_, __) =>
                {
                    stopWatch.Stop();
                    InvokeOnStopped(new GameStoppedEventArgs { SessionLength = Convert.ToUInt64(stopWatch.Elapsed.TotalSeconds) });
                };
                stopWatch = Stopwatch.StartNew();
                procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }
    }
}
