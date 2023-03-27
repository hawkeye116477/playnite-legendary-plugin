using Playnite;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegendaryLibraryNS.Models;
using System.Windows;

namespace LegendaryLibraryNS
{
    public class LegendaryInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;
        private IPlayniteAPI playniteAPI = API.Instance;

        public LegendaryInstallController(Game game) : base(game)
        {
            Name = "Install using Legendary client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception("Legendary Launcher is not installed.");
            }

            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowCloseButton = false,
            });
            window.Title = Game.Name;
            window.DataContext = Game.GameId.ToString();
            window.Content = new LegendaryGameInstaller(Game.GameId.ToString());
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.Height = 180;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
            Dispose();
            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

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
                    await Task.Delay(10000);
                }
            });
        }
    }

    public class LegendaryUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;
        private IPlayniteAPI playniteAPI = API.Instance;

        public LegendaryUninstallController(Game game) : base(game)
        {
            Name = "Uninstall";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception("Legendary Launcher is not installed.");
            }

            Dispose();
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCLegendaryUninstallGame"), Game.Name), 
                ResourceProvider.GetString("LOCUninstallGame"), 
                MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                throw new OperationCanceledException();
            }
            else
            {
                ProcessStarter.StartProcess(LegendaryLauncher.ClientExecPath, string.Format(LegendaryLauncher.GameUninstallCommand, Game.GameId));
                StartUninstallWatcher();
            }
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                var installed = LegendaryLauncher.GetInstalledAppList();
                if (!installed.ContainsKey(Game.GameId))
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }

        public Game GetGame()
        {
            return Game;
        }
    }
}
