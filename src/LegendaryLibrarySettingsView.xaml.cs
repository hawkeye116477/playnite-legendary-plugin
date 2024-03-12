using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

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
            UpdateAuthStatus();
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile($"{ResourceProvider.GetString(LOC.Legendary3P_PlayniteExecutableTitle)}|legendary.exe");
            if (file != "")
            {
                var path = Path.GetDirectoryName(file);
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

        private async void EOSOUninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm), ResourceProvider.GetString(LOC.LegendaryEOSOverlay)), ResourceProvider.GetString(LOC.Legendary3P_PlayniteUninstallGame), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithValidation(CommandResultValidation.None)
                                   .WithArguments(new[] { "-y", "eos-overlay", "remove" })
                                   .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                   .AddCommandToLog()
                                   .ExecuteBufferedAsync();
                if (cmd.StandardError.Contains("Done"))
                {
                    EOSOInstallBtn.Visibility = Visibility.Visible;
                    EOSOUninstallBtn.Visibility = Visibility.Collapsed;
                    EOSOToggleBtn.Visibility = Visibility.Collapsed;
                    EOSOCheckForUpdatesBtn.Visibility = Visibility.Collapsed;
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryUninstallSuccess).Format(ResourceProvider.GetString(LOC.LegendaryEOSOverlay)));
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
            var installProperties = new DownloadProperties { downloadAction = DownloadAction.Install };
            var installData = new DownloadManagerData.Download { name = ResourceProvider.GetString(LOC.LegendaryEOSOverlay), gameID = "eos-overlay", downloadProperties = installProperties };
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
                    EOSOCheckForUpdatesBtn.Visibility = Visibility.Visible;
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
                     .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                     .AddCommandToLog()
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
                EOSOCheckForUpdatesBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (!LegendaryLauncher.IsEOSOverlayEnabled)
                {
                    EOSOToggleBtn.Content = ResourceProvider.GetString(LOC.LegendaryEnable);
                }
            }

            var downloadCompleteActions = new Dictionary<DownloadCompleteAction, string>
            {
                { DownloadCompleteAction.Nothing, ResourceProvider.GetString(LOC.Legendary3P_PlayniteDoNothing) },
                { DownloadCompleteAction.ShutDown, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuShutdownSystem) },
                { DownloadCompleteAction.Reboot, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuRestartSystem) },
                { DownloadCompleteAction.Hibernate, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuHibernateSystem) },
                { DownloadCompleteAction.Sleep, ResourceProvider.GetString(LOC.Legendary3P_PlayniteMenuSuspendSystem) },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;

            var updatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, ResourceProvider.GetString(LOC.LegendaryCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceADay) },
                { UpdatePolicy.Week, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, ResourceProvider.GetString(LOC.LegendaryOnceAMonth) },
                { UpdatePolicy.ThreeMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery3Months) },
                { UpdatePolicy.SixMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery6Months) },
                { UpdatePolicy.GameLaunch, ResourceProvider.GetString(LOC.LegendaryCheckUpdatesGameLaunch) },
                { UpdatePolicy.Never, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnlyManually) }
            };
            GamesUpdatesCBo.ItemsSource = updatePolicyOptions;

            var launcherUpdatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, ResourceProvider.GetString(LOC.LegendaryCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceADay) },
                { UpdatePolicy.Week, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, ResourceProvider.GetString(LOC.LegendaryOnceAMonth) },
                { UpdatePolicy.ThreeMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery3Months) },
                { UpdatePolicy.SixMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery6Months) },
                { UpdatePolicy.Never, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnlyManually) }
            };
            LauncherUpdatesCBo.ItemsSource = launcherUpdatePolicyOptions;

            var autoClearOptions = new Dictionary<ClearCacheTime, string>
            {
                { ClearCacheTime.Day, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceADay) },
                { ClearCacheTime.Week, ResourceProvider.GetString(LOC.Legendary3P_PlayniteOptionOnceAWeek) },
                { ClearCacheTime.Month, ResourceProvider.GetString(LOC.LegendaryOnceAMonth) },
                { ClearCacheTime.ThreeMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery3Months) },
                { ClearCacheTime.SixMonths, ResourceProvider.GetString(LOC.LegendaryOnceEvery6Months) },
                { ClearCacheTime.Never, ResourceProvider.GetString(LOC.Legendary3P_PlayniteSettingsPlaytimeImportModeNever) }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;

            troubleshootingInformation = new LegendaryTroubleshootingInformation();
            if (LegendaryLauncher.IsInstalled)
            {
                var launcherVersion = await LegendaryLauncher.GetLauncherVersion();
                if (launcherVersion != "0")
                {
                    troubleshootingInformation.LauncherVersion = launcherVersion;
                    LauncherVersionTxt.Text = troubleshootingInformation.LauncherVersion;
                }
                LauncherBinaryTxt.Text = troubleshootingInformation.LauncherBinary;
            }
            else
            {
                LauncherVersionTxt.Text = ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled);
                LauncherBinaryTxt.Text = ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled);
                CheckForUpdatesBtn.IsEnabled = false;
                OpenLauncherBinaryBtn.IsEnabled = false;
            }

            PlayniteVersionTxt.Text = troubleshootingInformation.PlayniteVersion;
            PluginVersionTxt.Text = troubleshootingInformation.PluginVersion;
            GamesInstallationPathTxt.Text = troubleshootingInformation.GamesInstallationPath;
            LogFilesPathTxt.Text = playniteAPI.Paths.ConfigurationPath;
            ReportBugHyp.NavigateUri = new Uri($"https://github.com/hawkeye116477/playnite-legendary-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&legendaryV={troubleshootingInformation.PluginVersion}&playniteV={troubleshootingInformation.PlayniteVersion}&launcherV={troubleshootingInformation.LauncherVersion}");

            if (playniteAPI.ApplicationSettings.PlaytimeImportMode == PlaytimeImportMode.Never)
            {
                SyncPlaytimeChk.IsEnabled = false;
            }
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
                    LegendaryLibrary.Instance.GetCachePath("sdlcache"),
                    Path.Combine(LegendaryLauncher.ConfigPath, "metadata")
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
                                                             .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                                             .AddCommandToLog()
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
            var troubleshootingJSON = Serialization.ToJson(troubleshootingInformation, true);
            Clipboard.SetText(troubleshootingJSON);
        }


        private void OpenGamesInstallationPathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(troubleshootingInformation.GamesInstallationPath))
            {
                ProcessStarter.StartProcess("explorer.exe", troubleshootingInformation.GamesInstallationPath);
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(LOC.LegendaryPathNotExistsError);
            }
        }

        private void OpenLauncherBinaryBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryLauncher.StartClient();
        }

        private async void CheckForUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            var versionInfoContent = await LegendaryLauncher.GetVersionInfoContent();
            if (versionInfoContent.release_info != null)
            {
                var newVersion = versionInfoContent.release_info.version;
                if (troubleshootingInformation.LauncherVersion != newVersion)
                {
                    var options = new List<MessageBoxOption>
                    {
                        new MessageBoxOption(ResourceProvider.GetString(LOC.LegendaryViewChangelog)),
                        new MessageBoxOption(ResourceProvider.GetString(LOC.Legendary3P_PlayniteOKLabel)),
                    };
                    var result = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryNewVersionAvailable), "Legendary Launcher", newVersion), ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                    if (result == options[0])
                    {
                        var changelogURL = versionInfoContent.release_info.gh_url;
                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                    }
                }
                else
                {
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryNoUpdatesAvailable));
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdateCheckFailMessage), "Legendary Launcher");
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var clientApi = new EpicAccountClient(playniteAPI, LegendaryLauncher.TokensPath);
            var userLoggedIn = LoginBtn.IsChecked;
            if (!userLoggedIn == false)
            {
                try
                {
                    await clientApi.Login();
                }
                catch (Exception ex) when (!Debugger.IsAttached)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.Legendary3P_EpicNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                }
                UpdateAuthStatus();
            }
            else
            {
                var answer = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendarySignOutConfirm), LOC.LegendarySignOut, MessageBoxButton.YesNo);
                if (answer == MessageBoxResult.Yes)
                {
                    if (LegendaryLauncher.IsInstalled)
                    {
                        var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                              .WithArguments(new[] { "auth", "--delete" })
                                              .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                              .AddCommandToLog()
                                              .WithValidation(CommandResultValidation.None)
                                              .ExecuteBufferedAsync();
                        if (!result.StandardError.Contains("User data deleted"))
                        {
                            logger.Error($"[Legendary] Failed to sign out. Error: {result.StandardError}");
                            return;
                        }
                    }
                    else
                    {
                        File.Delete(LegendaryLauncher.TokensPath);
                    }
                    using (var view = playniteAPI.WebViews.CreateView(new WebViewSettings
                    {
                        WindowWidth = 580,
                        WindowHeight = 700,
                    }))
                    {
                        view.DeleteDomainCookies(".epicgames.com");
                    }
                    UpdateAuthStatus();
                }
                else
                {
                    LoginBtn.IsChecked = true;
                }
            }
        }

        private async void UpdateAuthStatus()
        {
            LoginBtn.IsEnabled = false;
            AuthStatusTB.Text = ResourceProvider.GetString(LOC.Legendary3P_EpicLoginChecking);
            var clientApi = new EpicAccountClient(playniteAPI, LegendaryLauncher.TokensPath);
            var userLoggedIn = await clientApi.GetIsUserLoggedIn();
            if (userLoggedIn)
            {
                AuthStatusTB.Text = ResourceProvider.GetString(LOC.LegendarySignedInAs).Format(clientApi.GetUsername());
                LoginBtn.Content = ResourceProvider.GetString(LOC.LegendarySignOut);
                LoginBtn.IsChecked = true;
            }
            else
            {
                AuthStatusTB.Text = ResourceProvider.GetString(LOC.Legendary3P_EpicNotLoggedIn);
                LoginBtn.Content = ResourceProvider.GetString(LOC.Legendary3P_EpicAuthenticateLabel);
                LoginBtn.IsChecked = false;
            }
            LoginBtn.IsEnabled = true;
        }

        private async void ActivateUbisoftBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
                return;
            }
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryContinueActivation).Format("Ubisoft"), "", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                bool warningDisplayed = false;
                bool errorDisplayed = false;
                bool successDisplayed = false;
                var errorBuffer = new StringBuilder();
                var warningBuffer = new StringBuilder();
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                             .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                             .WithArguments(new[] { "activate", "-U" })
                             .AddCommandToLog()
                             .WithValidation(CommandResultValidation.None);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync())
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            break;
                        case StandardErrorCommandEvent stdErr:
                            var activatedTitlesMatch = Regex.Match(stdErr.Text, @"(\d+) titles have already been activated on your Ubisoft account");
                            if (activatedTitlesMatch.Length >= 1)
                            {
                                successDisplayed = true;
                                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryAllActivatedUbisoft).Format("Ubisoft"));
                            }
                            if (stdErr.Text.Contains("Redeemed all"))
                            {
                                successDisplayed = true;
                                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryGamesActivateSuccess).Format("Ubisoft"));
                            }
                            if (stdErr.Text.Contains("WARNING"))
                            {
                                warningDisplayed = true;
                                warningBuffer.AppendLine(stdErr.Text);
                            }
                            if (stdErr.Text.Contains("ERROR"))
                            {
                                errorDisplayed = true;
                                errorBuffer.AppendLine(stdErr.Text);
                            }
                            break;
                        case ExitedCommandEvent exited:
                            if (errorDisplayed)
                            {
                                var errorMessage = errorBuffer.ToString();
                                logger.Error($"[Legendary] {errorMessage}");
                                if (errorMessage.Contains("No linked"))
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryNoLinkedAccount).Format("Ubisoft"));
                                    ProcessStarter.StartUrl("https://www.epicgames.com/id/link/ubisoft");
                                }
                                else if (errorMessage.Contains("Failed to establish a new connection")
                                    || errorMessage.Contains("Log in failed")
                                    || errorMessage.Contains("Login failed")
                                    || errorMessage.Contains("No saved credentials"))
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryGamesActivateFailure).Format("Ubisoft", ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryGamesActivateFailure).Format("Ubisoft", ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                                }
                            }
                            if (warningDisplayed)
                            {
                                var warningMessage = warningBuffer.ToString();
                                logger.Warn($"[Legendary] {warningMessage}");
                                if (!successDisplayed && !errorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryGamesActivateFailure).Format("Ubisoft", ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                                }
                            }
                            break;
                    }
                }
            }

        }

        private void ActivateEaBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsEaAppInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteClientNotInstalledError).Format("EA App"));
                return;
            }
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = $"{ResourceProvider.GetString(LOC.LegendaryActivateGames)} (EA)";
            window.Content = new LegendaryEaActivate();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void GamesUpdateCheckBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
                return;
            }

            var gamesUpdates = new Dictionary<string, UpdateInfo>();
            LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
            GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendaryCheckingForUpdates), false) { IsIndeterminate = true };
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                gamesUpdates = await legendaryUpdateController.CheckAllGamesUpdates();
            }, updateCheckProgressOptions);
            if (gamesUpdates.Count > 0)
            {
                var successUpdates = gamesUpdates.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                if (successUpdates.Count > 0)
                {
                    Window window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                    {
                        ShowMaximizeButton = false,
                    });
                    window.DataContext = successUpdates;
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
                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdateCheckFailMessage));
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryNoUpdatesAvailable));
            }
        }

        private void GamesUpdatesCBo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedValue = (KeyValuePair<UpdatePolicy, string>)GamesUpdatesCBo.SelectedItem;
            if (selectedValue.Key == UpdatePolicy.Never || selectedValue.Key == UpdatePolicy.GameLaunch)
            {
                AutoUpdateGamesChk.IsEnabled = false;
            }
            else
            {
                AutoUpdateGamesChk.IsEnabled = true;
            }
        }

        private void OpenLogFilesPathBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessStarter.StartProcess("explorer.exe", playniteAPI.Paths.ConfigurationPath);
        }

        private void EOSOCheckForUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
            var gamesToUpdate = new Dictionary<string, UpdateInfo>();
            GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendaryCheckingForUpdates), false) { IsIndeterminate = true };
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(ResourceProvider.GetString(LOC.LegendaryEOSOverlay), "eos-overlay");
            }, updateCheckProgressOptions);
            if (gamesToUpdate.Count > 0)
            {
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
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryNoUpdatesAvailable), ResourceProvider.GetString(LOC.LegendaryEOSOverlay));
            }

        }

        private void ImportUbisoftLauncherGamesChk_Click(object sender, RoutedEventArgs e)
        {
            if (ImportUbisoftLauncherGamesChk.IsChecked == true)
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryThirdPartyLauncherImportWarn).Format("Ubisoft Connect"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
