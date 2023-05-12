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
            Instance = this;
        }

        public static void CompleteInstall(GameInstalledEventArgs installArgs)
        {
            Instance.InvokeOnInstalled(installArgs);
        }

        public override void Install(InstallActionArgs args)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                throw new Exception("Legendary Launcher is not installed.");
            }

            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
            });
            window.Title = Game.Name;
            window.DataContext = Game.GameId.ToString();
            window.Content = new LegendaryGameInstaller();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
            Game.IsInstalling = false;
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
}
