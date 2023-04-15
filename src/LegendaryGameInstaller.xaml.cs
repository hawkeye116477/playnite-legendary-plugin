using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
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

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameInstaller.xaml
    /// </summary>
    public partial class LegendaryGameInstaller : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        private CancellationTokenSource installerCTS;
        private string installCommand;
        private string fullInstallPath;

        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        public LegendaryGameInstaller()
        {
            InitializeComponent();
        }

        public Window InstallerWindow => Window.GetWindow(this);

        public string GameID => DataContext.ToString();

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
                UpdateSpaceInfo(path);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = LegendaryLibrary.GetSettings();
            var installPath = settings.GamesInstallationPath;
            if (SelectedGamePathTxt.Text != "")
            {
                installPath = SelectedGamePathTxt.Text;
            }
            installCommand = "-y install " + GameID + " --base-path " + installPath;
            var prefferedCDN = settings.PreferredCDN;
            if (prefferedCDN != "")
            {
                installCommand += " --preferred-cdn " + prefferedCDN;
            }
            if (settings.NoHttps)
            {
                installCommand += " --no-https";
            }
            if (GameID == "eos-overlay")
            {
                installPath = Path.Combine(SelectedGamePathTxt.Text, ".overlay");
                installCommand = "-y eos-overlay install --path " + installPath;
            }
            InstallerPage.Visibility = Visibility.Collapsed;
            DownloaderPage.Visibility = Visibility.Visible;
            await Install();
        }

        static readonly string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        public static string FormatSize(double size)
        {
            int i = 0;
            decimal number = (decimal)size;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                i++;
            }
            return string.Format("{0:n2} {1}", number, suffixes[i]);
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y import " + GameID + " " + path, null, false);
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y repair " + GameID, null, false);
                InstallerWindow.DialogResult = true;
                InstallerWindow.Close();
            }
        }

        private async void LegendaryGameInstallerUC_Loaded(object sender, RoutedEventArgs e)
        {
            SelectedGamePathTxt.Text = LegendaryLibrary.GetSettings().GamesInstallationPath;
            UpdateSpaceInfo(SelectedGamePathTxt.Text);
            if (GameID != "eos-overlay")
            {
                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                      .WithArguments(new[] { "info", GameID, "--json" })
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                if (result.ExitCode != 0)
                {
                    logger.Error(result.StandardError);
                    Window.GetWindow(this).Close();
                }
                else
                {
                    var manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>(result.StandardOutput);
                    DownloadSizeTB.Text = FormatSize(manifest.Manifest.Download_size);
                    InstallSizeTB.Text = FormatSize(manifest.Manifest.Disk_size);
                }
            }
            else
            {
                ImportBtn.IsEnabled = false;
                ImportBtn.Visibility = Visibility.Collapsed;

                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                      .WithArguments(new[] { GameID, "install" })
                                      .WithStandardInputPipe(PipeSource.FromString("n"))
                                      .ExecuteBufferedAsync();
                string[] lines = result.StandardError.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (line.Contains("Download size:"))
                    {
                        var downloadSizeValue = double.Parse(line.Substring(line.IndexOf("Download size:") + 15).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                        DownloadSizeTB.Text = FormatSize(downloadSizeValue);
                    }
                    if (line.Contains("Install size:"))
                    {
                        var installSizeValue = double.Parse(line.Substring(line.IndexOf("Install size:") + 14).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                        InstallSizeTB.Text = FormatSize(installSizeValue);
                    }
                }
            }
            InstallBtn.IsEnabled = true;
        }

        private void UpdateSpaceInfo(string path)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                SpaceTB.Text = FormatSize(dDrive.AvailableFreeSpace);
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
                await Install();
            }
        }

        public async Task Install()
        {
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
                                string downloaded = FormatSize(double.Parse(downloadedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadedTB.Text = downloaded + " / " + DownloadSizeTB.Text;
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+.) MiB");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                string downloadSpeed = FormatSize(double.Parse(downloadSpeedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
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
                                Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(LegendaryLauncher.Icon), InstallerWindow.Title, ResourceProvider.GetString(LOC.LegendaryInstallationFinished), null);
                                InstallerWindow.DialogResult = true;
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
                                SetSuspendState(true, true, true);
                            }
                            else if (downloadCompleteSettings == (int)DownloadCompleteAction.Sleep)
                            {
                                SetSuspendState(false, true, true);
                            }
                            InstallerWindow.Close();
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

        private void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PauseBtn.IsChecked == false)
            {
                installerCTS?.Cancel();
                installerCTS?.Dispose();
            }
            var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", GameID + ".resume");
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
            InstallerWindow.Close();
        }
    }
}
