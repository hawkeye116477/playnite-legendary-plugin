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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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
        private bool downloadPaused;
        private string installCommand;

        public LegendaryGameInstaller()
        {
            InitializeComponent();
        }

        public Window InstallerWindow => Window.GetWindow(this);

        public string GameID => DataContext.ToString();

        private void ChooseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxtBox.Text = path;
                UpdateSpaceInfo(path);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = LegendaryLibrary.GetSettings();
            var installPath = settings.GamesInstallationPath;
            if (SelectedGamePathTxtBox.Text != "")
            {
                installPath = SelectedGamePathTxtBox.Text;
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
                installPath = System.IO.Path.Combine(SelectedGamePathTxtBox.Text, ".overlay");
                installCommand = "-y eos-overlay install --path " + installPath;
            }
            InstallerPage.Visibility = Visibility.Collapsed;
            DownloaderPage.Visibility = Visibility.Visible;
            await Install(installCommand);
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

        private void ImportButton_Click(object sender, RoutedEventArgs e)
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
            SelectedGamePathTxtBox.Text = LegendaryLibrary.GetSettings().GamesInstallationPath;
            UpdateSpaceInfo(SelectedGamePathTxtBox.Text);
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
                    downloadSize.Content = FormatSize(manifest.Manifest.Download_size);
                    installSize.Content = FormatSize(manifest.Manifest.Disk_size);
                }
            }
            else
            {
                importButton.IsEnabled = false;
                importButton.Visibility = Visibility.Collapsed;

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
                        downloadSize.Content = FormatSize(downloadSizeValue);
                    }
                    if (line.Contains("Install size:"))
                    {
                        var installSizeValue = double.Parse(line.Substring(line.IndexOf("Install size:") + 14).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                        installSize.Content = FormatSize(installSizeValue);
                    }
                }
            }
            installButton.IsEnabled = true;
        }

        private void UpdateSpaceInfo(string path)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                spaceLabel.Content = FormatSize(dDrive.AvailableFreeSpace);
            }
        }

        private async void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!downloadPaused)
            {
                installerCTS.Cancel();
                installerCTS.Dispose();
                PauseButton.Content = ResourceProvider.GetString(LOC.LegendaryResumeDownload);
                ETALabel.Content = ResourceProvider.GetString(LOC.LegendaryDownloadPaused);
            }
            else
            {
                PauseButton.Content = ResourceProvider.GetString(LOC.LegendaryPauseDownload);
                await Install(installCommand);
            }
        }

        public async Task Install(string installComand)
        {
            downloadPaused = false;
            installerCTS = new CancellationTokenSource();
            try
            {
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
                                DProgressBar.Value = progress;
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                ETALabel.Content = ETAMatch.Groups[1].Value;
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                ElapsedLabel.Content = elapsedMatch.Groups[1].Value;
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+.) MiB");
                            if (downloadedMatch.Length >= 2)
                            {
                                var downloaded = FormatSize(double.Parse(downloadedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadedLabel.Content = downloaded + " / " + downloadSize.Content;
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+.) MiB");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                var downloadSpeed = FormatSize(double.Parse(downloadSpeedMatch.Groups[1].Value, CultureInfo.InvariantCulture) * 1024 * 1024);
                                DownloadSpeedLabel.Content = downloadSpeed + "/s";
                            }
                            logger.Debug("[Legendary]: " + stdErr);
                            break;
                        case ExitedCommandEvent exited:
                            if (exited.ExitCode == 0)
                            {
                                SystemSounds.Hand.Play();
                                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryInstallationFinished), InstallerWindow.Title);
                                InstallerWindow.DialogResult = true;
                            }
                            else if (exited.ExitCode != 0)
                            {
                                logger.Error("[Legendary] exit code: " + exited.ExitCode);
                            }
                            InstallerWindow.Close();
                            installerCTS.Dispose();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Command was canceled
                downloadPaused = true;
            }
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (ETALabel.Content.ToString() != ResourceProvider.GetString(LOC.LegendaryDownloadPaused))
            {
                installerCTS.Cancel();
                installerCTS.Dispose();
            }
            var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", GameID + ".resume");
            if (File.Exists(resumeFile))
            {
                File.Delete(resumeFile);
            }
            InstallerWindow.Close();
        }
    }
}
