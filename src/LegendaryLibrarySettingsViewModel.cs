using LegendaryLibraryNS.Services;
using Playnite;
using Playnite.Commands;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LegendaryLibraryNS
{
    public class LegendaryLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = LegendaryLauncher.IsInstalledInDefaultPath;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public string SelectedLauncherPath { get; set; } = "";
        public bool UseCustomLauncherPath { get; set; } = false;
        public string GamesInstallationPath { get; set; } = LegendaryLauncher.DefaultGamesInstallationPath;
        public bool LaunchOffline { get; set; } = false;
        public List<string> OnlineList { get; set; } = new List<string>();
        public string PreferredCDN { get; set; } = LegendaryLauncher.DefaultPreferredCDN;
        public bool NoHttps { get; set; } = LegendaryLauncher.DefaultNoHttps;
        public int DoActionAfterDownloadComplete { get; set; } = (int)DownloadCompleteAction.Nothing;
        public bool SyncGameSaves { get; set; } = false;
    }

    public class LegendaryLibrarySettingsViewModel : PluginSettingsViewModel<LegendaryLibrarySettings, LegendaryLibrary>
    {
        public bool IsUserLoggedIn
        {
            get
            {
                return new EpicAccountClient(PlayniteApi, LegendaryLauncher.TokensPath).GetIsUserLoggedIn();
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public LegendaryLibrarySettingsViewModel(LegendaryLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating Epic settings from version 0.");
                    if (savedSettings.ImportUninstalledGames)
                    {
                        savedSettings.ConnectAccount = true;
                    }
                }

                savedSettings.Version = 1;
                Settings = savedSettings;
            }
            else
            {
                Settings = new LegendaryLibrarySettings { Version = 1 };
            }
        }

        private async void Login()
        {
            try
            {
                var clientApi = new EpicAccountClient(PlayniteApi, LegendaryLauncher.TokensPath);
                await clientApi.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.EpicNotLoggedInError), "");
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
    }

    public enum DownloadCompleteAction
    {
        Nothing,
        ShutDown,
        Reboot,
        Hibernate,
        Sleep
    };
}
