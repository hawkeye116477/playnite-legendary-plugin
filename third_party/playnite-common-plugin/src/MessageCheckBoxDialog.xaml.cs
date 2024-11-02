using Playnite.SDK;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


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
        private IPlayniteAPI playniteAPI = API.Instance;
        public string MessageText { get; set; }
        public string CheckBoxText { get; set; }
        public bool ShowOkBtn { get; set; }
        public bool ShowYesBtn { get; set; }
        public bool ShowNoBtn { get; set; }
        public MessageBoxImage DisplayIcon { get; set; }
        public MessageDialogSettings DialogSettings { get; set; }

        public MessageCheckBoxDialog()
        {
        }

        public MessageCheckBoxDialog(string messageBoxText, string checkBoxText, MessageBoxButton button, MessageBoxImage icon, MessageDialogSettings messageDialogSettings)
        {
            InitializeComponent();
            DialogSettings = messageDialogSettings;
            switch (button)
            {
                case MessageBoxButton.OK:
                    OkBtn.Content = ResourceProvider.GetString("LOCOKLabel");
                    ShowOkBtn = true;
                    OkBtn.IsDefault = true;
                    OkBtn.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    YesBtn.Content = ResourceProvider.GetString("LOCYesLabel");
                    NoBtn.Content = ResourceProvider.GetString("LOCNoLabel");
                    ShowYesBtn = true;
                    YesBtn.Focus();
                    YesBtn.IsDefault = true;
                    ShowNoBtn = true;
                    NoBtn.IsCancel = true;
                    break;
                default:
                    OkBtn.Content = ResourceProvider.GetString("LOCOKLabel");
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
                MessageText = ResourceProvider.GetString(messageBoxText);
            }
            else
            {
                MessageText = messageBoxText;
            }
            if (checkBoxText?.StartsWith("LOC", StringComparison.Ordinal) == true)
            {
                CheckBoxText = ResourceProvider.GetString(checkBoxText);
            }
            else
            {
                CheckBoxText = checkBoxText;
            }
            DisplayIcon = icon;
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

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = true;
        }

        private void YesBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = true;
        }

        private void NoBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = false;
        }

        private void Chk_Checked(object sender, RoutedEventArgs e)
        {
            DialogSettings.CheckboxChecked = (bool)Chk.IsChecked;
        }

        public static MessageDialogSettings ShowMessage(string title, string message, string checkBoxText, MessageBoxButton buttonType, MessageBoxImage icon)
        {
            MessageDialogSettings messageDialogSettings = new MessageDialogSettings();
            Window window = null;
            var playniteAPI = API.Instance;
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMaximizeButton = false,
                    ShowCloseButton = false,
                    ShowMinimizeButton = false,
                });
            }
            else
            {
                window = new Window
                {
                    Background = Brushes.DodgerBlue
                };
            }
            window.Title = title;
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.Content = new MessageCheckBoxDialog(message, checkBoxText, buttonType, icon, messageDialogSettings);
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            messageDialogSettings.Result = (bool)result;
            return messageDialogSettings;
        }
    }
}
