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
        public CancellationTokenSource forcefulInstallerCTS;
        public CancellationTokenSource gracefulInstallerCTS;
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public DownloadManagerData.Rootobject downloadManagerData;

        public LegendaryDownloadManager()
        {
            InitializeComponent();
            SetControlTextBlockStyle();
            SelectAllBtn.ToolTip = $"{ResourceProvider.GetResource(LOC.LegendarySelectAllEntries)} (Ctrl+A)";
            LoadSavedData();
            var runningAndQueuedDownloads = downloadManagerData.downloads.Where(i => i.status == (int)DownloadStatus.Running
                                                                                     || i.status == (int)DownloadStatus.Queued).ToList();
            if (runningAndQueuedDownloads.Count > 0)
            {
                foreach (var download in runningAndQueuedDownloads)
                {
                    download.status = (int)DownloadStatus.Paused;
                }
                SaveData();
            }
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
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(dataFile), out downloadManagerData))
                {
                    if (downloadManagerData != null && downloadManagerData.downloads != null)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                downloadManagerData = new DownloadManagerData.Rootobject
                {
                    downloads = new ObservableCollection<DownloadManagerData.Download>()
                };
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
            if (!strConf.IsNullOrEmpty())
            {
                File.WriteAllText(dataFile, strConf);
            }
        }

        public async void DoNextJobInQueue(object _, PropertyChangedEventArgs arg)
        {
            var running = downloadManagerData.downloads.Any(item => item.status == (int)DownloadStatus.Running);
            var queuedList = downloadManagerData.downloads.Where(i => i.status == (int)DownloadStatus.Queued).ToList();
            if (!running && queuedList.Count > 0)
            {
                await Install(queuedList[0].gameID, queuedList[0].name, queuedList[0].downloadSize, queuedList[0].downloadProperties);
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

        public async Task EnqueueJob(string gameID, string gameTitle, string downloadSize, string installSize, DownloadProperties downloadProperties)
        {
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameID);
            if (wantedItem == null)
            {
                DateTimeOffset now = DateTime.UtcNow;
                downloadManagerData.downloads.Add(new DownloadManagerData.Download
                { gameID = gameID, downloadSize = downloadSize, installSize = installSize, name = gameTitle, status = (int)DownloadStatus.Queued, addedTime = now.ToUnixTimeSeconds(), downloadProperties = downloadProperties });
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
                await Install(gameID, gameTitle, downloadSize, downloadProperties);
            }
            foreach (DownloadManagerData.Download download in downloadManagerData.downloads)
            {
                download.PropertyChanged -= DoNextJobInQueue;
                download.PropertyChanged += DoNextJobInQueue;
            }
        }

        public async Task Install(string gameID, string gameTitle, string downloadSize, DownloadProperties downloadProperties)
        {
            var installCommand = new List<string>() { "-y", "install", gameID };
            if (downloadProperties.installPath != "")
            {
                installCommand.AddRange(new[] { "--base-path", downloadProperties.installPath });
            }
            var settings = LegendaryLibrary.GetSettings();
            if (settings.PreferredCDN != "")
            {
                installCommand.AddRange(new[] { "--preferred-cdn", settings.PreferredCDN });
            }
            if (settings.NoHttps)
            {
                installCommand.Add("--no-https");
            }
            if (downloadProperties.maxWorkers != 0)
            {
                installCommand.AddRange(new[] { "--max-workers", downloadProperties.maxWorkers.ToString() });
            }
            if (downloadProperties.maxSharedMemory != 0)
            {
                installCommand.AddRange(new[] { "--max-shared-memory", downloadProperties.maxSharedMemory.ToString() });
            }
            if (downloadProperties.enableReordering)
            {
                installCommand.Add("--enable-reordering");
            }
            if (downloadProperties.downloadAction == (int)DownloadAction.Repair)
            {
                installCommand.Add("--repair");
            }
            if (downloadProperties.downloadAction == (int)DownloadAction.Update)
            {
                installCommand.Add("--update-only");
            }
            if (downloadProperties.extraContent != null)
            {
                if (downloadProperties.extraContent.Count > 0)
                {
                    foreach (var singleSelectedContent in downloadProperties.extraContent)
                    {
                        installCommand.Add("--install-tag=" + singleSelectedContent);
                    }
                }
            }
            installCommand.Add("--skip-dlcs");
            if (gameID == "eos-overlay")
            {
                installCommand = new List<string>() { "-y", "eos-overlay", "install", "--path", Path.Combine(downloadProperties.installPath, ".overlay") };
            }
            forcefulInstallerCTS = new CancellationTokenSource();
            gracefulInstallerCTS = new CancellationTokenSource();
            try
            {
                var stdOutBuffer = new StringBuilder();
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                             .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                             .WithArguments(installCommand)
                             .WithValidation(CommandResultValidation.None);
                var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameID);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync(Console.OutputEncoding, Console.OutputEncoding, forcefulInstallerCTS.Token, gracefulInstallerCTS.Token))
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
                            var downloadSizeMatch = Regex.Match(stdErr.Text, @"Download size: (\S+) (\wiB)");
                            if (downloadSizeMatch.Length >= 2)
                            {
                                downloadSize = Helpers.FormatSize(double.Parse(downloadSizeMatch.Groups[1].Value, CultureInfo.InvariantCulture), downloadSizeMatch.Groups[2].Value);
                                wantedItem.downloadSize = downloadSize;
                            }
                            var installSizeMatch = Regex.Match(stdErr.Text, @"Install size: (\S+) (\wiB)");
                            if (installSizeMatch.Length >= 2)
                            {
                                string installSize = Helpers.FormatSize(double.Parse(installSizeMatch.Groups[1].Value, CultureInfo.InvariantCulture), installSizeMatch.Groups[2].Value);
                                wantedItem.installSize = installSize;
                            }
                            var fullInstallPathMatch = Regex.Match(stdErr.Text, @"Install path: (\S+)");
                            if (fullInstallPathMatch.Length >= 2)
                            {
                                wantedItem.fullInstallPath = fullInstallPathMatch.Groups[1].Value;
                            }
                            SaveData();
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
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+) (\wiB)");
                            if (downloadedMatch.Length >= 2)
                            {
                                string downloaded = Helpers.FormatSize(double.Parse(downloadedMatch.Groups[1].Value, CultureInfo.InvariantCulture), downloadedMatch.Groups[2].Value);
                                DownloadedTB.Text = downloaded + " / " + downloadSize;
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+) (\wiB)");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                bool inBits = false;
                                if (settings.DisplayDownloadSpeedInBits)
                                {
                                    inBits = true;
                                }
                                string downloadSpeed = Helpers.FormatSize(double.Parse(downloadSpeedMatch.Groups[1].Value, CultureInfo.InvariantCulture), downloadSpeedMatch.Groups[2].Value, inBits);
                                DownloadSpeedTB.Text = downloadSpeed + "/s";
                            }
                            stdOutBuffer.AppendLine(stdErr.Text);
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
                                    var installedAppList = LegendaryLauncher.GetInstalledAppList();
                                    if (installedAppList != null)
                                    {
                                        if (installedAppList.ContainsKey(gameID))
                                        {
                                            var installedGameInfo = installedAppList[gameID];
                                            if (installedGameInfo.Is_dlc == false)
                                            {
                                                var game = playniteAPI.Database.Games.FirstOrDefault(item => item.PluginId == LegendaryLibrary.Instance.Id && item.GameId == gameID);
                                                game.InstallDirectory = installedGameInfo.Install_path;
                                                game.Version = installedGameInfo.Version;
                                                game.InstallSize = (ulong?)installedGameInfo.Install_size;
                                                game.IsInstalled = true;
                                                playniteAPI.Database.Games.Update(game);
                                            }
                                        }
                                    }
                                }));
                                Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(LegendaryLauncher.Icon), gameTitle, ResourceProvider.GetString(LOC.LegendaryInstallationFinished), null);
                            }
                            else
                            {
                                wantedItem.status = (int)DownloadStatus.Paused;
                                SaveData();
                                var memoryErrorMatch = Regex.Match(stdOutBuffer.ToString(), @"MemoryError: Current shared memory cache is smaller than required: (\S+) MiB < (\S+) MiB");
                                if (memoryErrorMatch.Length >= 2)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), string.Format(ResourceProvider.GetString(LOC.LegendaryMemoryError), memoryErrorMatch.Groups[1] + " MB", memoryErrorMatch.Groups[2] + " MB")));
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                                }
                                logger.Error("[Legendary]: " + stdOutBuffer.ToString());
                                logger.Error("[Legendary] exit code: " + exited.ExitCode);
                            }
                            gracefulInstallerCTS?.Dispose();
                            forcefulInstallerCTS?.Dispose();
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
                            gracefulInstallerCTS?.Cancel();
                            gracefulInstallerCTS?.Dispose();
                            forcefulInstallerCTS?.Dispose();
                            EtaTB.Text = "";
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
                        await EnqueueJob(selectedRow.gameID, selectedRow.name, selectedRow.downloadSize, selectedRow.installSize, selectedRow.downloadProperties);
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
                            gracefulInstallerCTS?.Cancel();
                            gracefulInstallerCTS?.Dispose();
                            forcefulInstallerCTS?.Dispose();
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
                        if (selectedRow.fullInstallPath != null && selectedRow.downloadProperties.downloadAction == (int)DownloadAction.Install)
                        {
                            if (Directory.Exists(selectedRow.fullInstallPath))
                            {
                                Directory.Delete(selectedRow.fullInstallPath, true);
                            }
                        }
                        selectedRow.status = (int)DownloadStatus.Canceled;
                        DownloadSpeedTB.Text = "";
                        DownloadedTB.Text = "";
                        ElapsedTB.Text = "";
                        EtaTB.Text = "";
                        DownloadPB.Value = 0;
                        SaveData();
                    }
                }
            }
        }

        private void RemoveDownloadEntry(DownloadManagerData.Download selectedEntry)
        {
            selectedEntry.PropertyChanged -= DoNextJobInQueue;
            if (selectedEntry.status != (int)DownloadStatus.Completed && selectedEntry.status != (int)DownloadStatus.Canceled)
            {
                if (selectedEntry.status == (int)DownloadStatus.Running)
                {
                    gracefulInstallerCTS?.Cancel();
                    gracefulInstallerCTS?.Dispose();
                    forcefulInstallerCTS?.Dispose();
                }
                selectedEntry.status = (int)DownloadStatus.Canceled;
            }
            var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", selectedEntry.gameID + ".resume");
            if (File.Exists(resumeFile))
            {
                File.Delete(resumeFile);
            }
            var repairFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", selectedEntry.gameID + ".repair");
            if (File.Exists(repairFile))
            {
                File.Delete(repairFile);
            }
            if (selectedEntry.fullInstallPath != null && selectedEntry.status != (int)DownloadStatus.Completed
                && selectedEntry.downloadProperties.downloadAction == (int)DownloadAction.Install)
            {
                if (Directory.Exists(selectedEntry.fullInstallPath))
                {
                    Directory.Delete(selectedEntry.fullInstallPath, true);
                }
            }
            downloadManagerData.downloads.Remove(selectedEntry);
            SaveData();
        }

        private void RemoveDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                string messageText;
                if (DownloadsDG.SelectedItems.Count == 1)
                {
                    var selectedRow = (DownloadManagerData.Download)DownloadsDG.SelectedItem;
                    messageText = string.Format(ResourceProvider.GetString(LOC.LegendaryRemoveEntryConfirm), selectedRow.name);
                }
                else
                {
                    messageText = ResourceProvider.GetString(LOC.LegendaryRemoveSelectedEntriesConfirm);
                }
                var result = playniteAPI.Dialogs.ShowMessage(messageText, ResourceProvider.GetString(LOC.LegendaryRemoveEntry), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                    {
                        RemoveDownloadEntry(selectedRow);
                    }
                }
            }
        }

        private void RemoveCompletedDownloadsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.Items.Count > 0)
            {
                var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryRemoveCompletedDownloadsConfirm), ResourceProvider.GetString(LOC.LegendaryRemoveCompletedDownloads), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var row in DownloadsDG.Items.Cast<DownloadManagerData.Download>().ToList())
                    {
                        if (row.status == (int)DownloadStatus.Completed)
                        {
                            RemoveDownloadEntry(row);
                        }
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
                FilterDownloadBtn.Content = "\uef29 " + ResourceProvider.GetString(LOC.Legendary3P_PlayniteFilterActiveLabel);
            }
            else
            {
                downloadsView.Filter = null;
                FilterDownloadBtn.Content = "\uef29";
            }
        }

        private void SetControlTextBlockStyle()
        {
            var baseStyleName = "BaseTextBlockStyle";
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                baseStyleName = "TextBlockBaseStyle";
            }

            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private void DownloadPropertiesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMaximizeButton = false,
                });
                var selectedItem = DownloadsDG.SelectedItems[0] as DownloadManagerData.Download;
                window.Title = selectedItem.name + " — " + ResourceProvider.GetString(LOC.LegendaryDownloadProperties);
                window.DataContext = selectedItem;
                window.Content = new LegendaryDownloadProperties();
                window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }
        }

        private void DownloadsDG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                ResumeDownloadBtn.IsEnabled = true;
                PauseBtn.IsEnabled = true;
                CancelDownloadBtn.IsEnabled = true;
                RemoveDownloadBtn.IsEnabled = true;
                if (DownloadsDG.SelectedItems.Count == 1)
                {
                    DownloadPropertiesBtn.IsEnabled = true;
                    OpenDownloadDirectoryBtn.IsEnabled = true;
                }
                else
                {
                    DownloadPropertiesBtn.IsEnabled = false;
                    OpenDownloadDirectoryBtn.IsEnabled = false;
                }
            }
            else
            {
                ResumeDownloadBtn.IsEnabled = false;
                PauseBtn.IsEnabled = false;
                CancelDownloadBtn.IsEnabled = false;
                RemoveDownloadBtn.IsEnabled = false;
                DownloadPropertiesBtn.IsEnabled = false;
                OpenDownloadDirectoryBtn.IsEnabled = false;
            }
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.Items.Count > 0)
            {
                DownloadsDG.SelectAll();
            }
        }

        private void OpenDownloadDirectoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DownloadsDG.SelectedItems[0] as DownloadManagerData.Download;
            var fullInstallPath = selectedItem.fullInstallPath;
            if (fullInstallPath != "")
            {
                Process.Start("explorer.exe", selectedItem.fullInstallPath);
            }
        }
    }
}
