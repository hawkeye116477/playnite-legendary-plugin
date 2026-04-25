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
using Playnite;
using Playnite.WebViews;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryLibrarySettingsView.xaml
    /// </summary>
    public partial class LegendaryLibrarySettingsView : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
        private LegendaryTroubleshootingInformation troubleshootingInformation;

        public LegendaryLibrarySettingsView()
        {
            InitializeComponent();
            UpdateAuthStatus();
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
        }

        private async void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var fileTypes = new Dictionary<string, string[]>
            {
                { LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExecutableTitle), ["legendary*.exe"] },
            };
            var result = await playniteApi.Dialogs.SelectFileAsync(fileTypes);
            if (result is { Count: > 0 })
            {
                SelectedLauncherPathTxt.Text = result[0];
            }
        }

        private async void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await playniteApi.Dialogs.SelectFolderAsync();
            if (result is { Count: > 0 })
            {
                SelectedGamePathTxt.Text = result[0];
            }
        }

        private async void EOSOUninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm,
                    new Dictionary<string, IFluentType>
                    {
                        ["gameTitle"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                            new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" })
                    }), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
            if (result == Playnite.MessageBoxResult.Yes)
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithValidation(CommandResultValidation.None)
                                   .WithArguments(["-y", "eos-overlay", "remove"])
                                   .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                                   .AddCommandToLog()
                                   .ExecuteBufferedAsync();
                if (cmd.StandardError.Contains("Done"))
                {
                    EOSOInstallBtn.Visibility = Visibility.Visible;
                    EOSOUninstallBtn.Visibility = Visibility.Collapsed;
                    EOSOToggleBtn.Visibility = Visibility.Collapsed;
                    EOSOCheckForUpdatesBtn.Visibility = Visibility.Collapsed;
                    await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(
                        LOC.CommonUninstallSuccess,
                        new Dictionary<string, IFluentType>
                        {
                            ["appName"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                                new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" })
                        }));
                }
            }
        }

        private void EOSOInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" });
            var installProperties = new DownloadProperties { DownloadAction = DownloadAction.Install };
            var installData = new DownloadManagerData.Download
            {
                Name = LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                    new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" }),
                GameId = "eos-overlay", DownloadProperties = installProperties
            };
            var installDataList = new List<DownloadManagerData.Download>
            {
                installData
            };
            window.DataContext = installDataList;
            window.Content = new LegendaryGameInstaller();
            window.Owner = playniteApi.GetLastActiveWindow();
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
                     .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
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
            var isEpicPluginInstalled = playniteApi.Addons.GetPlugin("Crow.EpicGames") != null;
            if (isEpicPluginInstalled)
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

            var updatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                {
                    UpdatePolicy.PlayniteLaunch,
                    LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup)
                },
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
                {
                    UpdatePolicy.PlayniteLaunch,
                    LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup)
                },
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
                {
                    ClearCacheTime.Never,
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsPlaytimeImportModeNever)
                }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;

            var launcherUpdateSourceFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "LauncherUpdateSource.json");
            List<string> repoList = new List<string>();
            if (File.Exists(launcherUpdateSourceFile))
            {
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
                CheckForLauncherUpdatesBtn.IsEnabled = false;
                OpenLauncherBinaryBtn.IsEnabled = false;
            }

            PlayniteVersionTxt.Text = LegendaryTroubleshootingInformation.PlayniteVersion;
            PluginVersionTxt.Text = LegendaryTroubleshootingInformation.PluginVersion;
            GamesInstallationPathTxt.Text = troubleshootingInformation.GamesInstallationPath;
            LogFilesPathTxt.Text = playniteApi.AppInfo.ConfigurationDirectory;
            ReportBugHyp.NavigateUri = new Uri(
                $"https://github.com/hawkeye116477/playnite-legendary-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&legendaryV={LegendaryTroubleshootingInformation.PluginVersion}&playniteV={LegendaryTroubleshootingInformation.PlayniteVersion}&launcherV={troubleshootingInformation.LauncherVersion}");

            // if (playniteApi.ApplicationSettings.PlaytimeImportMode == PlaytimeImportMode.Never)
            // {
            //     SyncPlaytimeChk.IsEnabled = false;
            // }
        }

        private async void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonClearCacheConfirm),
                LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsClearCacheTitle),
                MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
            if (result == Playnite.MessageBoxResult.Yes)
            {
                LegendaryLauncher.ClearCache();
            }
        }

        private async void SyncGameSavesChk_Click(object sender, RoutedEventArgs e)
        {
            if (SyncGameSavesChk.IsChecked == true)
            {
                await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonSyncGameSavesWarn), "", MessageBoxButtons.OK,
                    MessageBoxSeverity.Warning);
            }
        }

        private async void MigrateEpicBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsInstalled)
            {
                await LegendaryLauncher.ShowNotInstalledError();
                return;
            }

            var clientApi = new EpicAccountClient(playniteApi);
            var userLoggedIn = await clientApi.GetIsUserLoggedIn();
            if (!userLoggedIn)
            {
                await playniteApi.Dialogs.ShowErrorMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicNotLoggedInError));
                return;
            }

            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonMigrationConfirm),
                LocalizationManager.Instance.GetString(LOC.CommonMigrateGamesOriginal), MessageBoxButtons.YesNo,
                MessageBoxSeverity.Question);
            if (result == Playnite.MessageBoxResult.No)
            {
                return;
            }

            GlobalProgressOptions globalProgressOptions =
                new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMigratingGamesOriginal),
                    false) { IsIndeterminate = false };
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async (a) =>
            {
                var gamesToMigrate = playniteApi.Library.Games
                                                .Where(i => i.LibraryId == "Crow.EpicGames")
                                                .ToList();
                var migratedGames = new List<string>();
                var notImportedGames = new List<string>();
                if (gamesToMigrate.Count > 0)
                {
                    var iterator = 0;
                    a.SetProgressMaxValue(gamesToMigrate.Count + 1);
                    a.SetCrrentProgressValue(0);
                    foreach (var game in gamesToMigrate.ToList())
                    {
                        iterator++;
                        var alreadyExists = playniteApi.Library.Games.FirstOrDefault(i =>
                            i.LibraryGameId == game.LibraryGameId && i.LibraryId == LegendaryLibrary.PluginId);
                        if (alreadyExists == null)
                        {
                            game.LibraryId = LegendaryLibrary.PluginId;
                            if (game.InstallState == InstallState.Installed)
                            {
                                var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                         .WithArguments([
                                                              "-y", "import", game.LibraryGameId, game.InstallDirectory
                                                          ])
                                                         .WithEnvironmentVariables(await LegendaryLauncher
                                                             .GetDefaultEnvironmentVariables())
                                                         .AddCommandToLog()
                                                         .WithValidation(CommandResultValidation.None)
                                                         .ExecuteBufferedAsync();
                                if (!importCmd.StandardError.Contains("has been imported"))
                                {
                                    notImportedGames.Add(game.LibraryGameId);
                                    game.InstallState = InstallState.Uninstalled;
                                    logger.Debug("[Legendary] " + importCmd.StandardError);
                                    logger.Error("[Legendary] exit code: " + importCmd.ExitCode);
                                }
                            }

                            await playniteApi.Library.Games.UpdateAsync(game);
                            migratedGames.Add(game.LibraryGameId);
                            a.SetCrrentProgressValue(iterator);
                        }
                    }

                    a.SetCrrentProgressValue(gamesToMigrate.Count() + 1);
                    if (migratedGames.Count > 0)
                    {
                        await playniteApi.Dialogs.ShowMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonMigrationCompleted),
                            LocalizationManager.Instance.GetString(LOC.CommonMigrateGamesOriginal),
                            MessageBoxButtons.OK, MessageBoxSeverity.Information);
                        logger.Info("Successfully migrated " + migratedGames.Count +
                                    " game(s) from Epic to Legendary.");
                    }

                    if (notImportedGames.Count > 0)
                    {
                        logger.Info(notImportedGames.Count +
                                    " game(s) probably needs to be imported or installed again.");
                    }

                    if (migratedGames.Count == 0 && notImportedGames.Count == 0)
                    {
                        await playniteApi.Dialogs.ShowErrorMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                    }
                }
                else
                {
                    a.SetProgressMaxValue(1);
                    a.SetCrrentProgressValue(1);
                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                }
            });
        }

        private void CopyRawDataBtn_Click(object sender, RoutedEventArgs e)
        {
            var troubleshootingJSON = Serialization.ToJson(troubleshootingInformation, true);
            Clipboard.SetText(troubleshootingJSON);
        }

        private async void OpenGamesInstallationPathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(troubleshootingInformation.GamesInstallationPath))
            {
                ProcessStarter.StartProcess("explorer.exe", troubleshootingInformation.GamesInstallationPath);
            }
            else
            {
                await playniteApi.Dialogs.ShowErrorMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonPathNotExistsError));
            }
        }

        private void OpenLauncherBinaryBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryLauncher.StartClient();
        }

        private async void CheckForLauncherUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            await LegendaryLauncher.CheckForUpdates();
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var clientApi = new EpicAccountClient(playniteApi);
            var userLoggedIn = LoginBtn.IsChecked;
            if (!userLoggedIn == false)
            {
                try
                {
                    await clientApi.Login();
                }
                catch (Exception ex) when (!Debugger.IsAttached)
                {
                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                }

                UpdateAuthStatus();
            }
            else
            {
                var answer = await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonSignOutConfirm),
                    LocalizationManager.Instance.GetString(LOC.CommonSignOut), MessageBoxButtons.YesNo);
                if (answer == Playnite.MessageBoxResult.Yes)
                {
                    FileSystem.DeleteFileSafe(LegendaryLauncher.EncryptedTokensPath);
                    if (LegendaryLauncher.IsInstalled)
                    {
                        var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                              .WithArguments(new[] { "auth", "--delete" })
                                              .WithEnvironmentVariables(
                                                   await LegendaryLauncher.GetDefaultEnvironmentVariables())
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

                    using (var view = playniteApi.WebView.CreateView(new WebViewSettings
                           {
                               WindowWidth = 580,
                               WindowHeight = 700,
                           }))
                    {
                        await view.DeleteDomainCookiesAsync(".epicgames.com");
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
            var clientApi = new EpicAccountClient(playniteApi);
            var userLoggedIn = await clientApi.GetIsUserLoggedIn();
            if (userLoggedIn)
            {
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSignedInAs,
                    new Dictionary<string, IFluentType> { ["userName"] = (FluentString)clientApi.GetUsername() });
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

            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.LegendaryContinueActivation,
                    new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"Ubisoft" }), "",
                MessageBoxButtons.YesNo);
            if (result == Playnite.MessageBoxResult.Yes)
            {
                await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                         .WithArguments(new[] { "list", "-T", "--force-refresh" })
                         .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                         .AddCommandToLog()
                         .WithValidation(CommandResultValidation.None)
                         .ExecuteAsync();

                bool warningDisplayed = false;
                bool errorDisplayed = false;
                bool successDisplayed = false;
                var errorBuffer = new StringBuilder();
                var warningBuffer = new StringBuilder();
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                             .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
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
                            var activatedTitlesMatch = Regex.Match(stdErr.Text,
                                @"(\d+) titles have already been activated on your Ubisoft account");
                            if (activatedTitlesMatch.Length >= 1)
                            {
                                successDisplayed = true;
                                await playniteApi.Dialogs.ShowMessageAsync(
                                    LocalizationManager.Instance.GetString(LOC.LegendaryAllActivatedUbisoft,
                                        new Dictionary<string, IFluentType>
                                            { ["companyAccount"] = (FluentString)"Ubisoft" }));
                            }

                            if (stdErr.Text.Contains("Redeemed all"))
                            {
                                successDisplayed = true;
                                await playniteApi.Dialogs.ShowMessageAsync(
                                    LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateSuccess,
                                        new Dictionary<string, IFluentType>
                                            { ["companyAccount"] = (FluentString)"Ubisoft" }));
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
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                                        LocalizationManager.Instance.GetString(LOC.LegendaryNoLinkedAccount,
                                            new Dictionary<string, IFluentType>
                                                { ["companyAccount"] = (FluentString)"Ubisoft" }));
                                    ProcessStarter.StartUrl("https://www.epicgames.com/id/link/ubisoft");
                                }
                                else if (errorMessage.Contains("Failed to establish a new connection")
                                         || errorMessage.Contains("Log in failed")
                                         || errorMessage.Contains("Login failed")
                                         || errorMessage.Contains("No saved credentials"))
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                                        LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure,
                                            new Dictionary<string, IFluentType>
                                            {
                                                ["companyAccount"] = (FluentString)"Ubisoft",
                                                ["reason"] =
                                                    (FluentString)LocalizationManager.Instance.GetString(
                                                        LOC.ThirdPartyPlayniteLoginRequired)
                                            }));
                                }
                                else
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                                        LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure,
                                            new Dictionary<string, IFluentType>
                                            {
                                                ["companyAccount"] = (FluentString)"Ubisoft",
                                                ["reason"] =
                                                    (FluentString)LocalizationManager.Instance.GetString(
                                                        LOC.CommonCheckLog)
                                            }));
                                }
                            }

                            if (warningDisplayed)
                            {
                                var warningMessage = warningBuffer.ToString();
                                logger.Warn($"[Legendary] {warningMessage}");
                                if (!successDisplayed && !errorDisplayed)
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                                        LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure,
                                            new Dictionary<string, IFluentType>
                                            {
                                                ["companyAccount"] = (FluentString)"Ubisoft",
                                                ["reason"] =
                                                    (FluentString)LocalizationManager.Instance.GetString(
                                                        LOC.CommonCheckLog)
                                            }));
                                }
                            }

                            break;
                    }
                }
            }
        }

        private async void ActivateEaBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsEaAppInstalled)
            {
                await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(
                    LOC.ThirdPartyPlayniteClientNotInstalledError,
                    new Dictionary<string, IFluentType> { ["var0"] = (FluentString)"EA App" }));
                return;
            }

            var window = playniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = $"{LocalizationManager.Instance.GetString(LOC.LegendaryActivateGames)} (EA)";
            window.Content = new LegendaryEaActivate();
            window.Owner = playniteApi.GetLastActiveWindow();
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
            ProcessStarter.StartProcess("explorer.exe", playniteApi.AppInfo.ConfigurationDirectory);
        }

        private async void EOSOCheckForUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
            var gamesToUpdate = new Dictionary<string, UpdateInfo>();
            GlobalProgressOptions updateCheckProgressOptions =
                new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false)
                    { IsIndeterminate = true };
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions,
                async (a) =>
                {
                    gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(
                        LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                            new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" }),
                        "eos-overlay");
                });
            if (gamesToUpdate.Count > 0)
            {
                Window window = playniteApi.CreateWindow(new WindowCreationOptions
                {
                    ShowMaximizeButton = false,
                });
                window.DataContext = gamesToUpdate;
                window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                window.Content = new LegendaryUpdater();
                window.Owner = playniteApi.GetLastActiveWindow();
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.MinWidth = 600;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }
            else
            {
                await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable),
                    LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                        new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" }));
            }
        }

        private async void ImportUbisoftLauncherGamesChk_Click(object sender, RoutedEventArgs e)
        {
            if (ImportUbisoftLauncherGamesChk.IsChecked == true)
            {
                await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.LegendaryThirdPartyLauncherImportWarn,
                        new Dictionary<string, IFluentType>
                            { ["thirdPartyLauncherName"] = (FluentString)"Ubisoft Connect" }), "", MessageBoxButtons.OK,
                    MessageBoxSeverity.Warning);
            }
        }

        private async void ImportEALauncherGamesChk_Click(object sender, RoutedEventArgs e)
        {
            if (ImportEALauncherGamesChk.IsChecked == true)
            {
                await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.LegendaryThirdPartyLauncherImportWarn,
                        new Dictionary<string, IFluentType> { ["thirdPartyLauncherName"] = (FluentString)"EA App" }),
                    "", MessageBoxButtons.OK, MessageBoxSeverity.Warning);
            }
        }

        private void LoginAlternativeBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = LocalizationManager.Instance.GetString(LOC.LegendaryAuthenticateAlternativeLabel);
            window.Content = new LegendaryAlternativeAuthView();
            window.Owner = playniteApi.GetLastActiveWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            if (result == true)
            {
                UpdateAuthStatus();
            }
        }

        private async void MigrateRevertBtn_Click(object sender, RoutedEventArgs e)
        {
            var commonFluentArgs = new Dictionary<string, IFluentType>
            {
                { "pluginShortName", (FluentString)"Epic" },
                { "originalPluginShortName", (FluentString)"Legendary" },
            };
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonMigrationConfirm, commonFluentArgs),
                LocalizationManager.Instance.GetString(LOC.CommonRevertMigrateGames), MessageBoxButtons.YesNo,
                MessageBoxSeverity.Question);
            if (result == Playnite.MessageBoxResult.No)
            {
                return;
            }

            GlobalProgressOptions globalProgressOptions =
                new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonRevertMigratingGames), false)
                    { IsIndeterminate = false };
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async (a) =>
            {
                var gamesToMigrate = playniteApi.Library.Games.Where(i => i.LibraryId == LegendaryLibrary.PluginId)
                                                .ToList();
                var migratedGames = new List<string>();
                if (gamesToMigrate.Count > 0)
                {
                    var iterator = 0;
                    a.SetProgressMaxValue(gamesToMigrate.Count() + 1);
                    a.SetCrrentProgressValue(0);
                    foreach (var game in gamesToMigrate.ToList())
                    {
                        iterator++;
                        var alreadyExists = playniteApi.Library.Games.FirstOrDefault(i =>
                            i.LibraryGameId == game.LibraryGameId && i.LibraryId == LegendaryLibrary.PluginId);
                        if (alreadyExists == null)
                        {
                            game.LibraryId = "Crow.EpicGames";
                            await playniteApi.Library.Games.UpdateAsync(game);
                            migratedGames.Add(game.LibraryGameId);
                            a.SetCrrentProgressValue(iterator);
                        }
                    }

                    a.SetCrrentProgressValue(gamesToMigrate.Count + 1);
                    if (migratedGames.Count > 0)
                    {
                        await playniteApi.Dialogs.ShowMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonMigrationCompleted),
                            LocalizationManager.Instance.GetString(LOC.CommonRevertMigrateGames),
                            MessageBoxButtons.OK, MessageBoxSeverity.Information);
                        logger.Info($"Successfully migrated {migratedGames.Count} game(s) from Legendary to Epic.");
                    }

                    if (migratedGames.Count == 0)
                    {
                        await playniteApi.Dialogs.ShowErrorMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                    }
                }
                else
                {
                    a.SetProgressMaxValue(1);
                    a.SetCrrentProgressValue(1);
                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                }
            });
        }

        private void EpicConnectAccountChk_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAuthStatus();
        }
    }
}