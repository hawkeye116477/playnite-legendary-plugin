using CliWrap;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryCloud
    {
        internal static async Task<string> CalculateGameSavesPath(string gameName, string gameId, string gameInstallDir, bool skipRefreshingMetadata = true)
        {
            string cloudSaveFolder = "";
            var playniteApi = LegendaryLibrary.PlayniteApi;
            GlobalProgressOptions metadataProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteProgressMetadata), false);

            var manifest = new LegendaryGameInfo.Rootobject();
            var gameData = new LegendaryGameInfo.Game
            {
                Title = gameName,
                App_name = gameId
            };
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(metadataProgressOptions, async (_) =>
            {
                manifest = await LegendaryLauncher.GetGameInfo(gameData, skipRefreshingMetadata);
            });

            if (!string.IsNullOrEmpty(manifest.Game?.Cloud_save_folder))
            {
                cloudSaveFolder = manifest.Game.Cloud_save_folder;
            }
            if (!string.IsNullOrEmpty(cloudSaveFolder))
            {
                var clientApi = new EpicAccountClient(playniteApi);
                var userData = clientApi.LoadTokens();
                if (!string.IsNullOrEmpty(userData?.Account_id))
                {
                    var pathVariables = new Dictionary<string, string>
                    {
                        { "{installdir}", gameInstallDir },
                        { "{epicid}",  userData.Account_id },
                        { "{appdata}",  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
                        { "{userdir}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
                        { "{userprofile}", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
                        { "{usersavedgames}", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games") }
                    };
                    foreach (var pathVar in pathVariables)
                    {
                        if (cloudSaveFolder.Contains(pathVar.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            cloudSaveFolder = cloudSaveFolder.Replace(pathVar.Key, pathVar.Value, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    cloudSaveFolder = Path.GetFullPath(cloudSaveFolder);
                }
            }
            return cloudSaveFolder;
        }


        internal static async Task SyncGameSaves(Game game, CloudSyncAction cloudSyncAction, bool force = false, bool manualSync = false, bool skipRefreshingMetadata = true, string cloudSaveFolder = "")
        {
            var playniteApi = LegendaryLibrary.PlayniteApi;
            var cloudSyncEnabled = LegendaryLibrary.GetSettings() is { SyncGameSaves: true };
            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.LibraryGameId!);
            bool errorDisplayed = false;
            bool loginErrorDisplayed = false;
            if (gameSettings.AutoSyncSaves != null)
            {
                cloudSyncEnabled = (bool)gameSettings.AutoSyncSaves;
            }
            if (manualSync)
            {
                cloudSyncEnabled = true;
            }
            if (cloudSyncEnabled)
            {
                if (cloudSaveFolder == "")
                {
                    if (!gameSettings.CloudSaveFolder.IsNullOrEmpty())
                    {
                        cloudSaveFolder = gameSettings.CloudSaveFolder;
                    }
                    else
                    {
                        var installedList = LegendaryLauncher.GetInstalledAppList();
                        if (installedList.TryGetValue(game.LibraryGameId!, out var installedGame))
                        {
                            if (!string.IsNullOrEmpty(installedGame.Save_path))
                            {
                                cloudSaveFolder = installedGame.Save_path;
                            }
                        }
                    }
                    if (cloudSaveFolder == "" || !Directory.Exists(cloudSaveFolder))
                    {
                        cloudSaveFolder = await CalculateGameSavesPath(game.Name, game.LibraryGameId!, game.InstallDirectory!, skipRefreshingMetadata);
                    }
                }
                if (!cloudSaveFolder.IsNullOrEmpty())
                {
                    if (Directory.Exists(cloudSaveFolder))
                    {
                        var logger = LogManager.GetLogger();
                        GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonSyncing, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }), false);

                        await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async (a) =>
                        {
                            a.SetCurrentProgressValue(100);
                            a.SetCurrentProgressValue(0);
                            var cloudArgs = new List<string>();
                            cloudArgs.AddRange(["-y", "sync-saves", game.LibraryGameId!]);
                            var skippedActivity = "--skip-upload";
                            if (cloudSyncAction == CloudSyncAction.Upload)
                            {
                                skippedActivity = "--skip-download";
                            }
                            cloudArgs.Add(skippedActivity);
                            if (cloudSyncAction == CloudSyncAction.Download && force)
                            {
                                cloudArgs.Add("--force-download");
                            }
                            else if (cloudSyncAction == CloudSyncAction.Upload && force)
                            {
                                cloudArgs.Add("--force-upload");
                            }
                            cloudArgs.AddRange(["--save-path", cloudSaveFolder]);
                            var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                                         .WithArguments(cloudArgs)
                                         .AddCommandToLog();
                            await foreach (var cmdEvent in cmd.ListenAsync())
                            {
                                switch (cmdEvent)
                                {
                                    case StartedCommandEvent:
                                        a.SetCurrentProgressValue(1);
                                        break;
                                    case StandardErrorCommandEvent stdErr:
                                        var errorMessage = stdErr.Text;
                                        if (errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                                        {
                                            if (errorMessage.Contains("Failed to establish a new connection")
                                            || errorMessage.Contains("Log in failed")
                                            || errorMessage.Contains("Login failed")
                                            || errorMessage.Contains("No saved credentials"))
                                            {
                                                loginErrorDisplayed = true;
                                            }
                                            logger.Error($"[Legendary] {errorMessage}");
                                            errorDisplayed = true;
                                        }
                                        else if (errorMessage.Contains("WARNING"))
                                        {
                                            logger.Warn($"[Legendary] {errorMessage}");
                                        }
                                        else
                                        {
                                            logger.Debug("[Legendary] " + stdErr);
                                        }
                                        break;
                                    case ExitedCommandEvent exited:
                                        a.SetCurrentProgressValue(100);
                                        if (exited.ExitCode != 0 || errorDisplayed)
                                        {
                                            if (loginErrorDisplayed)
                                            {
                                                await playniteApi.Dialogs.ShowErrorMessageAsync($"{LocalizationManager.Instance.GetString(LOC.CommonSyncError, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name })} {LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired)}.");
                                            }
                                            else
                                            {
                                                await playniteApi.Dialogs.ShowErrorMessageAsync($"{LocalizationManager.Instance.GetString(LOC.CommonSyncError, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name })} {LocalizationManager.Instance.GetString(LOC.CommonCheckLog)}");
                                            }
                                        }
                                        break;
                                }
                            }
                        });
                    }
                }
            }
        }
    }
}
