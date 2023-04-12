using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryExcludeOnlineGames.xaml
    /// </summary>
    public partial class LegendaryExcludeOnlineGames : UserControl
    {
        public LegendaryExcludeOnlineGames()
        {
            InitializeComponent();
            Dispatcher.BeginInvoke((Action)(() =>
            {
                var offList = new List<KeyValuePair<string, string>>();
                var onList = new List<KeyValuePair<string, string>>();
                var appList = LegendaryLauncher.GetInstalledAppList();
                foreach (KeyValuePair<string, Models.Installed> d in appList)
                {
                    var app = d.Value;
                    if (LegendaryLibrary.GetSettings().OnlineList.Contains(app.App_name))
                    {
                        onList.Add(new KeyValuePair<string, string>(app.App_name, app.Title));
                    }
                    else if (!LegendaryLibrary.GetSettings().OnlineList.Contains(app.App_name) && app.Can_run_offline)
                    {
                        offList.Add(new KeyValuePair<string, string>(app.App_name, app.Title));
                    }
                }
                OfflineLB.ItemsSource = offList;
                OnlineLB.ItemsSource = onList;
            }));
        }

        private void OnlineBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedKeyValuePairs = OfflineLB.SelectedItems.Cast<KeyValuePair<string, string>>().ToList();
            var offListItems = OfflineLB.Items.Cast<KeyValuePair<string, string>>().ToList();
            var onListItems = OnlineLB.Items.Cast<KeyValuePair<string, string>>().ToList();
            foreach (var sel in selectedKeyValuePairs)
            {
                onListItems.Add(new KeyValuePair<string, string>(sel.Key, sel.Value));
                offListItems.Remove(new KeyValuePair<string, string>(sel.Key, sel.Value));
            }
            OfflineLB.ItemsSource = offListItems;
            OnlineLB.ItemsSource = onListItems;
        }

        private void OfflineBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedKeyValuePairs = OnlineLB.SelectedItems.Cast<KeyValuePair<string, string>>().ToList();
            var offListItems = OfflineLB.Items.Cast<KeyValuePair<string, string>>().ToList();
            var onListItems = OnlineLB.Items.Cast<KeyValuePair<string, string>>().ToList();
            foreach (var sel in selectedKeyValuePairs)
            {
                onListItems.Remove(new KeyValuePair<string, string>(sel.Key, sel.Value));
                offListItems.Add(new KeyValuePair<string, string>(sel.Key, sel.Value));
            }
            OfflineLB.ItemsSource = offListItems;
            OnlineLB.ItemsSource = onListItems;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            LegendaryLibrary.GetSettings().OnlineList = OnlineLB.Items.Cast<KeyValuePair<string, string>>().ToList().Select(a => a.Key).ToList();
            Window.GetWindow(this).Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
    }
}
