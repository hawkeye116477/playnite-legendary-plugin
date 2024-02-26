using CliWrap;
using CliWrap.EventStream;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LegendaryLibraryNS
{
    public class LegendaryCloud
    {
        internal static string CalculateGameSavesPath(string gameName, string gameID, string gameInstallDir, bool skipRefreshingMetadata = true)
        {
            string cloudSaveFolder = "";
            var playniteAPI = API.Instance;
            GlobalProgressOptions metadataProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.Legendary3P_PlayniteProgressMetadata), false);

            var manifest = new LegendaryGameInfo.Rootobject();
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                manifest = await LegendaryLauncher.GetGameInfo(gameID, skipRefreshingMetadata);
            }, metadataProgressOptions);

            if (manifest.Game != null)
            {
                cloudSaveFolder = manifest.Game.Cloud_save_folder;
            }
            if (!cloudSaveFolder.IsNullOrEmpty())
            {
                if (File.Exists(LegendaryLauncher.TokensPath))
                {
                    var userData = Serialization.FromJson<OauthResponse>(FileSystem.ReadFileAsStringSafe(LegendaryLauncher.TokensPath));
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


        internal static void SyncGameSaves(string gameName, string gameID, string gameInstallDir, CloudSyncAction cloudSyncAction, bool manualSync = false, bool skipRefreshingMetadata = true, string cloudSaveFolder = "")
        {
            var cloudSyncEnabled = LegendaryLibrary.GetSettings().SyncGameSaves;
            var gameSettings = LegendaryGameSettingsView.LoadGameSettings(gameID);
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
                        if (installedList.ContainsKey(gameID))
                        {
                            var installedGame = installedList[gameID];
                            if (!installedGame.Save_path.IsNullOrEmpty())
                            {
                                cloudSaveFolder = installedGame.Save_path;
                            }
                        }
                    }
                    if (cloudSaveFolder == "" || !Directory.Exists(cloudSaveFolder))
                    {
                        cloudSaveFolder = CalculateGameSavesPath(gameName, gameID, gameInstallDir, skipRefreshingMetadata);
                    }
                }
                if (cloudSaveFolder != null)
                {
                    if (Directory.Exists(cloudSaveFolder))
                    {
                        var playniteAPI = API.Instance;
                        var logger = LogManager.GetLogger();
                        GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.LegendarySyncing).Format(gameName), false);
                        playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
                        {
                            a.ProgressMaxValue = 100;
                            a.CurrentProgressValue = 0;
                            var cloudArgs = new List<string>();
                            cloudArgs.AddRange(new[] { "-y", "sync-saves", gameID });
                            var skippedActivity = "--skip-upload";
                            if (cloudSyncAction == CloudSyncAction.Upload || cloudSyncAction == CloudSyncAction.ForceUpload)
                            {
                                skippedActivity = "--skip-download";
                            }
                            cloudArgs.Add(skippedActivity);
                            if (cloudSyncAction == CloudSyncAction.ForceDownload)
                            {
                                cloudArgs.Add("--force-download");
                            }
                            else if (cloudSyncAction == CloudSyncAction.ForceUpload)
                            {
                                cloudArgs.Add("--force-upload");
                            }
                            cloudArgs.AddRange(new[] { "--save-path", cloudSaveFolder });
                            var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                         .WithArguments(cloudArgs)
                                         .AddCommandToLog();
                            await foreach (var cmdEvent in cmd.ListenAsync())
                            {
                                switch (cmdEvent)
                                {
                                    case StartedCommandEvent started:
                                        a.CurrentProgressValue = 1;
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
                                        a.CurrentProgressValue = 100;
                                        if (exited.ExitCode != 0 || errorDisplayed)
                                        {
                                            if (loginErrorDisplayed)
                                            {
                                                playniteAPI.Dialogs.ShowErrorMessage($"{playniteAPI.Resources.GetString(LOC.LegendarySyncError).Format(gameName)} {ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)}.");
                                            }
                                            else
                                            {
                                                playniteAPI.Dialogs.ShowErrorMessage($"{playniteAPI.Resources.GetString(LOC.LegendarySyncError).Format(gameName)} {ResourceProvider.GetString(LOC.LegendaryCheckLog)}");
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }, globalProgressOptions);
                    }
                }
            }
        }
    }
}
