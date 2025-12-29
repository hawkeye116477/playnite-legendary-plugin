using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExecutableTitle)}|legendary.exe");
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
                LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonOverlay, new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS"}) }), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame), MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                    playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonUninstallSuccess, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonOverlay, new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS"}) }));
                }
            }
        }

        private void EOSOInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                LegendaryLauncher.ShowNotInstalledError();
                return;
            }
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = LocalizationManager.Instance.GetString(LOC.CommonOverlay, new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS"});
            var installProperties = new DownloadProperties { downloadAction = DownloadAction.Install };
            var installData = new DownloadManagerData.Download { name = LocalizationManager.Instance.GetString(LOC.CommonOverlay, new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS"}), gameID = "eos-overlay", downloadProperties = installProperties };
            var installDataList = new List<DownloadManagerData.Download>
            {
                installData
            };
            window.DataContext = installDataList;
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
                    EOSOToggleBtn.Content = LocalizationManager.Instance.GetString(LOC.LegendaryDisable);
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
            EOSOToggleBtn.Content = LocalizationManager.Instance.GetString(toggleTxt);
        }

        private async void LegendarySettingsUC_Loaded(object sender, RoutedEventArgs e)
        {
            var installedAddons = playniteAPI.Addons.Addons;
            if (installedAddons.Contains("EpicGamesLibrary_Builtin"))
            {
                MigrateEpicBtn.IsEnabled = true;
                MigrateRevertBtn.IsEnabled = true;
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
                    EOSOToggleBtn.Content = LocalizationManager.Instance.GetString(LOC.LegendaryEnable);
                }
            }

            var downloadCompleteActions = new Dictionary<DownloadCompleteAction, string>
            {
                { DownloadCompleteAction.Nothing, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDoNothing) },
                { DownloadCompleteAction.ShutDown, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuShutdownSystem) },
                { DownloadCompleteAction.Reboot, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuRestartSystem) },
                { DownloadCompleteAction.Hibernate, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuHibernateSystem) },
                { DownloadCompleteAction.Sleep, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuSuspendSystem) },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;

            var updatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { UpdatePolicy.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { UpdatePolicy.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { UpdatePolicy.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { UpdatePolicy.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnlyManually) }
            };
            GamesUpdatesCBo.ItemsSource = updatePolicyOptions;

            var launcherUpdatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { UpdatePolicy.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { UpdatePolicy.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { UpdatePolicy.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { UpdatePolicy.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnlyManually) }
            };
            LauncherUpdatesCBo.ItemsSource = launcherUpdatePolicyOptions;

            var autoClearOptions = new Dictionary<ClearCacheTime, string>
            {
                { ClearCacheTime.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { ClearCacheTime.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { ClearCacheTime.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { ClearCacheTime.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { ClearCacheTime.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { ClearCacheTime.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsPlaytimeImportModeNever) }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;

            AutoRemoveCompletedDownloadsCBo.ItemsSource = autoClearOptions;

            var launcherUpdateSourceFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "LauncherUpdateSource.json");
            List<string> repoList = new List<string>();
            if (File.Exists(launcherUpdateSourceFile)) {
                var content = FileSystem.ReadFileAsStringSafe(launcherUpdateSourceFile);
                var savedRepoList = new List<string>();
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out savedRepoList))
                {
                    repoList = savedRepoList;
                }
            }
            LauncherUpdateSourceCBo.ItemsSource = repoList;

            troubleshootingInformation = new LegendaryTroubleshootingInformation();
            if (LegendaryLauncher.IsInstalled)
            {
                var launcherVersion = await LegendaryLauncher.GetLauncherVersion();
                if (!launcherVersion.IsNullOrWhiteSpace())
                {
                    troubleshootingInformation.LauncherVersion = launcherVersion;
                    LauncherVersionTxt.Text = troubleshootingInformation.LauncherVersion;
                }
                LauncherBinaryTxt.Text = troubleshootingInformation.LauncherBinary;
            }
            else
            {
                troubleshootingInformation.LauncherVersion = "Not%20installed";
                LauncherVersionTxt.Text = LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled);
                LauncherBinaryTxt.Text = LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled);
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
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonClearCacheConfirm), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsClearCacheTitle), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                LegendaryLauncher.ClearCache();
            }
        }

        private void SyncGameSavesChk_Click(object sender, RoutedEventArgs e)
        {
            if (SyncGameSavesChk.IsChecked == true)
            {
                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonSyncGameSavesWarn), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MigrateEpicBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                LegendaryLauncher.ShowNotInstalledError();
                return;
            }
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationConfirm), LocalizationManager.Instance.GetString(LOC.CommonMigrateGamesOriginal), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMigratingGamesOriginal), false) { IsIndeterminate = false };
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
                            playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationCompleted), LocalizationManager.Instance.GetString(LOC.CommonMigrateGamesOriginal), MessageBoxButton.OK, MessageBoxImage.Information);
                            logger.Info("Successfully migrated " + migratedGames.Count + " game(s) from Epic to Legendary.");
                        }
                        if (notImportedGames.Count > 0)
                        {
                            logger.Info(notImportedGames.Count + " game(s) probably needs to be imported or installed again.");
                        }
                        if (migratedGames.Count == 0 && notImportedGames.Count == 0)
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                        }
                    }
                    else
                    {
                        a.ProgressMaxValue = 1;
                        a.CurrentProgressValue = 1;
                        playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
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
                playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonPathNotExistsError));
            }
        }

        private void OpenLauncherBinaryBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryLauncher.StartClient();
        }

        private async void CheckForUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            var versionInfoContent = await LegendaryLauncher.GetVersionInfoContent();
            if (versionInfoContent.Tag_name != null && Version.TryParse(versionInfoContent.Tag_name, out Version newValidVersion))
            {
                var newVersion = new Version(versionInfoContent.Tag_name);
                var oldVersion = new Version(troubleshootingInformation.LauncherVersion);
                if (oldVersion.CompareTo(newVersion) < 0)
                {
                    var options = new List<MessageBoxOption>
                    {
                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonViewChangelog)),
                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel)),
                    };
                    var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNewVersionAvailable, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)"Legendary Launcher", ["appVersion"] = (FluentString)newVersion.ToString() }), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                    if (result == options[0])
                    {
                        var changelogURL = versionInfoContent.Html_url;
                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                    }
                }
                else
                {
                    playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable));
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage), "Legendary Launcher");
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var clientApi = new EpicAccountClient(playniteAPI);
            var userLoggedIn = LoginBtn.IsChecked;
            if (!userLoggedIn == false)
            {
                try
                {
                    await clientApi.Login();
                }
                catch (Exception ex) when (!Debugger.IsAttached)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                }
                UpdateAuthStatus();
            }
            else
            {
                var answer = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonSignOutConfirm), LocalizationManager.Instance.GetString(LOC.CommonSignOut), MessageBoxButton.YesNo);
                if (answer == MessageBoxResult.Yes)
                {
                    FileSystem.DeleteFileSafe(LegendaryLauncher.EncryptedTokensPath);
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
                        FileSystem.DeleteFileSafe(LegendaryLauncher.TokensPath);
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

        public async void UpdateAuthStatus()
        {
            LoginBtn.IsEnabled = false;
            AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicLoginChecking);
            var clientApi = new EpicAccountClient(playniteAPI);
            var userLoggedIn = await clientApi.GetIsUserLoggedIn();
            if (userLoggedIn)
            {
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSignedInAs, new Dictionary<string, IFluentType> { ["userName"] = (FluentString)clientApi.GetUsername() });
                LoginBtn.Content = LocalizationManager.Instance.GetString(LOC.CommonSignOut);
                LoginBtn.IsChecked = true;
            }
            else
            {
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicNotLoggedIn);
                LoginBtn.Content = LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicAuthenticateLabel);
                LoginBtn.IsChecked = false;
            }
            LoginBtn.IsEnabled = true;
        }

        private async void ActivateUbisoftBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                LegendaryLauncher.ShowNotInstalledError();
                return;
            }
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.LegendaryContinueActivation, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft" }), "", MessageBoxButton.YesNo);
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
                                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.LegendaryAllActivatedUbisoft, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft" }));
                            }
                            if (stdErr.Text.Contains("Redeemed all"))
                            {
                                successDisplayed = true;
                                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateSuccess, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft" }));
                            }
                            if (stdErr.Text.Contains("WARNING"))
                            {
                                warningDisplayed = true;
                                warningBuffer.AppendLine(stdErr.Text);
                            }
                            if (stdErr.Text.Contains("ERROR") || stdErr.Text.Contains("exceptions"))
                            {
                                errorDisplayed = true; 
                            }
                            errorBuffer.AppendLine(stdErr.Text);
                            break;
                        case ExitedCommandEvent exited:
                            if (errorDisplayed)
                            {
                                var errorMessage = errorBuffer.ToString();
                                logger.Error($"[Legendary] {errorMessage}");
                                if (errorMessage.Contains("No linked"))
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.LegendaryNoLinkedAccount, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft" }));
                                    ProcessStarter.StartUrl("https://www.epicgames.com/id/link/ubisoft");
                                }
                                else if (errorMessage.Contains("Failed to establish a new connection")
                                    || errorMessage.Contains("Log in failed")
                                    || errorMessage.Contains("Login failed")
                                    || errorMessage.Contains("No saved credentials"))
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft", ["reason"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }));
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft", ["reason"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                                }
                            }
                            if (warningDisplayed)
                            {
                                var warningMessage = warningBuffer.ToString();
                                logger.Warn($"[Legendary] {warningMessage}");
                                if (!successDisplayed && !errorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure, new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft", ["reason"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
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
                playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteClientNotInstalledError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)"EA App" }));
                return;
            }
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = $"{LocalizationManager.Instance.GetString(LOC.LegendaryActivateGames)} (EA)";
            window.Content = new LegendaryEaActivate();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void GamesUpdatesCBo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedValue = (KeyValuePair<UpdatePolicy, string>)GamesUpdatesCBo.SelectedItem;
            if (selectedValue.Key == UpdatePolicy.Never)
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
            GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false) { IsIndeterminate = true };
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(LocalizationManager.Instance.GetString(LOC.CommonOverlay, new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS"}), "eos-overlay");
            }, updateCheckProgressOptions);
            if (gamesToUpdate.Count > 0)
            {
                Window window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMaximizeButton = false,
                });
                window.DataContext = gamesToUpdate;
                window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                window.Content = new LegendaryUpdater();
                window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.MinWidth = 600;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }
            else
            {
                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), LocalizationManager.Instance.GetString(LOC.CommonOverlay, new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS"}));
            }

        }

        private void ImportUbisoftLauncherGamesChk_Click(object sender, RoutedEventArgs e)
        {
            if (ImportUbisoftLauncherGamesChk.IsChecked == true)
            {
                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.LegendaryThirdPartyLauncherImportWarn, new Dictionary<string, IFluentType> { ["thirdPartyLauncherName"] = (FluentString)"Ubisoft Connect" }), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ImportEALauncherGamesChk_Click(object sender, RoutedEventArgs e)
        {
            if (ImportEALauncherGamesChk.IsChecked == true)
            {
                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.LegendaryThirdPartyLauncherImportWarn, new Dictionary<string, IFluentType> { ["thirdPartyLauncherName"] = (FluentString)"EA App" }), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoginAlternativeBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = LocalizationManager.Instance.GetString(LOC.LegendaryAuthenticateAlternativeLabel);
            window.Content = new LegendaryAlternativeAuthView();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            if (result == true)
            {
                UpdateAuthStatus();
            }
        }

        private void MigrateRevertBtn_Click(object sender, RoutedEventArgs e)
        {
            var commonFluentArgs = new Dictionary<string, IFluentType>
            {
                { "pluginShortName", (FluentString)"Epic" },
                { "originalPluginShortName", (FluentString)"Legendary" },
            };
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationConfirm, commonFluentArgs), LocalizationManager.Instance.GetString(LOC.CommonRevertMigrateGames), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonRevertMigratingGames), false) { IsIndeterminate = false };
            playniteAPI.Dialogs.ActivateGlobalProgress((a) =>
            {
                using (playniteAPI.Database.BufferedUpdate())
                {
                    var gamesToMigrate = playniteAPI.Database.Games.Where(i => i.PluginId == LegendaryLibrary.Instance.Id).ToList();
                    var migratedGames = new List<string>();
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
                                game.PluginId = Guid.Parse("00000002-DBD1-46C6-B5D0-B1BA559D10E4");
                                playniteAPI.Database.Games.Update(game);
                                migratedGames.Add(game.GameId);
                                a.CurrentProgressValue = iterator;
                            }
                        }
                        a.CurrentProgressValue = gamesToMigrate.Count() + 1;
                        if (migratedGames.Count > 0)
                        {
                            playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationCompleted), LocalizationManager.Instance.GetString(LOC.CommonRevertMigrateGames), MessageBoxButton.OK, MessageBoxImage.Information);
                            logger.Info($"Successfully migrated {migratedGames.Count} game(s) from Legendary to Epic.");
                        }
                        if (migratedGames.Count == 0)
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                        }
                    }
                    else
                    {
                        a.ProgressMaxValue = 1;
                        a.CurrentProgressValue = 1;
                        playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                    }
                }
            }, globalProgressOptions);
        }
    }
}
