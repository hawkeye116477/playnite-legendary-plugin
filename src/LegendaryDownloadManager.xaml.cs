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
using LegendaryLibraryNS.Enums;
using Playnite.SDK.Data;
using Playnite.Common;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDownloadManager.xaml
    /// </summary>
    public partial class LegendaryDownloadManager : UserControl
    {
        public CancellationTokenSource installerCTS;
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        private string fullInstallPath;
        public DownloadManagerData.Rootobject downloadManagerData;

        public LegendaryDownloadManager()
        {
            InitializeComponent();
            LoadSavedData();
            var runningAndQueuedDownloads = downloadManagerData.downloads.Where(i => i.status == (int)DownloadStatus.Running
                                                                                     || i.status == (int)DownloadStatus.Queued).ToList();
            if (runningAndQueuedDownloads.Count > 0)
            {
                foreach (var download in runningAndQueuedDownloads)
                {
                    download.status = (int)DownloadStatus.Paused;
                }
            }
            SaveData();
        }

        public RelayCommand<object> NavigateBackCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                playniteAPI.MainView.SwitchToLibraryView();
            });
        }

        public DownloadManagerData.Rootobject LoadSavedData()
        {
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "downloadManager.json");
            if (!File.Exists(dataFile))
            {
                downloadManagerData = new DownloadManagerData.Rootobject
                {
                    downloads = new ObservableCollection<DownloadManagerData.Download>()
                };
            }
            else
            {
                downloadManagerData = Serialization.FromJson<DownloadManagerData.Rootobject>(FileSystem.ReadFileAsStringSafe(dataFile));
            }
            DownloadsDG.ItemsSource = downloadManagerData.downloads;
            return downloadManagerData;
        }

        public void SaveData()
        {
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "downloadManager.json");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            var strConf = Serialization.ToJson(downloadManagerData, true);
            File.WriteAllText(dataFile, strConf);
        }

        public async void DoNextJobInQueue(object _, PropertyChangedEventArgs arg)
        {
            var running = downloadManagerData.downloads.Any(item => item.status == (int)DownloadStatus.Running);
            var queuedList = downloadManagerData.downloads.Where(i => i.status == (int)DownloadStatus.Queued).ToList();
            if (!running && queuedList.Count > 0)
            {
                await Install(queuedList[0].gameID, queuedList[0].installPath, queuedList[0].downloadSize, queuedList[0].name, queuedList[0].downloadAction);
            }
            else if (!running)
            {
                DownloadPB.Value = 0;
                var downloadCompleteSettings = LegendaryLibrary.GetSettings().DoActionAfterDownloadComplete;
                switch (downloadCompleteSettings)
                {
                    case (int)DownloadCompleteAction.ShutDown:
                        Process.Start("shutdown", "/s /t 0");
                        break;
                    case (int)DownloadCompleteAction.Reboot:
                        Process.Start("shutdown", "/r /t 0");
                        break;
                    case (int)DownloadCompleteAction.Hibernate:
                        Playnite.Native.Powrprof.SetSuspendState(true, true, false);
                        break;
                    case (int)DownloadCompleteAction.Sleep:
                        Playnite.Native.Powrprof.SetSuspendState(false, true, false);
                        break;
                    default:
                        break;
                }
            }
        }

        public async Task EnqueueJob(string gameID, string installPath, string downloadSize, string installSize, string gameTitle, int downloadAction)
        {
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameID);
            if (wantedItem == null)
            {
                DateTimeOffset now = DateTime.UtcNow;
                downloadManagerData.downloads.Add(new DownloadManagerData.Download
                { gameID = gameID, installPath = installPath, downloadSize = downloadSize, installSize = installSize, name = gameTitle, status = (int)DownloadStatus.Queued, addedTime = now.ToUnixTimeSeconds(), downloadAction = downloadAction });
                SaveData();
            }
            else
            {
                wantedItem.status = (int)DownloadStatus.Queued;
                SaveData();
            }
            var running = downloadManagerData.downloads.Any(item => item.status == (int)DownloadStatus.Running);
            if (!running)
            {
                await Install(gameID, installPath, downloadSize, gameTitle, downloadAction);
            }
            foreach (DownloadManagerData.Download download in downloadManagerData.downloads)
            {
                download.PropertyChanged -= DoNextJobInQueue;
                download.PropertyChanged += DoNextJobInQueue;
            }
        }

        public async Task Install(string gameID, string installPath, string downloadSize, string gameTitle, int downloadAction)
        {
            var installCommand = new List<string>() { "-y", "install", gameID, "--base-path", installPath };
            var settings = LegendaryLibrary.GetSettings();
            if (settings.PreferredCDN != "")
            {
                installCommand.AddRange(new[] { "--preferred-cdn", settings.PreferredCDN });
            }
            if (settings.NoHttps)
            {
                installCommand.Add("--no-https");
            }
            if (settings.MaxWorkers != 0)
            {
                installCommand.AddRange(new[] { "--max-workers", settings.MaxWorkers.ToString() });
            }
            if (settings.MaxSharedMemory != 0)
            {
                installCommand.AddRange(new[] { "--max-shared-memory", settings.MaxSharedMemory.ToString() });
            }
            if (downloadAction == (int)DownloadAction.Repair)
            {
                installCommand.Add("--repair");
            }
            if (gameID == "eos-overlay")
            {
                installCommand = new List<string>() { "-y", "eos-overlay", "install", "--path", installPath };
            }
            installerCTS = new CancellationTokenSource();
            try
            {
                var stdOutBuffer = new StringBuilder();
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath).WithArguments(installCommand);
                var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameID);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync(installerCTS.Token))
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            wantedItem.status = (int)DownloadStatus.Running;
                            DownloadPB.Value = 0;
                            EtaTB.Text = "";
                            ElapsedTB.Text = "";
                            DownloadedTB.Text = "";
                            DownloadSpeedTB.Text = "";
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
                                wantedItem.status = (int)DownloadStatus.Completed;
                                DateTimeOffset now = DateTime.UtcNow;
                                wantedItem.completedTime = now.ToUnixTimeSeconds();
                                SaveData();
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

                                                var game = playniteAPI.Database.Games.FirstOrDefault(
                                                    item => item.PluginId == LegendaryLibrary.Instance.Id && item.GameId == gameID);
                                                game.InstallDirectory = app.Value.Install_path;
                                                game.Version = app.Value.Version;
                                                game.IsInstalled = true;
                                                playniteAPI.Database.Games.Update(game);
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

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    if (selectedRow.status == (int)DownloadStatus.Running ||
                        selectedRow.status == (int)DownloadStatus.Queued)
                    {
                        if (selectedRow.status == (int)DownloadStatus.Running)
                        {
                            installerCTS?.Cancel();
                            installerCTS?.Dispose();
                            EtaTB.Text = ResourceProvider.GetString(LOC.LegendaryDownloadPaused);
                        }
                        selectedRow.status = (int)DownloadStatus.Paused;
                        SaveData();
                    }
                }
            }
        }

        private async void ResumeDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    if (selectedRow.status == (int)DownloadStatus.Canceled ||
                        selectedRow.status == (int)DownloadStatus.Paused)
                    {
                        await EnqueueJob(selectedRow.gameID, selectedRow.installPath, selectedRow.downloadSize, selectedRow.installSize, selectedRow.name, selectedRow.downloadAction);
                    }
                }
            }
        }

        private void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    if (selectedRow.status == (int)DownloadStatus.Running ||
                        selectedRow.status == (int)DownloadStatus.Queued ||
                        selectedRow.status == (int)DownloadStatus.Paused)
                    {
                        if (selectedRow.status == (int)DownloadStatus.Running)
                        {
                            installerCTS?.Cancel();
                            installerCTS?.Dispose();
                        }
                        var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", selectedRow.gameID + ".resume");
                        if (File.Exists(resumeFile))
                        {
                            File.Delete(resumeFile);
                        }
                        var repairFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", selectedRow.gameID + ".repair");
                        if (File.Exists(repairFile))
                        {
                            File.Delete(repairFile);
                        }
                        if (fullInstallPath != null)
                        {
                            if (Directory.Exists(fullInstallPath))
                            {
                                Directory.Delete(fullInstallPath, true);
                            }
                        }
                        selectedRow.status = (int)DownloadStatus.Canceled;
                        SaveData();
                    }
                }
            }
        }

        private void RemoveDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    var result = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryRemoveEntryConfirm), selectedRow.name), ResourceProvider.GetString(LOC.LegendaryRemoveEntry), MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == selectedRow.gameID);
                        wantedItem.PropertyChanged -= DoNextJobInQueue;
                        if (wantedItem.status != (int)DownloadStatus.Completed &&
                            wantedItem.status != (int)DownloadStatus.Canceled)
                        {
                            if (wantedItem.status == (int)DownloadStatus.Running)
                            {
                                installerCTS?.Cancel();
                                installerCTS?.Dispose();
                            }
                            selectedRow.status = (int)DownloadStatus.Canceled;
                        }
                        var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", selectedRow.gameID + ".resume");
                        if (File.Exists(resumeFile))
                        {
                            File.Delete(resumeFile);
                        }
                        var repairFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", selectedRow.gameID + ".repair");
                        if (File.Exists(repairFile))
                        {
                            File.Delete(repairFile);
                        }
                        if (fullInstallPath != null)
                        {
                            if (Directory.Exists(fullInstallPath))
                            {
                                Directory.Delete(fullInstallPath, true);
                            }
                        }
                        downloadManagerData.downloads.Remove(selectedRow);
                        SaveData();
                    }
                }
            }
        }

        private void FilterDownloadBtn_Checked(object sender, RoutedEventArgs e)
        {
            FilterPop.IsOpen = true;
        }

        private void FilterDownloadBtn_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterPop.IsOpen = false;
        }

        private void DownloadFiltersChk_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            ICollectionView downloadsView = CollectionViewSource.GetDefaultView(downloadManagerData.downloads);
            var checkedStatus = new List<int>();
            foreach (CheckBox checkBox in FilterStatusSP.Children)
            {
                var downloadStatus = (int)Enum.Parse(typeof(DownloadStatus), checkBox.Name.Replace("Chk", ""));
                if (checkBox.IsChecked == true)
                {
                    checkedStatus.Add(downloadStatus);
                }
                else
                {
                    checkedStatus.Remove(downloadStatus);
                }
            }
            if (checkedStatus.Count > 0)
            {
                downloadsView.Filter = item => checkedStatus.Contains((item as DownloadManagerData.Download).status);
            }
            else
            {
                downloadsView.Filter = null;
            }
        }

    }
}
