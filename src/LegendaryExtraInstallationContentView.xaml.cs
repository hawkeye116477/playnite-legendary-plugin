using LegendaryLibraryNS.Models;
using Playnite.SDK;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryExtraInstallationContentView.xaml
    /// </summary>
    public partial class LegendaryExtraInstallationContentView : UserControl
    {
        public LegendaryExtraInstallationContentView()
        {
            InitializeComponent();
            SetControlStyles();
        }

        private DownloadManagerData.Download ChosenGame
        {
            get => DataContext as DownloadManagerData.Download;
            set { }
        }

        private IPlayniteAPI playniteAPI = API.Instance;
        private bool uncheckedByUser = true;
        private bool checkedByUser = true;

        private void SetControlStyles()
        {
            var baseStyleName = "BaseTextBlockStyle";
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                baseStyleName = "TextBlockBaseStyle";
                Resources.Add(typeof(Button), new Style(typeof(Button), null));
            }

            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private async void LegendaryExtraInstallationContentUC_Loaded(object sender, RoutedEventArgs e)
        {
            Dictionary<string, LegendarySDLInfo> extraContentInfo = await LegendaryLauncher.GetExtraContentInfo(ChosenGame);
            var dlcs = extraContentInfo.Where(i => i.Value.Is_dlc).ToList();
            var sdls = extraContentInfo.Where(i => i.Value.Is_dlc == false).ToList();
            if (dlcs.Count > 1)
            {
                AllDlcsChk.Visibility = Visibility.Visible;
            }
            if (sdls.Count > 1)
            {
                AllOrNothingChk.Visibility = Visibility.Visible;
            }
            if (extraContentInfo.Count > 0)
            {
                ExtraContentLB.ItemsSource = extraContentInfo;
                ExtraContentSP.Visibility = Visibility.Visible;
                var selectedExtraContent = new Dictionary<string, LegendarySDLInfo>();
                var selectedDlcs = ChosenGame.downloadProperties.selectedDlcs;
                if (selectedDlcs != null && selectedDlcs.Count > 0)
                {
                    foreach (var selectedDlc in selectedDlcs)
                    {
                        var sdlInfo = new LegendarySDLInfo
                        {
                            Is_dlc = true,
                        };
                        selectedExtraContent.Add(selectedDlc.Key, sdlInfo);
                    }
                }
                var selectedSdls = ChosenGame.downloadProperties.extraContent;
                if (selectedSdls.Count > 0)
                {
                    foreach (var selectedSdl  in selectedSdls)
                    {
                        var sdlInfo = new LegendarySDLInfo
                        {
                            Is_dlc = false,
                        };
                        selectedExtraContent.Add(selectedSdl, sdlInfo);
                    }
                }
                if (selectedExtraContent.Count > 0)
                {
                    foreach (var singleSelectedExtraContent in selectedExtraContent)
                    {
                        var selectedItem = extraContentInfo.FirstOrDefault(i => i.Key == singleSelectedExtraContent.Key);
                        if (selectedItem.Key != null)
                        {
                            ExtraContentLB.SelectedItems.Add(selectedItem);
                        }
                    }
                }
            }
        }

        private async void ExtraContentLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedExtraContent = ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList();
            var selectedDLCs = selectedExtraContent.Where(i => i.Value.Is_dlc).ToList();
            var sdls = selectedExtraContent.Where(i => i.Value.Is_dlc == false).ToList();

            var selectedSdls = new List<string>();

            if (sdls.Count > 0)
            {
                foreach (var sdl in sdls)
                {
                    selectedSdls.AddMissing(sdl.Key);
                }
            }
            ChosenGame.downloadProperties.selectedDlcs = new Dictionary<string, DownloadManagerData.Download>();
            if (selectedDLCs.Count > 0)
            {
                foreach (var dlc in selectedDLCs)
                {
                    var dlcData = new LegendaryGameInfo.Game
                    {
                        Title = dlc.Value.Name,
                        App_name = dlc.Key
                    };
                    var dlcInstallData = new DownloadManagerData.Download
                    {
                        gameID = dlcData.App_name,
                        name = dlcData.Title,
                    };
                    ChosenGame.downloadProperties.selectedDlcs.Add(dlcData.App_name, dlcInstallData);
                }
            }

            var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(ChosenGame);
            if (requiredTags.Count > 0)
            {
                foreach (var requiredTag in requiredTags)
                {
                    selectedSdls.AddMissing(requiredTag);
                }
            }
            ChosenGame.downloadProperties.extraContent = selectedSdls;
            var gameData = new LegendaryGameInfo.Game
            {
                App_name = ChosenGame.gameID,
                Title = ChosenGame.name,
            };

            if (AllOrNothingChk.IsChecked == true && selectedExtraContent.Count() != ExtraContentLB.Items.Count)
            {
                uncheckedByUser = false;
                AllOrNothingChk.IsChecked = false;
                uncheckedByUser = true;
            }
            if (AllOrNothingChk.IsChecked == false && selectedExtraContent.Count() == ExtraContentLB.Items.Count)
            {
                checkedByUser = false;
                AllOrNothingChk.IsChecked = true;
                checkedByUser = true;
            }
            var allDLCs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(i => i.Value.Is_dlc).ToList();
            if (AllDlcsChk.IsChecked == true && selectedDLCs.Count() != allDLCs.Count)
            {
                uncheckedByUser = false;
                AllDlcsChk.IsChecked = false;
                uncheckedByUser = true;
            }
            if (AllDlcsChk.IsChecked == false && selectedDLCs.Count() == allDLCs.Count)
            {
                checkedByUser = false;
                AllDlcsChk.IsChecked = true;
                checkedByUser = true;
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private void AllDlcsChk_Checked(object sender, RoutedEventArgs e)
        {
            if (checkedByUser)
            {
                var dlcs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(x => x.Value.Is_dlc).ToList();
                foreach (var dlc in dlcs)
                {
                    ExtraContentLB.SelectedItems.Add(dlc);
                }
            }
        }

        private void AllOrNothingChk_Checked(object sender, RoutedEventArgs e)
        {
            if (checkedByUser)
            {
                ExtraContentLB.SelectAll();
            }
        }

        private void AllOrNothingChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (uncheckedByUser)
            {
                ExtraContentLB.SelectedItems.Clear();
            }
        }

        private void AllDlcsChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (uncheckedByUser)
            {
                var dlcs = ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(x => x.Value.Is_dlc).ToList();
                foreach (var dlc in dlcs)
                {
                    ExtraContentLB.SelectedItems.Remove(dlc);
                }
            }
        }
    }
}
