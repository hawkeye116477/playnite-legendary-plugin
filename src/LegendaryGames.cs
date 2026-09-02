using System.IO;
using CommonPlugin;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite;
using Playnite.Common;

namespace LegendaryLibraryNS;

public static class LegendaryGames
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    private const string PluginId = LegendaryLibrary.PluginId;
    private static readonly SpecImportableProperty PcSpecProperty = new("pc_windows");
    private static readonly IPlayniteApi PlayniteApi = LegendaryLibrary.PlayniteApi;

    private static Dictionary<string, ImportableGame> GetInstalledGames()
    {
        var games = new Dictionary<string, ImportableGame>();
        var appList = LegendaryLauncher.GetInstalledAppList();

        foreach (var d in appList)
        {
            var app = d.Value;

            if (app.App_name.StartsWith("UE_"))
            {
                continue;
            }

            // DLC
            if (app.Is_dlc && app.Executable.IsNullOrEmpty())
            {
                continue;
            }

            var installLocation = app.Install_path;
            var gameName = app.Title;
            if (installLocation.IsNullOrEmpty())
            {
                continue;
            }

            installLocation = Paths.FixSeparators(installLocation);
            if (!Directory.Exists(installLocation))
            {
                Logger.Error($"Epic game {gameName} installation directory {installLocation} not detected.");
                continue;
            }

            var game = new ImportableGame(gameName, PluginId, app.App_name)
            {
                Source = new IdImportableProperty("epic", "Epic"),
                InstallState = InstallState.Installed,
                InstallSize = (ulong)app.Install_size,
                InstallDirectory = installLocation,
                Platforms = [PcSpecProperty]
            };

            game.Name = game.Name.RemoveMarks();
            games.Add(game.GameId, game);
        }

        return games;
    }

    private static async Task<Dictionary<string, ImportableGame>> GetLibraryGames(CancellationToken cancelToken)
    {
        var cacheDir = LegendaryLibrary.GetCachePath("catalog");
        var games = new Dictionary<string, ImportableGame>();
        var accountApi = new EpicAccountClient(PlayniteApi);

        var assets = await accountApi.GetLibraryItems();
        if (assets.Count <= 0)
        {
            Logger.Warn("Found no assets on Epic accounts.");
        }

        var playtimeItems = await accountApi.GetPlaytimeItems();
        if (assets.Count > 0)
        {
            foreach (var gameAsset in assets.Where(a => a.Namespace != "ue"))
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                var cacheFile =
                    Paths.GetSafeFileName(
                        $"{gameAsset.Namespace}_{gameAsset.CatalogItemId}_{gameAsset.BuildVersion}.json");
                var appId = gameAsset.AppName;
                if (!appId.IsNullOrEmpty())
                {
                    cacheFile = $"{appId}.json";
                }

                cacheFile = Path.Combine(cacheDir, cacheFile);
                var catalogItem =
                    await accountApi.GetCatalogItem(gameAsset.Namespace, gameAsset.CatalogItemId, cacheFile);

                if (catalogItem == null)
                {
                    continue;
                }

                if (catalogItem.Categories?.Any(a => a.Path == "applications") != true)
                {
                    continue;
                }

                if (catalogItem.MainGameItem != null &&
                    catalogItem.Categories?.Any(a => a.Path == "addons/launchable") == false)
                {
                    continue;
                }

                if (catalogItem.Categories?.Any(a =>
                        a.Path == "digitalextras" || a.Path == "plugins" || a.Path == "plugins/engine") == true)
                {
                    continue;
                }

                if (LegendaryLibrary.GetSettings() is not { ImportEaLauncherGames: true })
                {
                    if (catalogItem.CustomAttributes?.ThirdPartyManagedApp != null &&
                        (catalogItem.CustomAttributes?.ThirdPartyManagedApp.Value.ToLower() == "the ea app" ||
                         catalogItem.CustomAttributes?.ThirdPartyManagedApp.Value.ToLower() == "origin"))
                    {
                        continue;
                    }
                }

                if (LegendaryLibrary.GetSettings() is not { ImportUbisoftLauncherGames: true })
                {
                    if (catalogItem.CustomAttributes?.PartnerLinkType is { Value: "ubisoft" })
                    {
                        continue;
                    }
                }

                var newGame = new ImportableGame((catalogItem.Title ?? "").RemoveMarks(), PluginId,
                    gameAsset.AppName)
                {
                    Source = new IdImportableProperty("epic", "Epic"),
                    Platforms = [PcSpecProperty]
                };
                var playtimeItem = playtimeItems?.FirstOrDefault(x => x.ArtifactId == gameAsset.AppName);
                if (playtimeItem != null)
                {
                    newGame.PlayTime = (uint)playtimeItem.TotalTime;
                }

                games.TryAdd(newGame.GameId, newGame);
            }
        }

        return games;
    }

    public static async Task<List<ImportableGame>> GetAllGames(CancellationToken cts)
    {
        const string importErrorMessageId = $"{PluginId}_libImportError";
        var allGames = new List<ImportableGame>();
        var installedGames = new Dictionary<string, ImportableGame>();
        Exception? importError = null;

        if (LegendaryLibrary.Instance.Settings is { ImportInstalledGames: true })
        {
            try
            {
                installedGames = GetInstalledGames();
                Logger.Debug($"Found {installedGames.Count} installed Epic games.");
                allGames.AddRange(installedGames.Values.ToList());
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import installed Epic games.");
                importError = e;
            }
        }

        if (LegendaryLibrary.Instance.Settings is { ConnectAccount: true })
        {
            try
            {
                var libraryGames = await GetLibraryGames(cts);
                Logger.Debug($"Found {libraryGames.Count} library Epic games.");

                if (!LegendaryLibrary.Instance.Settings.ImportUninstalledGames)
                {
                    libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.Key)).ToDictionary();
                }

                foreach (var game in libraryGames)
                {
                    if (installedGames.TryGetValue(game.Key, out var installed))
                    {
                        installed.PlayTime = game.Value.PlayTime;
                        installed.LastPlayedDate = game.Value.LastPlayedDate;
                        installed.Name = game.Value.Name;
                    }
                    else
                    {
                        allGames.Add(game.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import linked account Epic games details.");
                importError = e;
            }
        }

        if (importError != null)
        {
            PlayniteApi.Notifications.Add(new NotificationMessage(
                importErrorMessageId,
                LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLibraryImportError,
                    new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LegendaryLibrary.LibraryName }) +
                Environment.NewLine + importError.Message,
                NotificationSeverity.Error,
                async () => await PlayniteApi.MainView.OpenPluginSettingsAsync(PluginId)));
        }
        else
        {
            PlayniteApi.Notifications.Remove(importErrorMessageId);
        }

        return allGames;
    }

    public static string InstallationPath
    {
        get
        {
            var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Games");
            //var playniteApi = LegendaryLibrary.PlayniteApi;
            // if (playniteApi.ApplicationInfo.IsPortable)
            // {
            //     var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            //     installPath = Path.Combine(playniteDirectoryVariable, "Games");
            // }
            var savedSettings = LegendaryLibrary.GetSettings();
            var savedGamesInstallationPath = savedSettings.GamesInstallationPath;
            if (savedGamesInstallationPath != "")
            {
                installPath = savedGamesInstallationPath;
            }
            return installPath;
        }
    }

    public static bool DefaultPlaytimeSyncEnabled
    {
        get
        {
            var playniteApi = LegendaryLibrary.PlayniteApi;
            var playtimeSyncEnabled = false;
            // if (playniteApi.ApplicationSettings.PlaytimeImportMode != PlaytimeImportMode.Never)
            // {
            //     playtimeSyncEnabled = true;
            // }
            return playtimeSyncEnabled;
        }
    }

    public static void CompleteGameInstallation(string gameId)
    {
        var logger = LogManager.GetLogger();
        var gameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(gameId);
        var appList = LegendaryLauncher.GetInstalledAppList();
        if (appList.TryGetValue(gameId, out var installedGameInfo))
        {
            if (installedGameInfo.Prereq_info != null)
            {
                var prereq = installedGameInfo.Prereq_info;
                var prereqPath = "";
                if (!prereq.Path.IsNullOrEmpty())
                {
                    prereqPath = prereq.Path;
                }

                var prereqArgs = "";
                if (!prereq.Args.IsNullOrEmpty())
                {
                    prereqArgs = prereq.Args;
                }

                if (prereqPath != "")
                {
                    try
                    {
                        ProcessStarter.StartProcessWait(
                            Path.GetFullPath(Path.Combine(installedGameInfo.Install_path, prereqPath)),
                            prereqArgs,
                            "");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to launch prerequisites executable. Error: {ex.Message}");
                    }
                }
            }

            gameSettings.InstallPrerequisites = false;
            var commonHelpers = LegendaryLibrary.Instance.CommonHelpers;
            commonHelpers.SaveJsonSettingsToFile(gameSettings, "GamesSettings", gameId, true);
        }
    }

    public static void ClearCache()
    {
        var logger = LogManager.GetLogger();
        var cacheDirs = new List<string>
        {
            Path.Combine(LegendaryLibrary.PlayniteApi.UserDataDir, "cache"),
            Path.Combine(LegendaryLauncher.ConfigPath, "metadata")
        };
        foreach (var cacheDir in cacheDirs)
        {
            try
            {
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"An error occured during removing {cacheDir} directory");
            }
        }
    }

    public static void ClearSpecificGamesCache(List<string> gameIds)
    {
        var logger = LogManager.GetLogger();
        var cacheDirs = new List<string>
        {
            LegendaryLibrary.GetCachePath("info"),
            LegendaryLibrary.GetCachePath("sdl"),
            LegendaryLibrary.GetCachePath("updateinfo"),
            Path.Combine(LegendaryLauncher.ConfigPath, "metadata")
        };

        foreach (var cacheDir in cacheDirs)
        {
            if (Directory.Exists(cacheDir))
            {
                foreach (var file in Directory.EnumerateFiles(cacheDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (gameIds.Any(gameId => file.Contains(gameId)))
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"An error occured during removing {file} file");
                    }
                }
            }
        }
    }
}