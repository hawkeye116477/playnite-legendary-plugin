using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryLibrarySettingsView.xaml
    /// </summary>
    public partial class LegendaryLibrarySettingsView : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public LegendaryTroubleshootingInformation troubleshootingInformation;

        public LegendaryLibrarySettingsView()
        {
            InitializeComponent();
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile("Legendary|legendary.exe");
            if (file != "")
            {
                var path = System.IO.Path.GetDirectoryName(file);
                SelectedLauncherPathTxt.Text = path;
            }
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
            }
        }

        private void ExcludeOnlineGamesBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
            });
            window.Content = new LegendaryExcludeOnlineGames();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.Height = 450;
            window.Width = 800;
            window.Title = ResourceProvider.GetString(LOC.LegendaryExcludeGames);
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private async void EOSOUninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm), ResourceProvider.GetString(LOC.LegendaryEOSOverlay)), ResourceProvider.GetString(LOC.Legendary3P_PlayniteUninstallGame), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithValidation(CommandResultValidation.None)
                                   .WithArguments(new[] { "-y", "eos-overlay", "remove" })
                                   .ExecuteBufferedAsync();
                if (cmd.StandardError.Contains("Done"))
                {
                    EOSOInstallBtn.Visibility = Visibility.Visible;
                    EOSOUninstallBtn.Visibility = Visibility.Collapsed;
                    EOSOToggleBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void EOSOInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
                return;
            }
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = ResourceProvider.GetString(LOC.LegendaryEOSOverlay);
            var installProperties = new DownloadProperties { downloadAction = (int)DownloadAction.Install };
            var installData = new DownloadManagerData.Download { gameID = "eos-overlay", downloadProperties = installProperties };
            window.DataContext = installData;
            window.Content = new LegendaryGameInstaller();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            if (result == true)
            {
                if (LegendaryLauncher.IsEOSOverlayInstalled)
                {
                    EOSOInstallBtn.Visibility = Visibility.Collapsed;
                    EOSOUninstallBtn.Visibility = Visibility.Visible;
                    EOSOToggleBtn.Content = ResourceProvider.GetString(LOC.LegendaryDisable);
                    EOSOToggleBtn.Visibility = Visibility.Visible;
                }
            }
        }

        private async void EOSOToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            var toggleCommand = "disable";
            if (!LegendaryLauncher.IsEOSOverlayEnabled)
            {
                toggleCommand = "enable";
            }
            await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                     .WithArguments(new[] { "-y", "eos-overlay", toggleCommand })
                     .WithValidation(CommandResultValidation.None)
                     .ExecuteAsync();
            var toggleTxt = LOC.LegendaryEnable;
            if (LegendaryLauncher.IsEOSOverlayEnabled)
            {
                toggleTxt = LOC.LegendaryDisable;
            }
            EOSOToggleBtn.Content = ResourceProvider.GetString(toggleTxt);
        }

        private async void LegendarySettingsUC_Loaded(object sender, RoutedEventArgs e)
        {
            var installedAddons = playniteAPI.Addons.Addons;
            if (installedAddons.Contains("EpicGamesLibrary_Builtin"))
            {
                MigrateEpicBtn.IsEnabled = true;
            }

            if (!LegendaryLauncher.IsEOSOverlayInstalled)
            {
                EOSOInstallBtn.Visibility = Visibility.Visible;
                EOSOToggleBtn.Visibility = Visibility.Collapsed;
                EOSOUninstallBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (!LegendaryLauncher.IsEOSOverlayEnabled)
                {
                    EOSOToggleBtn.Content = ResourceProvider.GetString(LOC.LegendaryEnable);
                }
            }

            var downloadCompleteActions = new Dictionary<int, string>
            {
                { (int)DownloadCompleteAction.Nothing, ResourceProvider.GetString(LOC.Legendary3P_PlayniteDoNothing) },
                { (int)DownloadCompleteAction.ShutDown, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuShutdownSystem) },
                { (int)DownloadCompleteAction.Reboot, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuRestartSystem) },
                { (int)DownloadCompleteAction.Hibernate, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuHibernateSystem) },
                { (int)DownloadCompleteAction.Sleep, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuSuspendSystem) },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;

            var autoClearOptions = new Dictionary<int, string>
            {
                { (int)ClearCacheTime.Day, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceADay) },
                { (int)ClearCacheTime.Week, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceAWeek) },
                { (int)ClearCacheTime.Month, ResourceProvider.GetString(LOC.LegendaryOnceAMonth) },
                { (int)ClearCacheTime.ThreeMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery3Months) },
                { (int)ClearCacheTime.SixMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery6Months) },
                { (int)ClearCacheTime.Never, ResourceProvider.GetString(LOC.Legendary3P_PlayniteSettingsPlaytimeImportModeNever) }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;

            troubleshootingInformation = new LegendaryTroubleshootingInformation();
            if (LegendaryLauncher.IsInstalled)
            {
                var verionCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithArguments(new[] { "-V" })
                                         .WithValidation(CommandResultValidation.None)
                                         .ExecuteBufferedAsync();
                if (verionCmd.StandardOutput.Contains("version"))
                {
                    troubleshootingInformation.LauncherVersion = Regex.Match(verionCmd.StandardOutput, @"\d+(\.\d+)+").Value;
                    LauncherVersionTxt.Text = troubleshootingInformation.LauncherVersion;
                }
            }
            else
            {
                LauncherVersionTxt.Text = ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled);
            }

            PlayniteVersionTxt.Text = troubleshootingInformation.PlayniteVersion;
            PluginVersionTxt.Text = troubleshootingInformation.PluginVersion;
            LauncherBinaryTxt.Text = troubleshootingInformation.LauncherBinary;
            if (LauncherBinaryTxt.Text.IsNullOrEmpty())
            {
                LauncherBinaryTxt.Text = ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled);
                OpenLauncherBinaryBtn.IsEnabled = false;
            }
            GamesInstallationPathTxt.Text = troubleshootingInformation.GamesInstallationPath;
            ReportBugHyp.NavigateUri = new Uri($"https://github.com/hawkeye116477/playnite-legendary-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&legendaryV={troubleshootingInformation.PluginVersion}&playniteV={troubleshootingInformation.PlayniteVersion}&launcherV={troubleshootingInformation.LauncherVersion}");
        }

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryClearCacheConfirm), ResourceProvider.GetString(LOC.Legendary3P_PlayniteSettingsClearCacheTitle), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var cacheDirs = new List<string>()
                {
                    LegendaryLibrary.Instance.GetCachePath("catalogcache"),
                    LegendaryLibrary.Instance.GetCachePath("infocache"),
                    LegendaryLibrary.Instance.GetCachePath("sdlcache")
                };
                foreach (var cacheDir in cacheDirs)
                {
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                    }
                }
            }
        }

        private void SyncGameSavesChk_Click(object sender, RoutedEventArgs e)
        {
            if (SyncGameSavesChk.IsChecked == true)
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendarySyncGameSavesWarn), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MigrateEpicBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
                return;
            }
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryMigrationConfirm), ResourceProvider.GetString(LOC.LegendaryMigrateGamesEpic), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendaryMigratingGamesEpic), false) { IsIndeterminate = false };
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                using (playniteAPI.Database.BufferedUpdate())
                {
                    var gamesToMigrate = playniteAPI.Database.Games.Where(i => i.PluginId == Guid.Parse("00000002-DBD1-46C6-B5D0-B1BA559D10E4")).ToList();
                    var migratedGames = new List<string>();
                    var notImportedGames = new List<string>();
                    if (gamesToMigrate.Count > 0)
                    {
                        var iterator = 0;
                        a.ProgressMaxValue = gamesToMigrate.Count() + 1;
                        a.CurrentProgressValue = 0;
                        foreach (var game in gamesToMigrate.ToList())
                        {
                            iterator++;
                            var alreadyExists = playniteAPI.Database.Games.FirstOrDefault(i => i.GameId == game.GameId && i.PluginId == LegendaryLibrary.Instance.Id);
                            if (alreadyExists == null)
                            {
                                game.PluginId = LegendaryLibrary.Instance.Id;
                                if (game.IsInstalled)
                                {
                                    var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                             .WithArguments(new[] { "-y", "import", game.GameId, game.InstallDirectory })
                                                             .WithValidation(CommandResultValidation.None)
                                                             .ExecuteBufferedAsync();
                                    if (!importCmd.StandardError.Contains("has been imported"))
                                    {
                                        notImportedGames.Add(game.GameId);
                                        game.IsInstalled = false;
                                        logger.Debug("[Legendary] " + importCmd.StandardError);
                                        logger.Error("[Legendary] exit code: " + importCmd.ExitCode);
                                    }
                                }
                                playniteAPI.Database.Games.Update(game);
                                migratedGames.Add(game.GameId);
                                a.CurrentProgressValue = iterator;
                            }
                        }
                        a.CurrentProgressValue = gamesToMigrate.Count() + 1;
                        if (migratedGames.Count > 0)
                        {
                            playniteAPI.Dialogs.ShowMessage(LOC.LegendaryMigrationCompleted, LOC.LegendaryMigrateGamesEpic, MessageBoxButton.OK, MessageBoxImage.Information);
                            logger.Info("Successfully migrated " + migratedGames.Count + " game(s) from Epic to Legendary.");
                        }
                        if (notImportedGames.Count > 0)
                        {
                            logger.Info(notImportedGames.Count + " game(s) probably needs to be imported or installed again.");
                        }
                    }
                    else
                    {
                        a.ProgressMaxValue = 1;
                        a.CurrentProgressValue = 1;
                        playniteAPI.Dialogs.ShowErrorMessage(LOC.LegendaryMigrationNoGames);
                    }
                }
            }, globalProgressOptions);
        }

        private void CopyRawDataBtn_Click(object sender, RoutedEventArgs e)
        {
            var troubleshootingJSON = Playnite.SDK.Data.Serialization.ToJson(troubleshootingInformation, true);
            Clipboard.SetText(troubleshootingJSON);
        }

        private void LogFilesFolderHyp_Click(object sender, RoutedEventArgs e)
        {
            ProcessStarter.StartProcess("explorer.exe", playniteAPI.Paths.ConfigurationPath);
        }

        private void OpenGamesInstallationPathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(troubleshootingInformation.GamesInstallationPath))
            {
                ProcessStarter.StartProcess("explorer.exe", troubleshootingInformation.GamesInstallationPath);
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(LOC.LegendaryPathNotExist);
            }
        }

        private void OpenLauncherBinaryBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryLauncher.StartClient();
        }
    }
}
