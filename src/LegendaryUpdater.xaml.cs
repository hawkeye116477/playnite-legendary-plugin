using LegendaryLibraryNS.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryUpdater.xaml
    /// </summary>
    public partial class LegendaryUpdater : UserControl
    {
        public Dictionary<string, Installed> UpdatesList => (Dictionary<string, Installed>)DataContext;
        private IPlayniteAPI playniteAPI = API.Instance;
        public LegendaryUpdater()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var gameUpdate in UpdatesList)
            {
                gameUpdate.Value.Title_for_updater = $"{gameUpdate.Value.Title.RemoveTrademarks()} {gameUpdate.Value.Version}";
            }
            UpdatesLB.ItemsSource = UpdatesList;
            UpdatesLB.SelectAll();
        }

        private void UpdatesLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBtn.IsEnabled = UpdatesLB.SelectedIndex != -1;
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdatesLB.Items.Count == UpdatesLB.SelectedItems.Count)
            {
                UpdatesLB.UnselectAll();
            }
            else
            {
                UpdatesLB.SelectAll();
            }
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdatesLB.SelectedItems.Count > 0)
            {
                LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                var downloadTasks = new List<Task>();
                foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, Installed>>().ToList())
                {
                    downloadTasks.Add(legendaryUpdateController.UpdateGame(selectedOption.Value.Title, selectedOption.Key, true));
                }
                if (downloadTasks.Count > 0)
                {
                    await Task.WhenAll(downloadTasks);
                }
            }
        }
    }
}
