using LegendaryLibraryNS.Services;
using Playnite.Common;
using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryAlternativeAuthView.xaml
    /// </summary>
    public partial class LegendaryAlternativeAuthView : UserControl
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private ILogger logger = LogManager.GetLogger();
        public Window alternativeAuthWindow => Window.GetWindow(this);

        public LegendaryAlternativeAuthView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AuthLinkTxt.Text = EpicAccountClient.authCodeUrl;
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            AuthLinkTxt.Focus();
            AuthLinkTxt.SelectAll();
            Clipboard.SetText(AuthLinkTxt.Text);
        }

        private void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessStarter.StartUrl(EpicAccountClient.authCodeUrl);
        }

        private async void AuthBtn_Click(object sender, RoutedEventArgs e)
        {
            var clientApi = new EpicAccountClient(playniteAPI);
            if (AuthCodeTxt.Text != "")
            {
                try
                {
                    await clientApi.AuthenticateUsingAuthCode(AuthCodeTxt.Text.Trim());
                    alternativeAuthWindow.DialogResult = true;
                }
                catch (Exception ex) when (!Debugger.IsAttached)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.Legendary3P_EpicNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                    alternativeAuthWindow.DialogResult = false;
                }
                alternativeAuthWindow.Close();
            }
        }
    }
}
