using CliWrap;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;

namespace LegendaryLibraryNS
{
    public class LegendaryDownloadLogic : IUnifiedDownloadLogic
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public bool downloadsChanged = false;

        public static async Task WaitUntilLegendaryCloses()
        {
            var installedLockPath = Path.Combine(LegendaryLauncher.ConfigPath, "installed.json.lock");
            if (File.Exists(installedLockPath) && Helpers.IsFileLocked(installedLockPath))
            {
                await Task.Delay(1000);
                await WaitUntilLegendaryCloses();
            }
        }

        public static bool CheckIfUdmInstalled()
        {
            var playniteAPI = API.Instance;
            bool installed = playniteAPI.Addons.Plugins.Any(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
            if (!installed)
            {
                var options = new List<MessageBoxOption>
                {
                    new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame)),
                    new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel)),
                };
                var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled, new Dictionary<string, IFluentType> { ["launcherName"] = (FluentString)"Unified Download Manager" }), "Legendary (Epic Games) library integration", MessageBoxImage.Information, options);
                if (result == options[0])
                {
                    Playnite.Commands.GlobalCommands.NavigateUrl("playnite://playnite/installaddon/UnifiedDownloadManager");
                }
            }
            return installed;
        }

        public async Task AddTasks(List<DownloadManagerData.Download> downloadTasks)
        {
            var unifiedTasks = new List<UnifiedDownload>();
            foreach (var downloadTask in downloadTasks)
            {
                LegendaryLibrary.Instance.pluginDownloadData.downloads.Add(downloadTask);
                var unifiedTask = new UnifiedDownload
                {
                    gameID = downloadTask.gameID,
                    name = downloadTask.name,
                    downloadSizeBytes = downloadTask.downloadSizeNumber,
                    installSizeBytes = downloadTask.installSizeNumber,
                    fullInstallPath = downloadTask.fullInstallPath,
                    pluginId = LegendaryLibrary.Instance.Id.ToString(),
                    sourceName = "Epic",
                };
                unifiedTasks.Add(unifiedTask);
            }
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            await unifiedDownloadManagerApi.AddTasks(unifiedTasks);
            LegendaryLibrary.Instance.SaveDownloadData();
        }


        public async Task StartDownload(UnifiedDownload downloadTask)
        {
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            await WaitUntilLegendaryCloses();
            var installCommand = new List<string>();
            var settings = LegendaryLibrary.GetSettings();
            var gameID = downloadTask.gameID;
            var wantedUnifiedTask = unifiedDownloadManagerApi.GetTask(gameID, LegendaryLibrary.Instance.Id.ToString());
            var matchingPluginTask = LegendaryLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == gameID);


            var downloadProperties = matchingPluginTask.downloadProperties;
            var gameTitle = wantedUnifiedTask.name;

            double cachedDownloadSizeNumber = wantedUnifiedTask.downloadSizeBytes;
            double newDownloadSizeNumber = 0;
            double downloadCache = 0;
            if (gameID == "eos-overlay")
            {
                var fullInstallPath = Path.Combine(downloadProperties.installPath, ".eos-overlay");
                if (downloadProperties.downloadAction != DownloadAction.Install)
                {
                    fullInstallPath = downloadProperties.installPath;
                }
                wantedUnifiedTask.fullInstallPath = fullInstallPath;
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

            var forcefulInstallerCTS = wantedUnifiedTask.forcefulCts;
            var gracefulInstallerCTS = wantedUnifiedTask.gracefulCts;
            try
            {
                bool errorDisplayed = false;
                bool successDisplayed = false;
                bool loginErrorDisplayed = false;
                string memoryErrorMessage = "";
                bool permissionErrorDisplayed = false;
                bool diskSpaceErrorDisplayed = false;
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                             .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                             .WithArguments(installCommand)
                             .AddCommandToLog()
                             .WithValidation(CommandResultValidation.None);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync(Encoding.Default, Encoding.Default, forcefulInstallerCTS.Token, gracefulInstallerCTS.Token))
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            wantedUnifiedTask.status = UnifiedDownloadStatus.Running;
                            break;
                        case StandardOutputCommandEvent stdOut:
                            if (downloadProperties.downloadAction == DownloadAction.Repair)
                            {
                                var verificationProgressMatch = Regex.Match(stdOut.Text, @"Verification progress:.*\((\d.*%)");
                                if (verificationProgressMatch.Length >= 2)
                                {
                                    double progress = CommonHelpers.ToDouble(verificationProgressMatch.Groups[1].Value.Replace("%", ""));
                                    wantedUnifiedTask.progress = progress;
                                }
                                var verificationFileProgressMatch = Regex.Match(stdOut.Text, @"Verifying large file \""(.*)""\: (\d.*%) \((\d+\.\d+)\/(\d+\.\d+) (\wiB)");
                                if (verificationFileProgressMatch.Length >= 2)
                                {
                                    string fileName = verificationFileProgressMatch.Groups[1].Value;
                                    string largeProgressPercent = verificationFileProgressMatch.Groups[2].Value;
                                    string readSize = CommonHelpers.FormatSize(CommonHelpers.ToDouble(verificationFileProgressMatch.Groups[3].Value), verificationFileProgressMatch.Groups[5].Value);
                                    string fullSize = CommonHelpers.FormatSize(CommonHelpers.ToDouble(verificationFileProgressMatch.Groups[4].Value), verificationFileProgressMatch.Groups[5].Value);
                                    wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonVerifyingLargeFile, new Dictionary<string, IFluentType> { ["fileName"] = (FluentString)fileName, ["progress"] = (FluentString)$"{largeProgressPercent} ({readSize}/{fullSize})" });
                                }
                                else if (stdOut.Text.Contains("Verification"))
                                {
                                    wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonVerifying);
                                }
                            }
                            break;
                        case StandardErrorCommandEvent stdErr:
                            var downloadSizeMatch = Regex.Match(stdErr.Text, @"Download size: (\S+) (\wiB)");
                            if (downloadSizeMatch.Length >= 2)
                            {
                                newDownloadSizeNumber = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadSizeMatch.Groups[1].Value), downloadSizeMatch.Groups[2].Value);
                                if (newDownloadSizeNumber > cachedDownloadSizeNumber)
                                {
                                    wantedUnifiedTask.downloadSizeBytes = newDownloadSizeNumber;
                                    cachedDownloadSizeNumber = newDownloadSizeNumber;
                                }
                                downloadCache = cachedDownloadSizeNumber - newDownloadSizeNumber;
                            }
                            var installSizeMatch = Regex.Match(stdErr.Text, @"Install size: (\S+) (\wiB)");
                            if (installSizeMatch.Length >= 2)
                            {
                                double installSizeNumber = CommonHelpers.ToBytes(CommonHelpers.ToDouble(installSizeMatch.Groups[1].Value), installSizeMatch.Groups[2].Value);
                                wantedUnifiedTask.installSizeBytes = installSizeNumber;
                            }
                            var fullInstallPathMatch = Regex.Match(stdErr.Text, @"Install path: (\S+)");
                            if (fullInstallPathMatch.Length >= 2)
                            {
                                wantedUnifiedTask.fullInstallPath = fullInstallPathMatch.Groups[1].Value;
                            }
                            var progressMatch = Regex.Match(stdErr.Text, @"Progress: (\d.*%)");
                            if (progressMatch.Length >= 2)
                            {
                                if (downloadProperties.downloadAction != DownloadAction.Update)
                                {
                                    wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDownloadingLabel);
                                }
                                else
                                {
                                    wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonDownloadingUpdate);
                                }
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.elapsed = TimeSpan.Parse(elapsedMatch.Groups[1].Value);
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                wantedUnifiedTask.eta = TimeSpan.Parse(ETAMatch.Groups[1].Value);
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+) (\wiB)");
                            if (downloadedMatch.Length >= 2)
                            {
                                double downloadedNumber = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadedMatch.Groups[1].Value), downloadedMatch.Groups[2].Value);
                                double totalDownloadedNumber = downloadedNumber + downloadCache;
                                wantedUnifiedTask.downloadedBytes = totalDownloadedNumber;
                                double newProgress = totalDownloadedNumber / wantedUnifiedTask.downloadSizeBytes * 100;
                                wantedUnifiedTask.progress = newProgress;
                                //legendaryPanel.ProgressValue = newProgress;

                                if (totalDownloadedNumber == wantedUnifiedTask.downloadSizeBytes)
                                {
                                    switch (downloadProperties.downloadAction)
                                    {
                                        case DownloadAction.Install:
                                            wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation);
                                            break;
                                        case DownloadAction.Update:
                                            wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingUpdate);
                                            break;
                                        case DownloadAction.Repair:
                                            wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingRepair);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+) (\wiB)");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.downloadSpeedBytes = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadSpeedMatch.Groups[1].Value), downloadSpeedMatch.Groups[2].Value);
                            }
                            var diskSpeedMatch = Regex.Match(stdErr.Text, @"Disk\t- (\S+) (\wiB)");
                            if (diskSpeedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.diskWriteSpeedBytes = CommonHelpers.ToBytes(CommonHelpers.ToDouble(diskSpeedMatch.Groups[1].Value), diskSpeedMatch.Groups[2].Value);
                            }
                            var errorMessage = stdErr.Text;
                            if (errorMessage.Contains("finished") || errorMessage.Contains("Finished") || errorMessage.Contains("already up to date"))
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
                                if (!errorMessage.Contains("old manifest") && !errorMessage.Contains("EGL ProgramData"))
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
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }));
                                }
                                else if (memoryErrorMessage != "")
                                {
                                    var memoryErrorMatch = Regex.Match(memoryErrorMessage, @"MemoryError: Current shared memory cache is smaller than required: (\S+) MiB < (\S+) MiB");
                                    var gameErrorFluentArgs = new Dictionary<string, IFluentType>
                                    {
                                        ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.LegendaryMemoryError, new Dictionary<string, IFluentType> { ["currentMemory"] = (FluentString)$"{memoryErrorMatch.Groups[1]} MB", ["requiredMemory"] = (FluentString)$"{memoryErrorMatch.Groups[2]} MB" })
                                    };
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, gameErrorFluentArgs));
                                }
                                else if (permissionErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonPermissionError) }));
                                }
                                else if (diskSpaceErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonNotEnoughSpace) }));
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                                }
                                wantedUnifiedTask.status = UnifiedDownloadStatus.Error;
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
                                            var playtimeSyncEnabled = false;
                                            if (downloadProperties.downloadAction == DownloadAction.Repair)
                                            {
                                                if (playniteAPI.ApplicationSettings.PlaytimeImportMode != PlaytimeImportMode.Never)
                                                {
                                                    playtimeSyncEnabled = LegendaryLibrary.GetSettings().SyncPlaytime;
                                                    var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.GameId);
                                                    if (gameSettings?.AutoSyncPlaytime != null)
                                                    {
                                                        playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                                                    }
                                                }
                                                if (playtimeSyncEnabled)
                                                {
                                                    var accountApi = new EpicAccountClient(playniteAPI);
                                                    var playtimeItems = await accountApi.GetPlaytimeItems();
                                                    var playtimeItem = playtimeItems?.FirstOrDefault(x => x.artifactId == gameID);
                                                    if (playtimeItem != null)
                                                    {
                                                        game.Playtime = playtimeItem.totalTime;
                                                    }
                                                }
                                            }
                                            // Some games need specific key in registry, otherwise they can't launch
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
                                            if (downloadProperties.installPrerequisites)
                                            {
                                                if (installedGameInfo.Prereq_info != null)
                                                {
                                                    var gameSettings = new GameSettings
                                                    {
                                                        InstallPrerequisites = true
                                                    };
                                                    var commonHelpers = LegendaryLibrary.Instance.commonHelpers;
                                                    commonHelpers.SaveJsonSettingsToFile(gameSettings, "GamesSettings", gameID, true);
                                                }
                                            }
                                            playniteAPI.Database.Games.Update(game);
                                        }
                                    }
                                }
                                wantedUnifiedTask.status = UnifiedDownloadStatus.Completed;
                                wantedUnifiedTask.progress = 100;
                                DateTimeOffset now = DateTime.UtcNow;
                                wantedUnifiedTask.completedTime = now.ToUnixTimeSeconds();
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
            finally
            {
                downloadsChanged = true;
            }
        }


        public async Task OnCancelDownload(UnifiedDownload downloadTask)
        {
            var gameID = downloadTask.gameID;
            await WaitUntilLegendaryCloses();
            var matchingPluginTask = LegendaryLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == gameID);
            const int maxRetries = 5;
            int delayMs = 100;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", gameID + ".resume");
                    if (File.Exists(resumeFile))
                    {
                        File.Delete(resumeFile);
                    }
                    var repairFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", gameID + ".repair");
                    if (File.Exists(repairFile))
                    {
                        File.Delete(repairFile);
                    }
                    if (downloadTask.fullInstallPath != null && matchingPluginTask.downloadProperties.downloadAction == DownloadAction.Install)
                    {
                        if (Directory.Exists(downloadTask.fullInstallPath))
                        {
                            Directory.Delete(downloadTask.fullInstallPath, true);
                        }
                    }
                }
                catch (Exception rex)
                {
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                    else
                    {
                        logger.Warn(rex, $"Can't cleanup after cancellation. Please try removing files manually.");
                        break;
                    }
                }
            }
        }

        public Task OnRemoveDownloadEntry(UnifiedDownload downloadTask)
        {
            var matchingPluginTask = LegendaryLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == downloadTask.gameID);
            if (matchingPluginTask != null)
            {
                downloadsChanged = true;
                LegendaryLibrary.Instance.pluginDownloadData.downloads.Remove(matchingPluginTask);
                LegendaryLibrary.Instance.SaveDownloadData();
            }
            return Task.CompletedTask;
        }

        public void OpenDownloadPropertiesWindow(UnifiedDownload selectedEntry)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
            });
            var matchingPluginTask = LegendaryLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == selectedEntry.gameID);
            window.Title = selectedEntry.name + " — " + LocalizationManager.Instance.GetString(LOC.CommonDownloadProperties);
            window.DataContext = matchingPluginTask;
            window.Content = new LegendaryDownloadProperties();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

    }
}
