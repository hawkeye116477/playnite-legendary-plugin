﻿using CliWrap;
using CliWrap.EventStream;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Enums;
using Playnite.SDK.Data;
using Playnite.Common;
using System.Collections.ObjectModel;
using System.ComponentModel;
using LegendaryLibraryNS.Services;
using System.Windows.Input;

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

            SelectAllBtn.ToolTip = GetToolTipWithKey(LOC.LegendarySelectAllEntries, "Ctrl+A");
            RemoveDownloadBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryRemoveEntry, "Delete");
            MoveTopBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryMoveEntryTop, "Alt+Home");
            MoveUpBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryMoveEntryUp, "Alt+Up");
            MoveDownBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryMoveEntryDown, "Alt+Down");
            MoveBottomBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryMoveEntryBottom, "Alt+End");
            DownloadPropertiesBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryEditSelectedDownloadProperties, "Ctrl+P");
            OpenDownloadDirectoryBtn.ToolTip = GetToolTipWithKey(LOC.LegendaryOpenDownloadDirectory, "Ctrl+O");
            LoadSavedData();
            var runningAndQueuedDownloads = downloadManagerData.downloads.Where(i => i.status == DownloadStatus.Running
                                                                                     || i.status == DownloadStatus.Queued).ToList();
            if (runningAndQueuedDownloads.Count > 0)
            {
                foreach (var download in runningAndQueuedDownloads)
                {
                    download.status = DownloadStatus.Paused;
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

        public string GetToolTipWithKey(string description, string shortcut)
        {
            return $"{ResourceProvider.GetString(description)} [{shortcut}]";
        }

        public DownloadManagerData.Rootobject LoadSavedData()
        {
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "downloadManager.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out downloadManagerData))
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
            Helpers.SaveJsonSettingsToFile(downloadManagerData, "downloadManager");
        }

        public async void DoNextJobInQueue()
        {
            var running = downloadManagerData.downloads.Any(item => item.status == DownloadStatus.Running);
            var queuedList = downloadManagerData.downloads.Where(i => i.status == DownloadStatus.Queued).ToList();
            if (!running)
            {
                DownloadSpeedTB.Text = "";
                DownloadedTB.Text = "";
                ElapsedTB.Text = "";
                EtaTB.Text = "";
                DownloadPB.Value = 0;
                DescriptionTB.Text = "";
                GameTitleTB.Text = "";
            }
            if (!running && queuedList.Count > 0)
            {
                await Install(queuedList[0]);
            }
            else if (!running)
            {
                var downloadCompleteSettings = LegendaryLibrary.GetSettings().DoActionAfterDownloadComplete;
                switch (downloadCompleteSettings)
                {
                    case DownloadCompleteAction.ShutDown:
                        Process.Start("shutdown", "/s /t 0");
                        break;
                    case DownloadCompleteAction.Reboot:
                        Process.Start("shutdown", "/r /t 0");
                        break;
                    case DownloadCompleteAction.Hibernate:
                        Playnite.Native.Powrprof.SetSuspendState(true, true, false);
                        break;
                    case DownloadCompleteAction.Sleep:
                        Playnite.Native.Powrprof.SetSuspendState(false, true, false);
                        break;
                    default:
                        break;
                }
            }
        }

        public void DisplayGreeting()
        {
            var messagesSettings = LegendaryMessagesSettings.LoadSettings();
            if (!messagesSettings.DontShowDownloadManagerWhatsUpMsg)
            {
                var result = MessageCheckBoxDialog.ShowMessage("", ResourceProvider.GetString(LOC.LegendaryDownloadManagerWhatsUp), ResourceProvider.GetString(LOC.Legendary3P_PlayniteDontShowAgainTitle), MessageBoxButton.OK, MessageBoxImage.Information);
                if (result.CheckboxChecked)
                {
                    messagesSettings.DontShowDownloadManagerWhatsUpMsg = true;
                    LegendaryMessagesSettings.SaveSettings(messagesSettings);
                }
            }
        }

        public void EnqueueMultipleJobs(List<DownloadManagerData.Download> downloadManagerDataList, bool silently = false)
        {
            if (!silently)
            {
                DisplayGreeting();
            }
            foreach (var downloadJob in downloadManagerDataList)
            {
                var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == downloadJob.gameID);
                if (wantedItem == null)
                {
                    DateTimeOffset now = DateTime.UtcNow;
                    downloadJob.status = DownloadStatus.Queued;
                    downloadJob.addedTime = now.ToUnixTimeSeconds();
                    downloadManagerData.downloads.Add(downloadJob);
                }
                else
                {
                    wantedItem.status = DownloadStatus.Queued;
                }
            }
            SaveData();
            DoNextJobInQueue();
        }

        public void EnqueueJob(DownloadManagerData.Download taskData)
        {
            DisplayGreeting();
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == taskData.gameID);
            if (wantedItem == null)
            {
                DateTimeOffset now = DateTime.UtcNow;
                downloadManagerData.downloads.Add(new DownloadManagerData.Download
                { gameID = taskData.gameID, downloadSize = taskData.downloadSize, installSize = taskData.installSize, name = taskData.name, status = DownloadStatus.Queued, addedTime = now.ToUnixTimeSeconds(), downloadProperties = taskData.downloadProperties });
            }
            else
            {
                wantedItem.status = DownloadStatus.Queued;
            }
            SaveData();
            DoNextJobInQueue();
        }

        public static async Task WaitUntilLegendaryCloses()
        {
            if (File.Exists(Path.Combine(LegendaryLauncher.ConfigPath, "installed.json.lock")))
            {
                await Task.Delay(1000);
                await WaitUntilLegendaryCloses();
            }
        }

        public async Task Install(DownloadManagerData.Download taskData)
        {
            await WaitUntilLegendaryCloses();
            var installCommand = new List<string>();
            var settings = LegendaryLibrary.GetSettings();
            var gameID = taskData.gameID;
            var downloadProperties = taskData.downloadProperties;
            var gameTitle = taskData.name;
            var downloadSize = taskData.downloadSize;
            if (gameID == "eos-overlay")
            {
                var fullInstallPath = Path.Combine(downloadProperties.installPath, ".overlay");
                taskData.fullInstallPath = fullInstallPath;
                installCommand = new List<string>() { "-y", "eos-overlay" };
                if (downloadProperties.downloadAction == DownloadAction.Update)
                {

                    installCommand.Add("update");
                }
                else
                {
                    installCommand.AddRange(new[] { "install", "--path", fullInstallPath });
                }
            }
            else
            {
                installCommand = new List<string>() { "-y", "install", gameID };
                if (downloadProperties.installPath != "")
                {
                    installCommand.AddRange(new[] { "--base-path", downloadProperties.installPath });
                }
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
                if (downloadProperties.ignoreFreeSpace)
                {
                    installCommand.Add("--ignore-free-space");
                }
                if (settings.ConnectionTimeout != 0)
                {
                    installCommand.AddRange(new[] { "--dl-timeout", settings.ConnectionTimeout.ToString() });
                }
                if (downloadProperties.downloadAction == DownloadAction.Repair)
                {
                    installCommand.Add("--repair");
                }
                if (downloadProperties.downloadAction == DownloadAction.Update)
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
                        if (downloadProperties.downloadAction == DownloadAction.Repair)
                        {
                            installCommand.Add("--reset-sdl");
                        }
                    }
                }
                installCommand.Add("--skip-dlcs");
            }

            forcefulInstallerCTS = new CancellationTokenSource();
            gracefulInstallerCTS = new CancellationTokenSource();
            try
            {
                bool errorDisplayed = false;
                bool successDisplayed = false;
                bool loginErrorDisplayed = false;
                string memoryErrorMessage = "";
                bool permissionErrorDisplayed = false;
                bool diskSpaceErrorDisplayed = false;
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                             .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                             .WithArguments(installCommand)
                             .AddCommandToLog()
                             .WithValidation(CommandResultValidation.None);
                var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameID);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync(Console.OutputEncoding, Console.OutputEncoding, forcefulInstallerCTS.Token, gracefulInstallerCTS.Token))
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            wantedItem.status = DownloadStatus.Running;
                            GameTitleTB.Text = gameTitle;
                            DownloadPB.Value = 0;
                            break;
                        case StandardOutputCommandEvent stdOut:
                            if (downloadProperties.downloadAction == DownloadAction.Repair)
                            {
                                var verificationProgressMatch = Regex.Match(stdOut.Text, @"Verification progress:.*\((\d.*%)");
                                if (verificationProgressMatch.Length >= 2)
                                {
                                    double progress = double.Parse(verificationProgressMatch.Groups[1].Value.Replace("%", ""), CultureInfo.InvariantCulture);
                                    DownloadPB.Value = progress;
                                }
                                var verificationFileProgressMatch = Regex.Match(stdOut.Text, @"Verifying large file \""(.*)""\: (\d.*%) \((\d+\.\d+)\/(\d+\.\d+) (\wiB)");
                                if (verificationFileProgressMatch.Length >= 2)
                                {
                                    string fileName = verificationFileProgressMatch.Groups[1].Value;
                                    string largeProgressPercent = verificationFileProgressMatch.Groups[2].Value;
                                    string readSize = Helpers.FormatSize(double.Parse(verificationFileProgressMatch.Groups[3].Value, CultureInfo.InvariantCulture), verificationFileProgressMatch.Groups[5].Value);
                                    string fullSize = Helpers.FormatSize(double.Parse(verificationFileProgressMatch.Groups[4].Value, CultureInfo.InvariantCulture), verificationFileProgressMatch.Groups[5].Value);
                                    DescriptionTB.Text = ResourceProvider.GetString(LOC.LegendaryVerifyingLargeFile).Format(fileName, $"{largeProgressPercent} ({readSize}/{fullSize})");
                                }
                                else if (stdOut.Text.Contains("Verification"))
                                {
                                    DescriptionTB.Text = ResourceProvider.GetString(LOC.LegendaryVerifying);
                                }
                            }
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
                            var progressMatch = Regex.Match(stdErr.Text, @"Progress: (\d.*%)");
                            if (progressMatch.Length >= 2)
                            {
                                double progress = double.Parse(progressMatch.Groups[1].Value.Replace("%", ""), CultureInfo.InvariantCulture);
                                if (downloadProperties.downloadAction != DownloadAction.Update)
                                {
                                    DescriptionTB.Text = ResourceProvider.GetString(LOC.Legendary3P_PlayniteDownloadingLabel);
                                }
                                else
                                {
                                    DescriptionTB.Text = ResourceProvider.GetString(LOC.LegendaryDownloadingUpdate);
                                }
                                DownloadPB.Value = progress;
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                ElapsedTB.Text = elapsedMatch.Groups[1].Value;
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                EtaTB.Text = ETAMatch.Groups[1].Value;
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+) (\wiB)");
                            if (downloadedMatch.Length >= 2)
                            {
                                string downloaded = Helpers.FormatSize(double.Parse(downloadedMatch.Groups[1].Value, CultureInfo.InvariantCulture), downloadedMatch.Groups[2].Value);
                                DownloadedTB.Text = downloaded + " / " + downloadSize;
                                if (downloaded == downloadSize)
                                {
                                    switch (downloadProperties.downloadAction)
                                    {
                                        case DownloadAction.Install:
                                            DescriptionTB.Text = ResourceProvider.GetString(LOC.LegendaryFinishingInstallation);
                                            break;
                                        case DownloadAction.Update:
                                            DescriptionTB.Text = ResourceProvider.GetString(LOC.LegendaryFinishingUpdate);
                                            break;
                                        case DownloadAction.Repair:
                                            DescriptionTB.Text = ResourceProvider.GetString(LOC.LegendaryFinishingRepair);
                                            break;
                                        default:
                                            break;
                                    }
                                }
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
                            var errorMessage = stdErr.Text;
                            if (errorMessage.Contains("finished successfully") || errorMessage.Contains("already up to date"))
                            {
                                successDisplayed = true;
                            }
                            else if (errorMessage.Contains("WARNING") && !errorMessage.Contains("exit requested") && !errorMessage.Contains("PermissionError"))
                            {
                                logger.Warn($"[Legendary] {errorMessage}");
                            }
                            else if (errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error") || errorMessage.Contains("Failure"))
                            {
                                logger.Error($"[Legendary] {errorMessage}");
                                if (errorMessage.Contains("Failed to establish a new connection")
                                    || errorMessage.Contains("Log in failed")
                                    || errorMessage.Contains("Login failed")
                                    || errorMessage.Contains("No saved credentials"))
                                {
                                    loginErrorDisplayed = true;
                                }
                                else if (errorMessage.Contains("MemoryError"))
                                {
                                    memoryErrorMessage = errorMessage;
                                }
                                else if (errorMessage.Contains("PermissionError"))
                                {
                                    permissionErrorDisplayed = true;
                                }
                                else if (errorMessage.Contains("Not enough available disk space"))
                                {
                                    diskSpaceErrorDisplayed = true;
                                }
                                if (!errorMessage.Contains("old manifest"))
                                {
                                    errorDisplayed = true;
                                }
                            }
                            break;
                        case ExitedCommandEvent exited:
                            if ((!successDisplayed && errorDisplayed) || exited.ExitCode != 0)
                            {
                                if (loginErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError).Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                                }
                                else if (memoryErrorMessage != "")
                                {
                                    var memoryErrorMatch = Regex.Match(memoryErrorMessage, @"MemoryError: Current shared memory cache is smaller than required: (\S+) MiB < (\S+) MiB");
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), string.Format(ResourceProvider.GetString(LOC.LegendaryMemoryError), memoryErrorMatch.Groups[1] + " MB", memoryErrorMatch.Groups[2] + " MB")));
                                }
                                else if (permissionErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryPermissionError)));
                                }
                                else if (diskSpaceErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryNotEnoughSpace)));
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                                }
                                wantedItem.status = DownloadStatus.Paused;
                            }
                            else
                            {
                                var installedAppList = LegendaryLauncher.GetInstalledAppList();
                                if (installedAppList != null)
                                {
                                    if (installedAppList.ContainsKey(gameID))
                                    {
                                        var installedGameInfo = installedAppList[gameID];
                                        Playnite.SDK.Models.Game game = new Playnite.SDK.Models.Game();
                                        if (installedGameInfo.Is_dlc == false || !installedGameInfo.Executable.IsNullOrEmpty())
                                        {
                                            game = playniteAPI.Database.Games.FirstOrDefault(item => item.PluginId == LegendaryLibrary.Instance.Id && item.GameId == gameID);
                                            game.InstallDirectory = installedGameInfo.Install_path;
                                            game.Version = installedGameInfo.Version;
                                            game.InstallSize = (ulong?)installedGameInfo.Install_size;
                                            game.IsInstalled = true;
                                            var playtimeSyncEnabled = LegendaryLibrary.GetSettings().SyncPlaytime;
                                            if (playtimeSyncEnabled && downloadProperties.downloadAction != DownloadAction.Update)
                                            {
                                                var accountApi = new EpicAccountClient(playniteAPI, LegendaryLauncher.TokensPath);
                                                var playtimeItems = await accountApi.GetPlaytimeItems();
                                                var playtimeItem = playtimeItems?.FirstOrDefault(x => x.artifactId == gameID);
                                                if (playtimeItem != null)
                                                {
                                                    game.Playtime = playtimeItem.totalTime;
                                                }
                                            }
                                            // Dishonored: Death of the Outsider and Fallout: New Vegas need specific key in registry
                                            if (gameID == "2fb8273dcf6f41e4899c0c881e047053" || gameID == "5daeb974a22a435988892319b3a4f476")
                                            {
                                                try
                                                {
                                                    using (var regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("com.epicgames.launcher", false))
                                                    {
                                                        if (regKey == null)
                                                        {
                                                            Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\com.epicgames.launcher");
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    logger.Error($"Failed to create registry key for {gameTitle}. Error: {ex.Message}");
                                                }
                                            }
                                            if (downloadProperties.installPrerequisites)
                                            {
                                                if (installedGameInfo.Prereq_info != null)
                                                {
                                                    var gameSettings = new GameSettings
                                                    {
                                                        InstallPrerequisites = true
                                                    };
                                                    Helpers.SaveJsonSettingsToFile(gameSettings, gameID, "GamesSettings");
                                                }
                                            }
                                            playniteAPI.Database.Games.Update(game);
                                        }
                                    }
                                }
                                wantedItem.status = DownloadStatus.Completed;
                                DateTimeOffset now = DateTime.UtcNow;
                                wantedItem.completedTime = now.ToUnixTimeSeconds();
                                Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(LegendaryLauncher.Icon), gameTitle, ResourceProvider.GetString(LOC.LegendaryInstallationFinished), null);
                            }
                            SaveData();
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
            finally
            {
                DoNextJobInQueue();
            }
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    if (selectedRow.status == DownloadStatus.Running ||
                               selectedRow.status == DownloadStatus.Queued)
                    {
                        if (selectedRow.status == DownloadStatus.Running)
                        {
                            gracefulInstallerCTS?.Cancel();
                            gracefulInstallerCTS?.Dispose();
                            forcefulInstallerCTS?.Dispose();
                        }
                        selectedRow.status = DownloadStatus.Paused;
                    }
                }
                SaveData();
            }
        }

        private void ResumeDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    if (selectedRow.status == DownloadStatus.Canceled ||
                        selectedRow.status == DownloadStatus.Paused)
                    {
                        EnqueueJob(selectedRow);
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
                    if (selectedRow.status == DownloadStatus.Running ||
                        selectedRow.status == DownloadStatus.Queued ||
                        selectedRow.status == DownloadStatus.Paused)
                    {
                        if (selectedRow.status == DownloadStatus.Running)
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
                        if (selectedRow.fullInstallPath != null && selectedRow.downloadProperties.downloadAction == DownloadAction.Install)
                        {
                            if (Directory.Exists(selectedRow.fullInstallPath))
                            {
                                Directory.Delete(selectedRow.fullInstallPath, true);
                            }
                        }
                        selectedRow.status = DownloadStatus.Canceled;
                        DownloadSpeedTB.Text = "";
                        DownloadedTB.Text = "";
                        ElapsedTB.Text = "";
                        EtaTB.Text = "";
                        DownloadPB.Value = 0;
                        DescriptionTB.Text = "";
                        GameTitleTB.Text = "";
                    }
                }
                SaveData();
            }
        }

        private void RemoveDownloadEntry(DownloadManagerData.Download selectedEntry)
        {
            if (selectedEntry.status != DownloadStatus.Completed && selectedEntry.status != DownloadStatus.Canceled)
            {
                if (selectedEntry.status == DownloadStatus.Running)
                {
                    gracefulInstallerCTS?.Cancel();
                    gracefulInstallerCTS?.Dispose();
                    forcefulInstallerCTS?.Dispose();
                }
                selectedEntry.status = DownloadStatus.Canceled;
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
            if (selectedEntry.fullInstallPath != null && selectedEntry.status != DownloadStatus.Completed
                && selectedEntry.downloadProperties.downloadAction == DownloadAction.Install)
            {
                if (Directory.Exists(selectedEntry.fullInstallPath))
                {
                    Directory.Delete(selectedEntry.fullInstallPath, true);
                }
            }
            downloadManagerData.downloads.Remove(selectedEntry);
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
                SaveData();
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
                        if (row.status == DownloadStatus.Completed)
                        {
                            RemoveDownloadEntry(row);
                        }
                    }
                    SaveData();
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
            var checkedStatus = new List<DownloadStatus>();
            foreach (CheckBox checkBox in FilterStatusSP.Children)
            {
                var downloadStatus = (DownloadStatus)Enum.Parse(typeof(DownloadStatus), checkBox.Name.Replace("Chk", ""));
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
                MoveBottomBtn.IsEnabled = true;
                MoveDownBtn.IsEnabled = true;
                MoveTopBtn.IsEnabled = true;
                MoveUpBtn.IsEnabled = true;
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
                MoveBottomBtn.IsEnabled = false;
                MoveDownBtn.IsEnabled = false;
                MoveTopBtn.IsEnabled = false;
                MoveUpBtn.IsEnabled = false;
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
            if (fullInstallPath != "" && Directory.Exists(fullInstallPath))
            {
                ProcessStarter.StartProcess("explorer.exe", selectedItem.fullInstallPath);
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage($"{selectedItem.fullInstallPath}\n{ResourceProvider.GetString(LOC.LegendaryPathNotExistsError)}");
            }
        }

        private enum EntryPosition
        {
            Up,
            Down,
            Top,
            Bottom
        }

        private void MoveEntries(EntryPosition entryPosition, bool moveFocus = false)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var selectedIndexes = new List<int>();
                var allItems = DownloadsDG.Items;
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<DownloadManagerData.Download>().ToList())
                {
                    var selectedIndex = allItems.IndexOf(selectedRow);
                    selectedIndexes.Add(selectedIndex);
                }
                selectedIndexes.Sort();
                if (entryPosition == EntryPosition.Down || entryPosition == EntryPosition.Top)
                {
                    selectedIndexes.Reverse();
                }
                var lastIndex = downloadManagerData.downloads.Count - 1;
                int loopIndex = 0;
                foreach (int selectedIndex in selectedIndexes)
                {
                    int newIndex = selectedIndex;
                    int newSelectedIndex = selectedIndex;
                    switch (entryPosition)
                    {
                        case EntryPosition.Up:
                            if (selectedIndex != 0)
                            {
                                newIndex = selectedIndex - 1;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case EntryPosition.Down:
                            if (selectedIndex != lastIndex)
                            {
                                newIndex = selectedIndex + 1;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case EntryPosition.Top:
                            newSelectedIndex += loopIndex;
                            newIndex = 0;
                            break;
                        case EntryPosition.Bottom:
                            newIndex = lastIndex;
                            newSelectedIndex -= loopIndex;
                            break;
                    }
                    downloadManagerData.downloads.Move(newSelectedIndex, newIndex);
                    loopIndex++;
                }
                if (moveFocus)
                {
                    DownloadsDG.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
                SaveData();
            }
        }

        private void MoveUpBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Up);
        }
        private void MoveTopBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Top);
        }

        private void MoveDownBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Down);
        }

        private void MoveBottomBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Bottom);
        }

        private void LegendaryDownloadManagerUC_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                RemoveDownloadBtn_Click(sender, e);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Home))
            {
                MoveEntries(EntryPosition.Top, true);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Up))
            {
                MoveEntries(EntryPosition.Up, true);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Down))
            {
                MoveEntries(EntryPosition.Down, true);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.End))
            {
                MoveEntries(EntryPosition.Bottom, true);
            }
            else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.P)
            {
                DownloadPropertiesBtn_Click(sender, e);
            }
            else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.O)
            {
                OpenDownloadDirectoryBtn_Click(sender, e);
            }
        }
    }
}
