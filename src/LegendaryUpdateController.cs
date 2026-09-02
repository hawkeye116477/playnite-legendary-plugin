using System.IO;
using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite;

namespace LegendaryLibraryNS;

public class LegendaryUpdateController
{
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private static ILogger logger = LogManager.GetLogger();

    public async Task<Dictionary<string, UpdateInfo>> CheckGameUpdates(string gameTitle, string gameId, bool forceRefreshCache = false)
    {
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();

        if (gameId == "eos-overlay")
        {
            var cacheVersionFile = Path.Combine(LegendaryLauncher.ConfigPath, "overlay_version.json");
            var newVersion = "";
            if (File.Exists(cacheVersionFile))
            {
                if (File.GetLastWriteTime(cacheVersionFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheVersionFile);
                }
            }

            var correctJson = false;
            var overlayVersionInfo = new OverlayVersion.Rootobject();
            if (File.Exists(cacheVersionFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
                if (!content.IsNullOrWhiteSpace() &&
                    Serialization.TryFromJson(content, out OverlayVersion.Rootobject? newOverlayVersionInfo))
                {
                    if (newOverlayVersionInfo is { Data: not null })
                    {
                        overlayVersionInfo = newOverlayVersionInfo;
                        correctJson = true;
                    }
                }
            }

            if (!correctJson)
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithArguments(["status", "--json"])
                                   .WithEnvironmentVariables(LegendaryLauncher.GetDefaultEnvironmentVariables())
                                   .AddCommandToLog()
                                   .WithValidation(CommandResultValidation.None)
                                   .ExecuteBufferedAsync();
                var errorMessage = cmd.StandardError;
                if (cmd.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") ||
                    errorMessage.Contains("Error"))
                {
                    logger.Error("[Legendary]" + cmd.StandardError);
                }
                else
                {
                    var content = cmd.StandardOutput;
                    if (!content.IsNullOrWhiteSpace() &&
                        Serialization.TryFromJson(content, out OverlayVersion.Rootobject? newOverlayVersionInfo))
                    {
                        if (newOverlayVersionInfo is { Data.BuildVersion: not null })
                        {
                            overlayVersionInfo = newOverlayVersionInfo;
                            correctJson = true;
                        }
                    }
                }
            }

            if (correctJson)
            {
                newVersion = overlayVersionInfo.Data?.BuildVersion;
                if (newVersion == null)
                {
                    return gamesToUpdate;
                }

                var overlayInstallFile = Path.Combine(LegendaryLauncher.ConfigPath, "overlay_install.json");
                var overlayInstallContent = FileSystem.ReadFileAsStringSafe(overlayInstallFile);
                if (!overlayInstallContent.IsNullOrWhiteSpace() &&
                    Serialization.TryFromJson(overlayInstallContent, out Installed? overlayInstallInfo))
                {
                    if (overlayInstallInfo is { Version: not null })
                    {
                        if (overlayInstallInfo.Version != newVersion)
                        {
                            var result = await LegendaryLauncher.GetUpdateSizes("eos-overlay");
                            if (result.Download_size != 0)
                            {
                                var updateInfo = new UpdateInfo
                                {
                                    Version = newVersion,
                                    Title = gameTitle,
                                    Download_size = result.Download_size,
                                    Disk_size = result.Disk_size,
                                    Install_path = overlayInstallInfo.Install_path,
                                    Old_version = overlayInstallInfo.Version,
                                };
                                gamesToUpdate.Add(gameId, updateInfo);
                            }
                        }
                    }
                }
            }
            else
            {
                logger.Error($"An error occured during checking {gameTitle} updates.");
                gamesToUpdate.Add(gameId, new UpdateInfo
                {
                    Status = UpdateStatus.Error,
                    Title = gameTitle
                });
            }
            return gamesToUpdate;
        }

        var newGameData = new LegendaryGameInfo.Game
        {
            Title = gameTitle,
            App_name = gameId
        };
        var newGameInfo = await LegendaryLauncher.GetGameInfo(newGameData, false, true, forceRefreshCache);
        if (newGameInfo.Game != null)
        {
            var installedAppList = LegendaryLauncher.GetInstalledAppList();
            if (installedAppList.TryGetValue(gameId, out var oldGameInfo))
            {
                if (oldGameInfo.Version != newGameInfo.Game.Version)
                {
                    var resultUpdateSizes = await LegendaryLauncher.GetUpdateSizes(gameId);
                    if (resultUpdateSizes.Download_size != 0)
                    {
                        var updateInfo = new UpdateInfo
                        {
                            Version = newGameInfo.Game.Version,
                            Title = newGameInfo.Game.Title,
                            Download_size = resultUpdateSizes.Download_size,
                            Disk_size = resultUpdateSizes.Disk_size,
                            Install_path = oldGameInfo.Install_path,
                            Old_version = oldGameInfo.Version,
                        };
                        gamesToUpdate.Add(oldGameInfo.App_name, updateInfo);
                    }
                }

                // We need to also check for DLCs updates (see https://github.com/derrod/legendary/issues/506)
                if (newGameInfo.Game.Owned_dlc.Count > 0)
                {
                    foreach (var dlc in newGameInfo.Game.Owned_dlc)
                    {
                        if (!dlc.App_name.IsNullOrEmpty())
                        {
                            if (installedAppList.TryGetValue(dlc.App_name, out var oldDlcInfo))
                            {
                                var dlcData = new LegendaryGameInfo.Game
                                {
                                    Title = dlc.Title.RemoveMarks(),
                                    App_name = dlc.App_name
                                };
                                var newDlcInfo = await LegendaryLauncher.GetGameInfo(dlcData, false, true, forceRefreshCache);
                                if (newDlcInfo.Game != null)
                                {
                                    if (oldDlcInfo.Version != newDlcInfo.Game.Version)
                                    {
                                        var resultDlcUpdateSizes = await LegendaryLauncher.GetUpdateSizes(gameId);
                                        if (resultDlcUpdateSizes.Download_size != 0)
                                        {
                                            var updateDlcInfo = new UpdateInfo
                                            {
                                                Version = newDlcInfo.Game.Version,
                                                Title = newDlcInfo.Game.Title,
                                                Download_size = resultDlcUpdateSizes.Download_size,
                                                Disk_size = resultDlcUpdateSizes.Disk_size,
                                                Install_path = oldDlcInfo.Install_path,
                                                Old_version = oldDlcInfo.Version,
                                            };
                                            gamesToUpdate.Add(oldDlcInfo.App_name, updateDlcInfo);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            logger.Error($"An error occured during checking {gameTitle} updates.");
            gamesToUpdate.Add(gameId, new UpdateInfo
            {
                Status = UpdateStatus.Error,
                Title = gameTitle
            });
        }
        if (!gamesToUpdate.ContainsKey(gameId))
        {
            gamesToUpdate.Add(gameId, new UpdateInfo
            {
                Status = UpdateStatus.NotAvailable,
                Title = gameTitle
            });
        }

        return gamesToUpdate;
    }

    public async Task<Dictionary<string, UpdateInfo>> CheckAllGamesUpdates()
    {
        var appList = LegendaryLauncher.GetInstalledAppList();
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();
        foreach (var game in appList.Where(item => !item.Value.Is_dlc).OrderBy(item => item.Value.Title))
        {
            var gameId = game.Value.App_name;
            var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(gameId);
            var canUpdate = gameSettings.DisableGameVersionCheck != true;
            if (canUpdate)
            {
                LegendaryUpdateController legendaryUpdateController = new();
                var gameToUpdate = await legendaryUpdateController.CheckGameUpdates(game.Value.Title, gameId);
                if (gameToUpdate.Count > 0)
                {
                    foreach (var singleGame in gameToUpdate)
                    {
                        gamesToUpdate.Add(singleGame.Key, singleGame.Value);
                    }
                }
            }
        }

        if (LegendaryLauncher.IsEosOverlayInstalled)
        {
            var legendaryUpdateController = new LegendaryUpdateController();
            var overlayTitle = LocalizationManager.Instance.GetString(LOC.CommonOverlay,
                new Dictionary<string, IFluentType> { ["overlayName"] = (FluentString)"EOS" });
            var overlayToUpdate = await legendaryUpdateController.CheckGameUpdates(overlayTitle, "eos-overlay");
            if (overlayToUpdate.Count > 0)
            {
                gamesToUpdate.Add("eos-overlay", overlayToUpdate["eos-overlay"]);
            }
        }

        return gamesToUpdate;
    }

    public async Task UpdateGame(
        Dictionary<string, UpdateInfo> gamesToUpdate, string gameTitle = "", bool silently = false,
        DownloadProperties? downloadProperties = null)
    {
        var updateTasks = new List<DownloadManagerData.Download>();
        if (gamesToUpdate.Count > 0)
        {
            var canUpdate = true;
            if (canUpdate)
            {
                if (silently)
                {
                    playniteApi.Notifications.Add(new NotificationMessage("LegendaryGamesUpdates",
                        LocalizationManager.Instance.GetString(LOC.CommonGamesUpdatesUnderway), NotificationSeverity.Info));
                }

                var installedAppList = LegendaryLauncher.GetInstalledAppList();
                foreach (var gameToUpdate in gamesToUpdate)
                {
                    var settings = LegendaryLibrary.GetSettings();
                    var newDownloadProperties = new DownloadProperties
                    {
                        DownloadAction = DownloadAction.Update,
                        EnableReordering = settings is { EnableReordering: true },
                        MaxWorkers = settings!.MaxWorkers,
                        MaxSharedMemory = settings.MaxSharedMemory
                    };
                    if (downloadProperties != null)
                    {
                        newDownloadProperties = downloadProperties.GetClone();
                    }

                    newDownloadProperties.InstallPath = gameToUpdate.Value.Install_path;

                    var updateTask = new DownloadManagerData.Download
                    {
                        GameId = gameToUpdate.Key,
                        Name = gameToUpdate.Value.Title,
                        DownloadSizeNumber = gameToUpdate.Value.Download_size,
                        InstallSizeNumber = gameToUpdate.Value.Disk_size,
                        DownloadProperties = newDownloadProperties
                    };
                    if (gameToUpdate.Value.Install_path.IsNullOrEmpty())
                    {
                        logger.Warn($"No install path for {gameToUpdate.Value.Title}, skipping...");
                        continue;
                    }

                    updateTask.DownloadProperties.InstallPath = Directory.GetParent(gameToUpdate.Value.Install_path)?.FullName!;
                    updateTask.FullInstallPath = gameToUpdate.Value.Install_path;
                    if (installedAppList != null)
                    {
                        if (installedAppList.ContainsKey(gameToUpdate.Key))
                        {
                            var installedGameData = installedAppList[gameToUpdate.Key];
                            if (installedGameData.Install_tags.Count > 0)
                            {
                                updateTask.DownloadProperties.ExtraContent = installedGameData.Install_tags;
                            }

                            var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(updateTask);
                            foreach (var requiredTag in requiredTags)
                            {
                                updateTask.DownloadProperties.ExtraContent.AddMissing(requiredTag);
                            }
                        }
                    }

                    updateTasks.Add(updateTask);
                }

                if (updateTasks.Count > 0)
                {
                    var downloadLogic = (LegendaryDownloadLogic)LegendaryLibrary.Instance.UnifiedDownloadLogic;
                    await downloadLogic.AddTasks(updateTasks, silently);
                }
            }
        }
        else if (!silently)
        {
            await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), gameTitle);
        }
    }
}