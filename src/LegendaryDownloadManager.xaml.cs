using CliWrap;
using CliWrap.EventStream;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using LegendaryLibraryNS.Models;
using Playnite.SDK.Plugins;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDownloadManager.xaml
    /// </summary>
    public partial class LegendaryDownloadManager : UserControl
    {
        private CancellationTokenSource installerCTS;
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        private string fullInstallPath;
        public string savedGameID;
        public string savedInstallPath;
        public string savedDownloadSize;
        public string savedGameTitle;

        public LegendaryDownloadManager()
        {
            InitializeComponent();
        }

        public RelayCommand<object> NavigateBackCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                playniteAPI.MainView.SwitchToLibraryView();
            });
        }

        public async Task Install(string gameID, string installPath, string downloadSize, string gameTitle)
        {
            savedGameID = gameID;
            savedInstallPath = installPath;
            savedDownloadSize = downloadSize;
            savedGameTitle = gameTitle;
            string installCommand = "-y install " + gameID + " --base-path " + installPath;
            var settings = LegendaryLibrary.GetSettings();
            var prefferedCDN = settings.PreferredCDN;
            if (prefferedCDN != "")
            {
                installCommand += " --preferred-cdn " + prefferedCDN;
            }
            if (settings.NoHttps)
            {
                installCommand += " --no-https";
            }
            if (settings.MaxWorkers != 0)
            {
                installCommand += " --max-workers " + settings.MaxWorkers;
            }
            if (settings.MaxSharedMemory != 0)
            {
                installCommand += " --max-shared-memory " + settings.MaxSharedMemory;
            }
            if (gameID == "eos-overlay")
            {
                installCommand = "-y eos-overlay install --path " + installPath;
            }
            installerCTS = new CancellationTokenSource();
            try
            {
                var stdOutBuffer = new StringBuilder();
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath).WithArguments(installCommand);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync(installerCTS.Token))
                {
                    switch (cmdEvent)
                    {
                        case StandardErrorCommandEvent stdErr:
                            var progressMatch = Regex.Match(stdErr.Text, @"Progress: (\d.*%)");
                            if (progressMatch.Length >= 2)
                            {
                                double progress = double.Parse(progressMatch.Groups[1].Value.Replace("%", ""), CultureInfo.InvariantCulture);
                                DownloadPB.Value = progress;
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                EtaTB.Text = ETAMatch.Groups[1].Value;
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                ElapsedTB.Text = elapsedMatch.Groups[1].Value;
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+.) MiB");
                            if (downloadedMatch.Length >= 2)
                            {
                                string downloaded = Helpers.FormatSize(double.Parse(downloadedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadedTB.Text = downloaded + " / " + downloadSize;
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+.) MiB");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                string downloadSpeed = Helpers.FormatSize(double.Parse(downloadSpeedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadSpeedTB.Text = downloadSpeed + "/s";
                            }
                            var fullInstallPathMatch = Regex.Match(stdErr.Text, @"Install path: (\S+)");
                            if (fullInstallPathMatch.Length >= 2)
                            {
                                fullInstallPath = fullInstallPathMatch.Groups[1].Value;
                            }
                            stdOutBuffer.AppendLine("[Legendary]: " + stdErr);
                            break;
                        case ExitedCommandEvent exited:
                            if (exited.ExitCode == 0)
                            {
                                _ = (Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                                {
                                    var installed = LegendaryLauncher.GetInstalledAppList();
                                    if (installed != null)
                                    {
                                        foreach (KeyValuePair<string, Installed> app in installed)
                                        {
                                            if (app.Value.App_name == gameID)
                                            {
                                                var installInfo = new GameInstallationData
                                                {
                                                    InstallDirectory = app.Value.Install_path
                                                };

                                                LegendaryInstallController.CompleteInstall(new GameInstalledEventArgs(installInfo));
                                                break;
                                            }
                                        }
                                    }
                                }));
                                Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(LegendaryLauncher.Icon), gameTitle, ResourceProvider.GetString(LOC.LegendaryInstallationFinished), null);
                            }
                            else if (exited.ExitCode != 0)
                            {
                                logger.Debug(stdOutBuffer.ToString());
                                logger.Error("[Legendary] exit code: " + exited.ExitCode);
                            }
                            var downloadCompleteSettings = LegendaryLibrary.GetSettings().DoActionAfterDownloadComplete;
                            if (downloadCompleteSettings == (int)DownloadCompleteAction.ShutDown)
                            {
                                Process.Start("shutdown", "/s /t 0");
                            }
                            else if (downloadCompleteSettings == (int)DownloadCompleteAction.Reboot)
                            {
                                Process.Start("shutdown", "/r /t 0");
                            }
                            else if (downloadCompleteSettings == (int)DownloadCompleteAction.Hibernate)
                            {
                                Playnite.Native.Powrprof.SetSuspendState(true, true, false);
                            }
                            else if (downloadCompleteSettings == (int)DownloadCompleteAction.Sleep)
                            {
                                Playnite.Native.Powrprof.SetSuspendState(false, true, false);
                            }
                            installerCTS?.Dispose();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Command was canceled
            }
        }


        private async void PauseBtn_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (PauseBtn.IsChecked == true)
            {
                installerCTS?.Cancel();
                installerCTS?.Dispose();
                PauseBtn.Content = ResourceProvider.GetString(LOC.LegendaryResumeDownload);
                EtaTB.Text = ResourceProvider.GetString(LOC.LegendaryDownloadPaused);
            }
            else
            {
                PauseBtn.Content = ResourceProvider.GetString(LOC.LegendaryPauseDownload);
                await Install(savedGameID, savedInstallPath, savedDownloadSize, savedGameTitle);
            }
        }

        private void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PauseBtn.IsChecked == false)
            {
                installerCTS?.Cancel();
                installerCTS?.Dispose();
            }
            var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", savedGameID + ".resume");
            if (File.Exists(resumeFile))
            {
                File.Delete(resumeFile);
            }
            if (fullInstallPath != null)
            {
                if (Directory.Exists(fullInstallPath))
                {
                    Directory.Delete(fullInstallPath, true);
                }
            }
        }

        public async Task Repair(string gameID, string downloadSize, string gameTitle)
        {
            installerCTS = new CancellationTokenSource();
            try
            {
                var stdOutBuffer = new StringBuilder();
                var repairCmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(new[] { "-y", "repair", gameID })
                                   .WithValidation(CommandResultValidation.None);
                await foreach (var cmdEvent in repairCmd.ListenAsync(installerCTS.Token))
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            break;
                        case StandardErrorCommandEvent stdErr:
                            var verificationProgressMatch = Regex.Match(stdErr.Text, @"Verification progress:.*\((\d.*%)");
                            var progressMatch = Regex.Match(stdErr.Text, @"Progress: (\d.*%)");
                            if (verificationProgressMatch.Length >= 2)
                            {
                                double progress = double.Parse(verificationProgressMatch.Groups[1].Value.Replace("%", ""), CultureInfo.InvariantCulture);
                                DownloadPB.Value = progress;
                            }
                            else if (progressMatch.Length >= 2)
                            {
                                double progress = double.Parse(progressMatch.Groups[1].Value.Replace("%", ""), CultureInfo.InvariantCulture);
                                DownloadPB.Value = progress;
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                EtaTB.Text = ETAMatch.Groups[1].Value;
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                ElapsedTB.Text = elapsedMatch.Groups[1].Value;
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+.) MiB");
                            if (downloadedMatch.Length >= 2)
                            {
                                string downloaded = Helpers.FormatSize(double.Parse(downloadedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadedTB.Text = downloaded + " / " + downloadSize;
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+.) MiB");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                string downloadSpeed = Helpers.FormatSize(double.Parse(downloadSpeedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadSpeedTB.Text = downloadSpeed + "/s";
                            }
                            var fullInstallPathMatch = Regex.Match(stdErr.Text, @"Install path: (\S+)");
                            if (fullInstallPathMatch.Length >= 2)
                            {
                                fullInstallPath = fullInstallPathMatch.Groups[1].Value;
                            }
                            stdOutBuffer.AppendLine("[Legendary]: " + stdErr);
                            break;
                        case ExitedCommandEvent exited:
                            if (exited.ExitCode == 0)
                            {
                                _ = (Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                                {
                                    var installed = LegendaryLauncher.GetInstalledAppList();
                                    if (installed != null)
                                    {
                                        foreach (KeyValuePair<string, Installed> app in installed)
                                        {
                                            if (app.Value.App_name == gameID)
                                            {
                                                var installInfo = new GameInstallationData
                                                {
                                                    InstallDirectory = app.Value.Install_path
                                                };

                                                LegendaryInstallController.CompleteInstall(new GameInstalledEventArgs(installInfo));
                                                break;
                                            }
                                        }
                                    }
                                }));
                                Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(LegendaryLauncher.Icon), gameTitle, ResourceProvider.GetString(LOC.LegendaryImportFinished), null);
                            }
                            else if (exited.ExitCode != 0)
                            {
                                logger.Debug(stdOutBuffer.ToString());
                                logger.Error("[Legendary] exit code: " + exited.ExitCode);
                            }
                            installerCTS?.Dispose();
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Command was canceled
            }
        }

    }
}
