﻿using LegendaryLibraryNS.Enums;
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
        public bool ImportInstalledGames { get; set; } = LegendaryLauncher.IsInstalled;
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
        public int MaxWorkers { get; set; } = LegendaryLauncher.DefaultMaxWorkers;
        public int MaxSharedMemory { get; set; } = LegendaryLauncher.DefaultMaxSharedMemory;
        public bool EnableReordering { get; set; } = false;
        public int AutoClearCache { get; set; } = (int)ClearCacheTime.Never;
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
            get => new RelayCommand<object>(async (a) =>
            {
                await Login();
            });
        }

        public LegendaryLibrarySettingsViewModel(LegendaryLibrary library, IPlayniteAPI api) : base(library, api)
        {
            Settings = LoadSavedSettings() ?? new LegendaryLibrarySettings();
        }

        private async Task Login()
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
}
