using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LegendaryLibraryNS.Models;

public partial class GameSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool? LaunchOffline { get; set; }

    [ObservableProperty]
    public partial bool? DisableGameVersionCheck { get; set; }

    [ObservableProperty]
    public partial List<string>? StartupArguments { get; set; }

    [ObservableProperty]
    public partial string? LanguageCode { get; set; }

    [ObservableProperty]
    public partial string? OverrideExe { get; set; }

    [ObservableProperty]
    public partial bool? AutoSyncSaves { get; set; }

    [ObservableProperty]
    public partial string CloudSaveFolder { get; set; } = "";

    [ObservableProperty]
    public partial bool? AutoSyncPlaytime { get; set; }

    [ObservableProperty]
    public partial bool InstallPrerequisites { get; set; }
}