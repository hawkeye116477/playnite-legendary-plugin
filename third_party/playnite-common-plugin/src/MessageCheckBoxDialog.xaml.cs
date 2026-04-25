using Playnite;
using System;
using System.Windows;
using System.Windows.Controls;


namespace CommonPlugin
{
    public class MessageDialogSettings
    {
        public bool CheckboxChecked { get; set; }
        public bool Result { get; set; }
    }

    /// <summary>
    /// Interaction logic for MessageCheckBoxDialog.xaml
    /// </summary>
    public partial class MessageCheckBoxDialog : UserControl
    {
        private IPlayniteApi PlayniteApi { get; set; }
        public string MessageText { get; set; }
        public string CheckBoxText { get; set; }
        public bool ShowOkBtn { get; set; }
        public bool ShowYesBtn { get; set; }
        public bool ShowNoBtn { get; set; }
        public MessageBoxImage DisplayIcon { get; set; }
        public MessageDialogSettings DialogSettings { get; set; }

        public MessageCheckBoxDialog(IPlayniteApi playniteApi)
        {
            this.PlayniteApi = playniteApi;
        }

        public MessageCheckBoxDialog(string messageBoxText, string checkBoxText, MessageBoxButton button, MessageBoxImage icon, MessageDialogSettings messageDialogSettings)
        {
            InitializeComponent();
            DialogSettings = messageDialogSettings;
            switch (button)
            {
                case MessageBoxButton.OK:
                    OkBtn.Content = LocalizationManager.Instance.GetString("third-party-playnite-ok-label");
                    ShowOkBtn = true;
                    OkBtn.IsDefault = true;
                    OkBtn.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    YesBtn.Content = LocalizationManager.Instance.GetString("third-party-playnite-yes-label");
                    NoBtn.Content = LocalizationManager.Instance.GetString("third-party-playnite-no-label");
                    ShowYesBtn = true;
                    YesBtn.Focus();
                    YesBtn.IsDefault = true;
                    ShowNoBtn = true;
                    NoBtn.IsCancel = true;
                    break;
                default:
                    OkBtn.Content = LocalizationManager.Instance.GetString("third-party-playnite-ok-label");
                    ShowOkBtn = true;
                    OkBtn.Focus();
                    OkBtn.IsDefault = true;
                    break;
            }
            if (icon != MessageBoxImage.None)
            {
                ViewIcon.Visibility = Visibility.Visible;
            }
            if (messageBoxText?.StartsWith("LOC", StringComparison.Ordinal) == true)
            {
                MessageText = LocalizationManager.Instance.GetString(messageBoxText);
            }
            else
            {
                MessageText = messageBoxText;
            }
            if (checkBoxText?.StartsWith("LOC", StringComparison.Ordinal) == true)
            {
                CheckBoxText = LocalizationManager.Instance.GetString(checkBoxText);
            }
            else
            {
                CheckBoxText = checkBoxText;
            }
            DisplayIcon = icon;
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.DialogResult = true;
        }

        private void YesBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.DialogResult = true;
        }

        private void NoBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.DialogResult = false;
        }

        private void Chk_Checked(object sender, RoutedEventArgs e)
        {
            DialogSettings.CheckboxChecked = (bool)Chk.IsChecked!;
        }

        public MessageDialogSettings ShowMessage(string title, string message, string checkBoxText, MessageBoxButton buttonType, MessageBoxImage icon)
        {
            MessageDialogSettings messageDialogSettings = new MessageDialogSettings();
            Window window = PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
                ShowCloseButton = false,
                ShowMinimizeButton = false,
            });
            if (PlayniteApi.AppInfo.Mode == AppMode.Fullscreen)
            {
                window.Background = (System.Windows.Media.Brush)Application.Current?.TryFindResource("ControlBackgroundBrush")!;
            }
            window.Title = title;
            window.Owner = PlayniteApi.GetLastActiveWindow();
            window.Content = new MessageCheckBoxDialog(message, checkBoxText, buttonType, icon, messageDialogSettings);
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            messageDialogSettings.Result = (bool)result!;
            return messageDialogSettings;
        }
    }
}
