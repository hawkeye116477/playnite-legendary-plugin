using System.IO;
using CommonPlugin;
using CommunityToolkit.Mvvm.ComponentModel;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite;

namespace LegendaryLibraryNS;

public partial class LegendaryGameSettingsViewModel : ObservableObject
{
    public Game Game { get; set; }
    private CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;

    [ObservableProperty]
    public partial GameSettings ChosenGameSettings { get; set; }

    [ObservableProperty]
    public partial string StartupArgumentsTxt { get; set; }

    public LegendaryGameSettingsViewModel(Game game)
    {
        Game = game;
        ChosenGameSettings = LoadGameSettings(game.LibraryGameId!, true);
        if (ChosenGameSettings.StartupArguments is { Count: > 0 })
        {
            StartupArgumentsTxt = string.Join(" ",
                ChosenGameSettings.StartupArguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        }
        else
        {
            StartupArgumentsTxt = "";
        }
    }

    public static GameSettings LoadGameSettings(string gameId, bool init = false)
    {
        var playniteApi = LegendaryLibrary.PlayniteApi;
        var gameSettings = new GameSettings();
        var gameSettingsFile = Path.Combine(playniteApi.UserDataDir, "GamesSettings", $"{gameId}.json");
        if (File.Exists(gameSettingsFile) &&
            Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(gameSettingsFile), out GameSettings? savedGameSettings))
        {
            if (savedGameSettings != null)
            {
                gameSettings = savedGameSettings;
            }
        }

        if (init)
        {
            var globalSettings = LegendaryLibrary.GetSettings();
            gameSettings.LaunchOffline ??= globalSettings.LaunchOffline;

            if (gameSettings.DisableGameVersionCheck == null
                && globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
            {
                gameSettings.DisableGameVersionCheck = true;
            }

            gameSettings.AutoSyncSaves ??= globalSettings.SyncGameSaves;
            gameSettings.AutoSyncPlaytime ??= globalSettings.SyncPlaytime;
        }

        return gameSettings;
    }

    public GameSettings PrepareNewGameSettings()
    {
        var globalSettings = LegendaryLibrary.GetSettings();
        var newGameSettings = new GameSettings();
        if (ChosenGameSettings.LaunchOffline != globalSettings!.LaunchOffline)
        {
            newGameSettings.LaunchOffline = ChosenGameSettings.LaunchOffline;
        }

        var globalDisableUpdates = globalSettings.GamesUpdatePolicy == UpdatePolicy.Never;

        if (ChosenGameSettings.DisableGameVersionCheck != globalDisableUpdates)
        {
            newGameSettings.DisableGameVersionCheck = ChosenGameSettings.DisableGameVersionCheck;
        }

        if (StartupArgumentsTxt != "")
        {
            newGameSettings.StartupArguments = CommonHelpers.SplitArguments(StartupArgumentsTxt).ToList();
        }

        newGameSettings.LanguageCode = ChosenGameSettings.LanguageCode;
        newGameSettings.OverrideExe = ChosenGameSettings.OverrideExe;

        if (ChosenGameSettings.AutoSyncSaves != globalSettings.SyncGameSaves)
        {
            newGameSettings.AutoSyncSaves = ChosenGameSettings.AutoSyncSaves;
        }

        newGameSettings.CloudSaveFolder = ChosenGameSettings.CloudSaveFolder;

        if (ChosenGameSettings.AutoSyncPlaytime != globalSettings.SyncPlaytime)
        {
            newGameSettings.AutoSyncPlaytime = ChosenGameSettings.AutoSyncSaves;
        }

        newGameSettings.InstallPrerequisites = ChosenGameSettings.InstallPrerequisites;
        return newGameSettings;
    }

    public void Save()
    {
        var newGameSettings = PrepareNewGameSettings();
        if (newGameSettings.GetType().GetProperties().Any(p => p.GetValue(newGameSettings) != null))
        {
            commonHelpers.SaveJsonSettingsToFile(newGameSettings, "GamesSettings", Game.LibraryGameId!, true);
        }
    }
}