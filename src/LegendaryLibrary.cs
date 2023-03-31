using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    [LoadPlugin]
    public class LegendaryLibrary : LibraryPluginBase<LegendaryLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        internal readonly string TokensPath;
        public static LegendaryLibrary Instance { get; set; }
        public static bool LegendaryGameInstaller { get; internal set; }

        public LegendaryLibrary(IPlayniteAPI api) : base(
            "Legendary (Epic)",
            Guid.Parse("EAD65C3B-2F8F-4E37-B4E6-B3DE6BE540C6"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new LegendaryClient(),
            LegendaryLauncher.Icon,
            (_) => new LegendaryLibrarySettingsView(),
            api)
        {
            Instance = this;
            SettingsViewModel = new LegendaryLibrarySettingsViewModel(this, api);
            TokensPath = Path.Combine(GetPluginUserDataPath(), "tokens.json");
            LoadEpicLocalization();
        }

        public static LegendaryLibrarySettings GetSettings()
        {
            return Instance.SettingsViewModel.Settings;
        }

        internal Dictionary<string, GameMetadata> GetInstalledGames()
        {
            var games = new Dictionary<string, GameMetadata>();
            var appList = LegendaryLauncher.GetInstalledAppList();

            foreach (KeyValuePair<string, Installed> d in appList)
            {
                var app = d.Value;

                if (app.App_name.StartsWith("UE_"))
                {
                    continue;
                }

                // DLC
                if (app.Is_dlc)
                {
                    continue;
                }

                var installLocation = app.Install_path;
                var gameName = app?.Title ?? Path.GetFileName(installLocation);
                if (installLocation.IsNullOrEmpty())
                {
                    continue;
                }

                installLocation = Paths.FixSeparators(installLocation);
                if (!Directory.Exists(installLocation))
                {
                    logger.Error($"Epic game {gameName} installation directory {installLocation} not detected.");
                    continue;
                }

                var game = new GameMetadata()
                {
                    Source = new MetadataNameProperty("Epic"),
                    GameId = app.App_name,
                    Name = gameName,
                    Version = app.Version,
                    InstallDirectory = installLocation,
                    IsInstalled = true,
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                game.Name = game.Name.RemoveTrademarks();
                games.Add(game.GameId, game);
            }

            return games;
        }

        internal List<GameMetadata> GetLibraryGames(CancellationToken cancelToken)
        {
            var cacheDir = GetCachePath("catalogcache");
            var games = new List<GameMetadata>();
            var accountApi = new EpicAccountClient(PlayniteApi, TokensPath);
            var assets = accountApi.GetAssets();
            if (!assets?.Any() == true)
            {
                Logger.Warn("Found no assets on Epic accounts.");
            }

            var playtimeItems = accountApi.GetPlaytimeItems();
            foreach (var gameAsset in assets.Where(a => a.@namespace != "ue"))
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                var cacheFile = Paths.GetSafePathName($"{gameAsset.@namespace}_{gameAsset.catalogItemId}_{gameAsset.buildVersion}.json");
                cacheFile = Path.Combine(cacheDir, cacheFile);
                var catalogItem = accountApi.GetCatalogItem(gameAsset.@namespace, gameAsset.catalogItemId, cacheFile);
                if (catalogItem?.categories?.Any(a => a.path == "applications") != true)
                {
                    continue;
                }

                if (catalogItem?.categories?.Any(a => a.path == "dlc") == true)
                {
                    continue;
                }

                var newGame = new GameMetadata
                {
                    Source = new MetadataNameProperty("Epic"),
                    GameId = gameAsset.appName,
                    Name = catalogItem.title.RemoveTrademarks(),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                var playtimeItem = playtimeItems?.FirstOrDefault(x => x.artifactId == gameAsset.appName);
                if (playtimeItem != null)
                {
                    newGame.Playtime = playtimeItem.totalTime;
                }

                games.Add(newGame);
            }

            return games;
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameMetadata>();
            var installedGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;

            if (SettingsViewModel.Settings.ImportInstalledGames)
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

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames(args.CancelToken);
                    Logger.Debug($"Found {libraryGames.Count} library Epic games.");

                    if (!SettingsViewModel.Settings.ImportUninstalledGames)
                    {
                        libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.GameId)).ToList();
                    }

                    foreach (var game in libraryGames)
                    {
                        if (installedGames.TryGetValue(game.GameId, out var installed))
                        {
                            installed.Playtime = game.Playtime;
                            installed.LastActivity = game.LastActivity;
                            installed.Name = game.Name;
                        }
                        else
                        {
                            allGames.Add(game);
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
                    ImportErrorMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return allGames;
        }

        public string GetCachePath(string dirName)
        {
            return Path.Combine(GetPluginUserDataPath(), dirName);
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new LegendaryInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new LegendaryUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            var LegendaryLauncher = new LegendaryLauncher();
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            string gameLaunchCommand = "launch {0}";

            bool canRunOffline = false;
            var appList = LegendaryLauncher.GetInstalledAppList();
            foreach (KeyValuePair<string, Installed> d in appList)
            {
                var app = d.Value;
                if (app.App_name == args.Game.GameId)
                {
                    if (app.Can_run_offline && !SettingsViewModel.Settings.OnlineList.Contains(app.App_name))
                    {
                        canRunOffline = true;
                    }
                    break;
                }
            }
            if (GetSettings().LaunchOffline && canRunOffline)
            {
                gameLaunchCommand += " --offline";
            }

            yield return new AutomaticPlayController(args.Game)
            {
                Type = AutomaticPlayActionType.File,
                TrackingMode = TrackingMode.Directory,
                TrackingPath = args.Game.InstallDirectory,
                Path = LegendaryLauncher.ClientExecPath,
                Arguments = string.Format(gameLaunchCommand, args.Game.GameId),
                Name = ResourceProvider.GetString(LOC.EpicStartUsingClient).Format("Legendary")
            };
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new EpicMetadataProvider(PlayniteApi);
        }

        public void LoadEpicLocalization()
        {
            var currentLanguage = PlayniteApi.ApplicationSettings.Language;
            var dictionaries = Application.Current.Resources.MergedDictionaries;

            void loadString(string xamlPath)
            {
                ResourceDictionary res = null;
                try
                {
                    res = Xaml.FromFile<ResourceDictionary>(xamlPath);
                    res.Source = new Uri(xamlPath, UriKind.Absolute);
                    foreach (var key in res.Keys)
                    {
                        if (res[key] is string locString)
                        {
                            if (locString.IsNullOrEmpty())
                            {
                                res.Remove(key);
                            }
                        }
                        else
                        {
                            res.Remove(key);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to parse localization file {xamlPath}");
                    return;
                }
                dictionaries.Add(res);
            }

            var extraLocDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Localization\Epic");
            if (!Directory.Exists(extraLocDir))
            {
                return;
            }

            var enXaml = Path.Combine(extraLocDir, "en_US.xaml");
            if (!File.Exists(enXaml))
            {
                return;
            }

            if (currentLanguage != "en_US")
            {
                var langXaml = Path.Combine(extraLocDir, $"{currentLanguage}.xaml");
                if (File.Exists(langXaml))
                {
                    loadString(langXaml);
                }
                else
                {
                    loadString(enXaml);
                }
            }
            else
            {
                loadString(enXaml);
            }
        }
    }
}
