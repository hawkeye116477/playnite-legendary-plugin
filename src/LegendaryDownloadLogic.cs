using CliWrap;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;

namespace LegendaryLibraryNS
{
    public class LegendaryDownloadLogic : IUnifiedDownloadLogic
    {
        private static readonly RetryHandler retryHandler = new RetryHandler(new HttpClientHandler());
        private static readonly HttpClient client = new HttpClient(retryHandler);
        private static readonly ILogger logger = LogManager.GetLogger();
        private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;

        public static async Task WaitUntilLegendaryCloses()
        {
            var installedLockPath = Path.Combine(LegendaryLauncher.ConfigPath, "installed.json.lock");
            if (File.Exists(installedLockPath) && Helpers.IsFileLocked(installedLockPath))
            {
                await Task.Delay(1000);
                await WaitUntilLegendaryCloses();
            }
        }

        public static async Task<bool> CheckIfUdmInstalled()
        {
            var playniteAPI = LegendaryLibrary.PlayniteApi;
            bool installed = playniteAPI.Addons.Plugins.Any(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
            if (!installed)
            {
                var options = new List<MessageBoxResponse>
                {
                    new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame)),
                    new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel)),
                };
                var result = await playniteAPI.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled, new Dictionary<string, IFluentType> { ["launcherName"] = (FluentString)"Unified Download Manager" }), "Legendary (Epic Games) library integration", MessageBoxSeverity.Error, options, []);
                if (result == options[0])
                {
                    Playnite.Commands.GlobalCommands.NavigateUrl("playnite://playnite/installaddon/UnifiedDownloadManager");
                }
            }
            return installed;
        }

        public async Task AddTasks(List<DownloadManagerData.Download> downloadTasks, bool silently = false)
        {
            var unifiedTasks = new List<UnifiedDownload>();
            var downloadItemsAlreadyAdded = new List<string>();
            var unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(playniteApi);
            foreach (var downloadTask in downloadTasks)
            {
                bool completedDownload = true;
                var wantedUnifiedItem = unifiedDownloadManagerApi.GetTask(downloadTask.GameId,  LegendaryLibrary.PluginId);
                if (wantedUnifiedItem != null)
                {
                    if (wantedUnifiedItem.Status != UnifiedDownloadStatus.Completed)
                    {
                        completedDownload = false;
                    }
                }
                if (completedDownload)
                {
                    var wantedPluginItem = LegendaryLibrary.Instance.PluginDownloadData?.Downloads?.FirstOrDefault(item => item.GameId == downloadTask.GameId);
                    if (wantedPluginItem != null)
                    {
                        LegendaryLibrary.Instance.PluginDownloadData?.Downloads?.Remove(wantedPluginItem);
                        wantedPluginItem = LegendaryLibrary.Instance.PluginDownloadData.Downloads.FirstOrDefault(item => item.GameId == downloadTask.GameId);
                    }
                    if (wantedUnifiedItem != null)
                    {
                        unifiedDownloadManagerApi.RemoveTask(wantedUnifiedItem);
                        wantedUnifiedItem = unifiedDownloadManagerApi.GetTask(downloadTask.GameId, LegendaryLibrary.PluginId);
                    }
                }
                if (wantedUnifiedItem != null)
                {
                    downloadItemsAlreadyAdded.Add(wantedUnifiedItem.Name);
                    continue;
                }
                LegendaryLibrary.Instance.PluginDownloadData?.Downloads?.Add(downloadTask);
                var unifiedTask = new UnifiedDownload
                {
                    GameId = downloadTask.GameId,
                    Name = downloadTask.Name,
                    PluginId = LegendaryLibrary.PluginId.ToString(),
                    SourceName = "Epic",
                    AddedTime = downloadTask.AddedTime,
                    DownloadSizeBytes = downloadTask.DownloadSizeNumber,
                    InstallSizeBytes = downloadTask.InstallSizeNumber,
                    FullInstallPath = downloadTask.FullInstallPath
                };
                unifiedTasks.Add(unifiedTask);
            }
            await unifiedDownloadManagerApi.AddTasks(unifiedTasks);
            LegendaryLibrary.Instance.SaveDownloadData();

            if (!silently && unifiedTasks.Count == 0)
            {
                if (downloadItemsAlreadyAdded.Count > 0)
                {
                    string downloadItemsAlreadyAddedCombined = downloadItemsAlreadyAdded[0];
                    if (downloadItemsAlreadyAdded.Count > 1)
                    {
                        downloadItemsAlreadyAddedCombined = string.Join(", ", downloadItemsAlreadyAdded.Select(item => item.ToString()));
                    }
                    await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonDownloadAlreadyExists, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)downloadItemsAlreadyAddedCombined, ["count"] = (FluentNumber)downloadItemsAlreadyAdded.Count, ["pluginShortName"] = (FluentString)"Unified Download Manager" }), "", MessageBoxButtons.OK, MessageBoxSeverity.Error);
                }
            }
        }

        public async Task StartDownload(UnifiedDownload downloadTask)
        {
            var gameId = downloadTask.GameId;
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(playniteApi);
            var wantedUnifiedTask = unifiedDownloadManagerApi.GetTask(gameId, LegendaryLibrary.PluginId);
            try
            {
                if (gameId == "legendary-launcher")
                {
                    if (wantedUnifiedTask != null)
                    {
                        await DownloadLauncher(wantedUnifiedTask);
                    }
                }
                else
                {
                    await DownloadOtherThings(downloadTask);
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && (downloadTask.Status == UnifiedDownloadStatus.Canceled || downloadTask.Status == UnifiedDownloadStatus.Paused))
                {
                    if (downloadTask.Status == UnifiedDownloadStatus.Canceled)
                    {
                        await OnCancelDownload(downloadTask);
                    }
                }
                else
                {
                    logger.Debug($"An error occured during downloading {downloadTask.Name}: {ex.Message}");
                    downloadTask.Status = UnifiedDownloadStatus.Error;
                }
            }
            finally
            {
                LegendaryLibrary.Instance.SaveDownloadData();
            }
        }

        private async Task DownloadLauncher(UnifiedDownload downloadTask, int bufferSize = 1 * 1024 * 1024)
        {
            var totalStopwatch = Stopwatch.StartNew();
            downloadTask.Status = UnifiedDownloadStatus.Running;
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", LegendaryLauncher.userAgent);

            var tempDir = Path.Combine(downloadTask.FullInstallPath, ".Downloader_temp");
            if (!await LegendaryLibrary.Instance.CommonHelpers.IsDirectoryWritable(tempDir))
            {
                var tempFolderName = $"{downloadTask.GameId}_PlayniteLegendaryPlugin";
                tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "temp", tempFolderName);
            }
            Directory.CreateDirectory(tempDir);

            long totalSize = 0;
            long downloadedBytes = 0;

            var url = "";
            var versionInfoContent = await LegendaryLauncher.GetVersionInfoContent();
            if (!versionInfoContent.Tag_name.IsNullOrEmpty())
            {
                var newAsset = versionInfoContent.Assets.FirstOrDefault(a => a.Browser_download_url.Contains($"{versionInfoContent.Tag_name}/legendary")
                                                                             && a.Browser_download_url.EndsWith(".exe"));
                if (newAsset?.Browser_download_url != null)
                {
                    url = newAsset.Browser_download_url;
                }
            }
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            if (downloadTask.GracefulCts != null)
            {
                using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, downloadTask.GracefulCts.Token);
                headResponse.EnsureSuccessStatusCode();
                totalSize = headResponse.Content.Headers.ContentLength ?? 0;
                downloadTask.DownloadSizeBytes = totalSize;
                downloadTask.InstallSizeBytes = totalSize;
                var contentDisposition = headResponse.Content.Headers.ContentDisposition;
                var serverFileName =
                    contentDisposition?.FileNameStar ??
                    contentDisposition?.FileName;
                if (serverFileName.IsNullOrEmpty())
                {
                    var finalUrl = headResponse.RequestMessage.RequestUri;
                    serverFileName = Path.GetFileName(finalUrl.LocalPath);
                }
                var tempPath = Path.Combine(tempDir, serverFileName.Trim('"'));
                downloadedBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;
                long lastBytes = downloadedBytes;

                var finalPath = Path.Combine(downloadTask.FullInstallPath, serverFileName.Trim('"'));

                async Task DoFinalStep(string tempPath, string finalPath)
                {
                    if (!await LegendaryLibrary.Instance.CommonHelpers.IsDirectoryWritable(Path.GetDirectoryName(finalPath)))
                    {
                        var roboCopyArgs = new List<string?>()
                        {
                            Path.GetDirectoryName(tempPath),
                            Path.GetDirectoryName(finalPath),
                            Path.GetFileName(tempPath),
                            "/R:3",
                            "/COPYALL"
                        };
                        var roboCopyCmd = Cli.Wrap("robocopy")
                                             .WithArguments(roboCopyArgs!);
                        var proc = ProcessStarter.StartProcess("robocopy", roboCopyCmd.Arguments, true);
                        proc.WaitForExit();
                    }
                    else
                    {
                        File.Move(tempPath, finalPath);
                    }
                }

                if (totalSize > 0 && downloadedBytes >= totalSize)
                {
                    DoFinalStep(tempPath, finalPath);
                    downloadTask.DownloadedBytes = downloadedBytes;
                    downloadTask.Status = UnifiedDownloadStatus.Completed;
                    return;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (downloadedBytes > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(downloadedBytes, null);
                }

                var speedStopwatch = Stopwatch.StartNew();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, downloadTask.GracefulCts.Token);
                response.EnsureSuccessStatusCode();

                using var networkStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                int bytesRead = 0;
                long totalNetWorkBytes = downloadedBytes;
                long totalDiskBytes = downloadedBytes;
                long lastNetWorkBytes = downloadedBytes;
                long lastDiskBytes = downloadedBytes;

                byte[] buffer = new byte[bufferSize];
                FileMode fileMode = downloadedBytes > 0 ? FileMode.Append : FileMode.Create;

                using (var tempFs = new FileStream(tempPath, fileMode, FileAccess.Write, FileShare.Read, bufferSize, FileOptions.Asynchronous))
                {
                    while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, downloadTask.GracefulCts.Token).ConfigureAwait(false)) > 0)
                    {
                        totalNetWorkBytes += bytesRead;

                        await tempFs.WriteAsync(buffer, 0, bytesRead, downloadTask.GracefulCts.Token).ConfigureAwait(false);

                        totalDiskBytes += bytesRead;

                        if (speedStopwatch.ElapsedMilliseconds >= 900)
                        {
                            var elapsed = speedStopwatch.Elapsed;
                            double seconds = elapsed.TotalSeconds;

                            if (seconds > 0)
                            {
                                long deltaNet = totalNetWorkBytes - lastNetWorkBytes;
                                downloadTask.DownloadSpeedBytes = deltaNet / seconds;

                                long deltaDisk = totalDiskBytes - lastDiskBytes;
                                downloadTask.DiskWriteSpeedBytes = deltaDisk / seconds;

                                downloadTask.DownloadedBytes = totalDiskBytes;

                                long currentPercentProgress = 0;
                                if (totalSize > 0)
                                {
                                    currentPercentProgress = totalDiskBytes / totalSize * 100;
                                }
                                downloadTask.Progress = currentPercentProgress;

                                downloadTask.Elapsed = totalStopwatch.Elapsed;

                                if (totalSize > 0)
                                {
                                    if (downloadTask.DownloadSpeedBytes > 0)
                                    {
                                        double remaining = (totalSize - totalDiskBytes) / downloadTask.DownloadSpeedBytes;
                                        downloadTask.Eta = (remaining < TimeSpan.MaxValue.TotalSeconds)
                                            ? TimeSpan.FromSeconds(remaining)
                                            : TimeSpan.MaxValue;
                                    }
                                    else
                                    {
                                        downloadTask.Eta = TimeSpan.MaxValue;
                                    }
                                }
                            }

                            lastNetWorkBytes = totalNetWorkBytes;
                            lastDiskBytes = totalDiskBytes;
                            speedStopwatch.Restart();
                        }
                    }
                }

                downloadTask.DiskWriteSpeedBytes = 0;
                downloadTask.DownloadSpeedBytes = 0;

                downloadTask.GracefulCts.Token.ThrowIfCancellationRequested();

                await DoFinalStep(tempPath, finalPath);

                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {

                }
                downloadTask.DownloadedBytes = totalDiskBytes;
                long newCurrentPercentProgress = 0;
                if (downloadTask.DownloadSizeBytes > 0)
                {
                    newCurrentPercentProgress = totalDiskBytes / totalSize * 100;
                }
                downloadTask.Progress = newCurrentPercentProgress;
            }

            downloadTask.Elapsed = totalStopwatch.Elapsed;
            downloadTask.Status = UnifiedDownloadStatus.Completed;
            DateTimeOffset now = DateTime.UtcNow;
            downloadTask.CompletedTime = now.ToUnixTimeSeconds();
        }

        public async Task DownloadOtherThings(UnifiedDownload downloadTask)
        {
            var gameID = downloadTask.GameId;
            var wantedUnifiedTask = downloadTask;
            var forcefulInstallerCTS = wantedUnifiedTask.ForcefulCts;
            var gracefulInstallerCTS = wantedUnifiedTask.GracefulCts;
            await WaitUntilLegendaryCloses();
            var installCommand = new List<string>();
            var settings = LegendaryLibrary.GetSettings();
            var matchingPluginTask = LegendaryLibrary.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == gameID);

            var downloadProperties = matchingPluginTask.DownloadProperties;
            var gameTitle = wantedUnifiedTask.Name;

            double cachedDownloadSizeNumber = wantedUnifiedTask.DownloadSizeBytes;
            double newDownloadSizeNumber = 0;
            double downloadCache = 0;
            if (gameID == "eos-overlay")
            {
                var fullInstallPath = Path.Combine(downloadProperties.InstallPath, ".eos-overlay");
                if (downloadProperties.DownloadAction != DownloadAction.Install)
                {
                    fullInstallPath = downloadProperties.InstallPath;
                }
                wantedUnifiedTask.FullInstallPath = fullInstallPath;
                installCommand = ["-y", "eos-overlay"];
                if (downloadProperties.DownloadAction == DownloadAction.Update)
                {
                    installCommand.Add("update");
                }
                else
                {
                    installCommand.AddRange(["install", "--path", fullInstallPath]);
                }
            }
            else
            {
                installCommand = ["-y", "install", gameID];
                if (downloadProperties.InstallPath != "")
                {
                    installCommand.AddRange(["--base-path", downloadProperties.InstallPath]);
                }
                if (settings.PreferredCDN != "")
                {
                    installCommand.AddRange(["--preferred-cdn", settings.PreferredCDN]);
                }
                if (settings.NoHttps)
                {
                    installCommand.Add("--no-https");
                }
                if (downloadProperties.MaxWorkers != 0)
                {
                    installCommand.AddRange(["--max-workers", downloadProperties.MaxWorkers.ToString()]);
                }
                if (downloadProperties.MaxSharedMemory != 0)
                {
                    installCommand.AddRange(["--max-shared-memory", downloadProperties.MaxSharedMemory.ToString()]);
                }
                if (downloadProperties.EnableReordering)
                {
                    installCommand.Add("--enable-reordering");
                }
                if (downloadProperties.IgnoreFreeSpace)
                {
                    installCommand.Add("--ignore-free-space");
                }
                if (settings.ConnectionTimeout != 0)
                {
                    installCommand.AddRange(new[] { "--dl-timeout", settings.ConnectionTimeout.ToString() });
                }
                if (downloadProperties.DownloadAction == DownloadAction.Repair)
                {
                    installCommand.Add("--repair");
                }
                if (downloadProperties.DownloadAction == DownloadAction.Update)
                {
                    installCommand.Add("--update-only");
                }
                if (downloadProperties.ExtraContent is { Count: > 0 })
                {
                    foreach (var singleSelectedContent in downloadProperties.ExtraContent)
                    {
                        installCommand.Add("--install-tag=" + singleSelectedContent);
                    }
                    if (downloadProperties.DownloadAction == DownloadAction.Repair)
                    {
                        installCommand.Add("--reset-sdl");
                    }
                }
                installCommand.Add("--skip-dlcs");
            }

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
                            wantedUnifiedTask.Status = UnifiedDownloadStatus.Running;
                            break;
                        case StandardOutputCommandEvent stdOut:
                            if (downloadProperties.DownloadAction == DownloadAction.Repair)
                            {
                                var verificationProgressMatch = Regex.Match(stdOut.Text, @"Verification progress:.*\((\d.*%)");
                                if (verificationProgressMatch.Length >= 2)
                                {
                                    double progress = CommonHelpers.ToDouble(verificationProgressMatch.Groups[1].Value.Replace("%", ""));
                                    wantedUnifiedTask.Progress = progress;
                                }
                                var verificationFileProgressMatch = Regex.Match(stdOut.Text, @"Verifying large file \""(.*)""\: (\d.*%) \((\d+\.\d+)\/(\d+\.\d+) (\wiB)");
                                if (verificationFileProgressMatch.Length >= 2)
                                {
                                    string fileName = verificationFileProgressMatch.Groups[1].Value;
                                    string largeProgressPercent = verificationFileProgressMatch.Groups[2].Value;
                                    string readSize = CommonHelpers.FormatSize(CommonHelpers.ToDouble(verificationFileProgressMatch.Groups[3].Value), verificationFileProgressMatch.Groups[5].Value);
                                    string fullSize = CommonHelpers.FormatSize(CommonHelpers.ToDouble(verificationFileProgressMatch.Groups[4].Value), verificationFileProgressMatch.Groups[5].Value);
                                    wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonVerifyingLargeFile, new Dictionary<string, IFluentType> { ["fileName"] = (FluentString)fileName, ["progress"] = (FluentString)$"{largeProgressPercent} ({readSize}/{fullSize})" });
                                }
                                else if (stdOut.Text.Contains("Verification"))
                                {
                                    wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonVerifying);
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
                                    wantedUnifiedTask.DownloadSizeBytes = newDownloadSizeNumber;
                                    cachedDownloadSizeNumber = newDownloadSizeNumber;
                                }
                                downloadCache = cachedDownloadSizeNumber - newDownloadSizeNumber;
                            }
                            var installSizeMatch = Regex.Match(stdErr.Text, @"Install size: (\S+) (\wiB)");
                            if (installSizeMatch.Length >= 2)
                            {
                                double installSizeNumber = CommonHelpers.ToBytes(CommonHelpers.ToDouble(installSizeMatch.Groups[1].Value), installSizeMatch.Groups[2].Value);
                                wantedUnifiedTask.InstallSizeBytes = installSizeNumber;
                            }
                            var fullInstallPathMatch = Regex.Match(stdErr.Text, @"Install path: (\S+)");
                            if (fullInstallPathMatch.Length >= 2)
                            {
                                wantedUnifiedTask.FullInstallPath = fullInstallPathMatch.Groups[1].Value;
                            }
                            var progressMatch = Regex.Match(stdErr.Text, @"Progress: (\d.*%)");
                            if (progressMatch.Length >= 2)
                            {
                                if (downloadProperties.DownloadAction != DownloadAction.Update)
                                {
                                    wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDownloadingLabel);
                                }
                                else
                                {
                                    wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonDownloadingUpdate);
                                }
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.Elapsed = TimeSpan.Parse(elapsedMatch.Groups[1].Value);
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                wantedUnifiedTask.Eta = TimeSpan.Parse(ETAMatch.Groups[1].Value);
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+) (\wiB)");
                            if (downloadedMatch.Length >= 2)
                            {
                                double downloadedNumber = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadedMatch.Groups[1].Value), downloadedMatch.Groups[2].Value);
                                double totalDownloadedNumber = downloadedNumber + downloadCache;
                                wantedUnifiedTask.DownloadedBytes = totalDownloadedNumber;
                                double newProgress = totalDownloadedNumber / wantedUnifiedTask.DownloadSizeBytes * 100;
                                wantedUnifiedTask.Progress = newProgress;

                                if (totalDownloadedNumber == wantedUnifiedTask.DownloadSizeBytes)
                                {
                                    switch (downloadProperties.DownloadAction)
                                    {
                                        case DownloadAction.Install:
                                            wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation);
                                            break;
                                        case DownloadAction.Update:
                                            wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingUpdate);
                                            break;
                                        case DownloadAction.Repair:
                                            wantedUnifiedTask.Activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingRepair);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+) (\wiB)");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.DownloadSpeedBytes = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadSpeedMatch.Groups[1].Value), downloadSpeedMatch.Groups[2].Value);
                            }
                            var diskSpeedMatch = Regex.Match(stdErr.Text, @"Disk\t- (\S+) (\wiB)");
                            if (diskSpeedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.DiskWriteSpeedBytes = CommonHelpers.ToBytes(CommonHelpers.ToDouble(diskSpeedMatch.Groups[1].Value), diskSpeedMatch.Groups[2].Value);
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
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }));
                                }
                                else if (memoryErrorMessage != "")
                                {
                                    var memoryErrorMatch = Regex.Match(memoryErrorMessage, @"MemoryError: Current shared memory cache is smaller than required: (\S+) MiB < (\S+) MiB");
                                    var gameErrorFluentArgs = new Dictionary<string, IFluentType>
                                    {
                                        ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.LegendaryMemoryError, new Dictionary<string, IFluentType> { ["currentMemory"] = (FluentString)$"{memoryErrorMatch.Groups[1]} MB", ["requiredMemory"] = (FluentString)$"{memoryErrorMatch.Groups[2]} MB" })
                                    };
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, gameErrorFluentArgs));
                                }
                                else if (permissionErrorDisplayed)
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonPermissionError) }));
                                }
                                else if (diskSpaceErrorDisplayed)
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonNotEnoughSpace) }));
                                }
                                else
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                                }
                                wantedUnifiedTask.Status = UnifiedDownloadStatus.Error;
                            }
                            else
                            {
                                var installedAppList = LegendaryLauncher.GetInstalledAppList();
                                if (installedAppList != null)
                                {
                                    if (installedAppList.ContainsKey(gameID))
                                    {
                                        var installedGameInfo = installedAppList[gameID];
                                        Playnite.Game game = new();
                                        if (!installedGameInfo.Is_dlc || !installedGameInfo.Executable.IsNullOrEmpty())
                                        {
                                            game = playniteApi.Library.Games.First(item => item.LibraryId == LegendaryLibrary.PluginId && item.LibraryGameId == gameID);
                                            game.InstallDirectory = installedGameInfo.Install_path;
                                            //game.Version = installedGameInfo.Version;
                                            game.InstallSize = (ulong)installedGameInfo.Install_size;
                                            game.InstallState = InstallState.Installed;
                                            var playtimeSyncEnabled = false;
                                            if (downloadProperties.DownloadAction == DownloadAction.Repair)
                                            {
                                                playtimeSyncEnabled = LegendaryLibrary.GetSettings().SyncPlaytime;
                                                var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.LibraryGameId);
                                                if (gameSettings?.AutoSyncPlaytime != null)
                                                {
                                                    playtimeSyncEnabled = (bool)gameSettings.AutoSyncPlaytime;
                                                }
                                                if (playtimeSyncEnabled)
                                                {
                                                    var accountApi = new EpicAccountClient(playniteApi);
                                                    var playtimeItems = await accountApi.GetPlaytimeItems();
                                                    var playtimeItem = playtimeItems?.FirstOrDefault(x => x.artifactId == gameID);
                                                    if (playtimeItem != null)
                                                    {
                                                        game.PlayTime = (uint)playtimeItem.totalTime;
                                                    }
                                                }
                                            }
                                            // Some games need specific key in registry, otherwise they can't launch
                                            try
                                            {
                                                using var regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("com.epicgames.launcher", false);
                                                if (regKey == null)
                                                {
                                                    Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\com.epicgames.launcher");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                logger.Error($"Failed to create registry key for {gameTitle}. Error: {ex.Message}");
                                            }
                                            if (downloadProperties.InstallPrerequisites)
                                            {
                                                if (installedGameInfo.Prereq_info != null)
                                                {
                                                    var gameSettings = new GameSettings
                                                    {
                                                        InstallPrerequisites = true
                                                    };
                                                    var commonHelpers = LegendaryLibrary.Instance.CommonHelpers;
                                                    commonHelpers.SaveJsonSettingsToFile(gameSettings, "GamesSettings", gameID, true);
                                                }
                                            }
                                            await playniteApi.Library.Games.UpdateAsync(game);
                                        }
                                    }
                                }
                                wantedUnifiedTask.Status = UnifiedDownloadStatus.Completed;
                                wantedUnifiedTask.Progress = 100;
                                DateTimeOffset now = DateTime.UtcNow;
                                wantedUnifiedTask.CompletedTime = now.ToUnixTimeSeconds();
                            }
                            gracefulInstallerCTS?.Dispose();
                            forcefulInstallerCTS?.Dispose();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && (downloadTask.Status == UnifiedDownloadStatus.Canceled || downloadTask.Status == UnifiedDownloadStatus.Paused))
                {
                    if (downloadTask.Status == UnifiedDownloadStatus.Canceled)
                    {
                        await OnCancelDownload(downloadTask);
                    }
                }
                else
                {
                    logger.Debug($"An error occured during downloading {downloadTask.Name}: {ex.Message}");
                    downloadTask.Status = UnifiedDownloadStatus.Error;
                }
            }
            finally
            {

            }
        }


        public async Task OnCancelDownload(UnifiedDownload downloadTask)
        {
            var gameID = downloadTask.GameId;
            await WaitUntilLegendaryCloses();
            var matchingPluginTask = LegendaryLibrary.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == gameID);
            const int maxRetries = 5;
            int delayMs = 100;
            var resumeFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", gameID + ".resume");
            var repairFile = Path.Combine(LegendaryLauncher.ConfigPath, "tmp", gameID + ".repair");
            var tempDir = Path.Combine(downloadTask.FullInstallPath, ".Downloader_temp");
            if (!Directory.Exists(tempDir))
            {
                var tempFolderName = $"{downloadTask.GameId}_PlayniteLegendaryPlugin";
                tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "temp", tempFolderName);
            }

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                   
                    if (File.Exists(resumeFile))
                    {
                        File.Delete(resumeFile);
                    }

                    if (File.Exists(repairFile))
                    {
                        File.Delete(repairFile);
                    }
                    if (downloadTask.FullInstallPath != null && matchingPluginTask.DownloadProperties.DownloadAction == DownloadAction.Install)
                    {
                        if (Directory.Exists(downloadTask.FullInstallPath))
                        {
                            Directory.Delete(downloadTask.FullInstallPath, true);
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
            var matchingPluginTask = LegendaryLibrary.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == downloadTask.GameId);
            if (matchingPluginTask != null)
            {
                LegendaryLibrary.Instance.PluginDownloadData.Downloads.Remove(matchingPluginTask);
                LegendaryLibrary.Instance.SaveDownloadData();
            }
            return Task.CompletedTask;
        }

        public void OpenDownloadPropertiesWindow(UnifiedDownload selectedEntry)
        {
            var window = playniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
            });
            var matchingPluginTask = LegendaryLibrary.Instance.PluginDownloadData.Downloads.FirstOrDefault(t => t.GameId == selectedEntry.GameId);
            window.Title = selectedEntry.Name + " — " + LocalizationManager.Instance.GetString(LOC.CommonDownloadProperties);
            window.DataContext = matchingPluginTask;
            window.Content = new LegendaryDownloadProperties();
            window.Owner = playniteApi.GetLastActiveWindow();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

    }
}
