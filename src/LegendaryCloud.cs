using CliWrap;
using CliWrap.EventStream;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LegendaryLibraryNS
{
    public class LegendaryCloud
    {
        public static string CalculateGameSavesPath(string gameName, string gameID, string gameInstallDir)
        {
            string cloudSaveFolder = "";
            var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            var cacheInfoFile = Path.Combine(cacheInfoPath, gameID + ".json");
            bool correctJson = false;
            LegendaryGameInfo.Rootobject manifest = null;
            if (File.Exists(cacheInfoFile))
            {
                var metadataFile = Path.Combine(LegendaryLauncher.ConfigPath, "metadata", gameID + ".json");
                if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheInfoFile);
                    if (File.Exists(metadataFile))
                    {
                        File.Delete(metadataFile);
                    }
                }
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(cacheInfoFile), out manifest))
                {
                    if (manifest != null && manifest.Manifest != null)
                    {
                        correctJson = true;
                    }
                }
            }
            else
            {
                if (!Directory.Exists(cacheInfoPath))
                {
                    Directory.CreateDirectory(cacheInfoPath);
                }
            }
            if (!correctJson)
            {
                var playniteAPI = API.Instance;
                var logger = LogManager.GetLogger();
                GlobalProgressOptions metadataProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.Legendary3P_PlayniteProgressMetadata), false);
                playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
                {
                    a.ProgressMaxValue = 100;
                    a.CurrentProgressValue = 0;
                    var stdOutBuffer = new StringBuilder();
                    var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                 .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                 .WithArguments(new[] { "info", gameID, "--json" });
                    await foreach (var cmdEvent in cmd.ListenAsync())
                    {
                        switch (cmdEvent)
                        {
                            case StartedCommandEvent started:
                                a.CurrentProgressValue = 1;
                                break;
                            case StandardOutputCommandEvent stdOut:
                                stdOutBuffer.AppendLine(stdOut.Text);
                                break;
                            case StandardErrorCommandEvent stdErr:
                                logger.Debug("[Legendary] " + stdErr.ToString());
                                break;
                            case ExitedCommandEvent exited:
                                if (exited.ExitCode != 0)
                                {
                                    logger.Error("[Legendary] exit code: " + exited.ExitCode);
                                    playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(gameName));
                                    return;
                                }
                                else
                                {
                                    File.WriteAllText(cacheInfoFile, stdOutBuffer.ToString());
                                    manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>
                                    (FileSystem.ReadFileAsStringSafe(cacheInfoFile));
                                }
                                a.CurrentProgressValue = 100;
                                break;
                            default:
                                break;
                        }
                    }
                }, metadataProgressOptions);
            }
            cloudSaveFolder = manifest.Game.Cloud_save_folder;
            if (cloudSaveFolder != null)
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


        public static void SyncGameSaves(string gameName, string gameID, string gameInstallDir, bool download)
        {
            var cloudSyncEnabled = LegendaryLibrary.GetSettings().SyncGameSaves;
            var gamesSettings = LegendaryGameSettingsView.LoadSavedGamesSettings();
            var gameSettings = new GameSettings();
            if (gamesSettings.ContainsKey(gameID))
            {
                gameSettings = gamesSettings[gameID];
            }
            if (gameSettings?.AutoSyncSaves != null)
            {
                cloudSyncEnabled = (bool)gameSettings.AutoSyncSaves;
            }
            if (cloudSyncEnabled)
            {
                var cloudSaveFolder = CalculateGameSavesPath(gameName, gameID, gameInstallDir);
                if (gameSettings?.CloudSaveFolder != "")
                {
                    cloudSaveFolder = gameSettings.CloudSaveFolder;
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
                            var skippedActivity = "--skip-upload";
                            if (download == false)
                            {
                                skippedActivity = "--skip-download";
                            }
                            var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                         .WithArguments(new[] { "-y", "sync-saves", gameID, skippedActivity, "--save-path", cloudSaveFolder });
                            await foreach (var cmdEvent in cmd.ListenAsync())
                            {
                                switch (cmdEvent)
                                {
                                    case StartedCommandEvent started:
                                        a.CurrentProgressValue = 1;
                                        break;
                                    case StandardErrorCommandEvent stdErr:
                                        logger.Debug("[Legendary] " + stdErr.ToString());
                                        break;
                                    case ExitedCommandEvent exited:
                                        a.CurrentProgressValue = 100;
                                        if (exited.ExitCode != 0)
                                        {
                                            logger.Error("[Legendary] exit code: " + exited.ExitCode);
                                            playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.LegendarySyncError).Format(gameName));
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
