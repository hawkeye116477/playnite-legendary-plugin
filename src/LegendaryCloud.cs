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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryCloud
    {
        internal static async Task<string> CalculateGameSavesPath(string gameName, string gameID, string gameInstallDir, bool skipRefreshingMetadata = true)
        {
            string cloudSaveFolder = "";
            var playniteAPI = LegendaryLibrary.PlayniteApi;
            GlobalProgressOptions metadataProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteProgressMetadata), false);

            var manifest = new LegendaryGameInfo.Rootobject();
            var gameData = new LegendaryGameInfo.Game
            {
                Title = gameName,
                App_name = gameID
            };
            await playniteAPI.Dialogs.ShowAsyncBlockingProgressAsync(metadataProgressOptions, async (a) =>
            {
                manifest = await LegendaryLauncher.GetGameInfo(gameData, skipRefreshingMetadata);
            });

            if (manifest.Game != null)
            {
                cloudSaveFolder = manifest.Game.Cloud_save_folder;
            }
            if (!cloudSaveFolder.IsNullOrEmpty())
            {
                var clientApi = new EpicAccountClient(playniteAPI);
                var userData = clientApi.LoadTokens();
                if (!userData.account_id.IsNullOrEmpty())
                {
                    var pathVariables = new Dictionary<string, string>
                    {
                        { "{installdir}", gameInstallDir },
                        { "{epicid}",  userData.account_id },
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


        internal static async Task SyncGameSaves(Playnite.Game game, CloudSyncAction cloudSyncAction, bool force = false, bool manualSync = false, bool skipRefreshingMetadata = true, string cloudSaveFolder = "")
        {
            var playniteAPI = LegendaryLibrary.PlayniteApi;
            var cloudSyncEnabled = LegendaryLibrary.GetSettings().SyncGameSaves;
            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(game.LibraryGameId);
            bool errorDisplayed = false;
            bool loginErrorDisplayed = false;
            if (gameSettings?.AutoSyncSaves != null)
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
                        if (installedList.ContainsKey(game.LibraryGameId))
                        {
                            var installedGame = installedList[game.LibraryGameId];
                            if (!installedGame.Save_path.IsNullOrEmpty())
                            {
                                cloudSaveFolder = installedGame.Save_path;
                            }
                        }
                    }
                    if (cloudSaveFolder == "" || !Directory.Exists(cloudSaveFolder))
                    {
                        cloudSaveFolder = await CalculateGameSavesPath(game.Name, game.LibraryGameId, game.InstallDirectory, skipRefreshingMetadata);
                    }
                }
                if (cloudSaveFolder != null)
                {
                    if (Directory.Exists(cloudSaveFolder))
                    {
                        var logger = LogManager.GetLogger();
                        GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonSyncing, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }), false);

                        await playniteAPI.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async (a) =>
                        {
                            a.SetCrrentProgressValue(100);
                            a.SetCrrentProgressValue(0);
                            var cloudArgs = new List<string>();
                            cloudArgs.AddRange(new[] { "-y", "sync-saves", game.LibraryGameId });
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
                            cloudArgs.AddRange(new[] { "--save-path", cloudSaveFolder });
                            var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                                         .WithArguments(cloudArgs)
                                         .AddCommandToLog();
                            await foreach (var cmdEvent in cmd.ListenAsync())
                            {
                                switch (cmdEvent)
                                {
                                    case StartedCommandEvent started:
                                        a.SetCrrentProgressValue(1);
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
                                            logger.Debug("[Legendary] " + stdErr.ToString());
                                        }
                                        break;
                                    case ExitedCommandEvent exited:
                                        a.SetCrrentProgressValue(100);
                                        if (exited.ExitCode != 0 || errorDisplayed)
                                        {
                                            if (loginErrorDisplayed)
                                            {
                                                await playniteAPI.Dialogs.ShowErrorMessageAsync($"{LocalizationManager.Instance.GetString(LOC.CommonSyncError, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name })} {LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired)}.");
                                            }
                                            else
                                            {
                                                await playniteAPI.Dialogs.ShowErrorMessageAsync($"{LocalizationManager.Instance.GetString(LOC.CommonSyncError, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name })} {LocalizationManager.Instance.GetString(LOC.CommonCheckLog)}");
                                            }
                                        }
                                        break;
                                    default:
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
