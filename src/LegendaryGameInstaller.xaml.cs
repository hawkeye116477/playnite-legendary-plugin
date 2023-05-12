using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using LegendaryLibraryNS.Enums;
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
        public string installCommand;
        public string downloadSize;
        public string installSize;

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
            if (GameID == "eos-overlay")
            {
                installPath = Path.Combine(SelectedGamePathTxt.Text, ".overlay");
            }
            playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryDownloadManagerWhatsUp));
            InstallerWindow.Close();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            await downloadManager.EnqueueJob(GameID, installPath, downloadSize, installSize, InstallerWindow.Title, (int)DownloadAction.Install);
        }

        private async void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                InstallerPage.Visibility = Visibility.Collapsed;
                LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
                downloadManager.CancelDownloadBtn.Visibility = Visibility.Collapsed;
                downloadManager.PauseBtn.Visibility = Visibility.Collapsed;
                InstallerWindow.Close();
                var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithArguments(new[] { "-y", "import", GameID, path })
                                         .WithValidation(CommandResultValidation.None)
                                         .ExecuteBufferedAsync();
                if (importCmd.StandardError.Contains("has been imported"))
                {
                    await downloadManager.EnqueueJob(GameID, path, downloadSize, installSize, InstallerWindow.Title, (int)DownloadAction.Repair);
                }
                else
                {
                    logger.Debug("[Legendary] " + importCmd.StandardError);
                    logger.Error("[Legendary] exit code: " + importCmd.ExitCode);
                }
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
                    logger.Error("[Legendary]" + result.StandardError);
                    if (result.StandardError.Contains("Log in failed"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString("LOCGameInstallError"), ResourceProvider.GetString("LOCLoginRequired")));
                    } else
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString("LOCGameInstallError"), result.StandardError));
                    }
                    Window.GetWindow(this).Close();
                }
                else
                {
                    var manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>(result.StandardOutput);
                    downloadSize = Helpers.FormatSize(manifest.Manifest.Download_size);
                    DownloadSizeTB.Text = downloadSize;
                    installSize = Helpers.FormatSize(manifest.Manifest.Disk_size);
                    InstallSizeTB.Text = installSize;
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
                        downloadSize = Helpers.FormatSize(downloadSizeValue);
                        DownloadSizeTB.Text = downloadSize;
                    }
                    if (line.Contains("Install size:"))
                    {
                        var installSizeValue = double.Parse(line.Substring(line.IndexOf("Install size:") + 14).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                        installSize = Helpers.FormatSize(installSizeValue);
                        InstallSizeTB.Text = installSize;
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
                SpaceTB.Text = Helpers.FormatSize(dDrive.AvailableFreeSpace);
            }
        }
    }
}
