using Playnite.SDK;
using Playnite.SDK.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CommonPlugin
{
    public static class CommonControllerHelpers
    {
        public static void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args, HashSet<Type> windowContents)
        {
            if (args.State == ControllerInputState.Pressed)
            {
                var openedWindows = Application.Current.Windows.OfType<Window>();
                foreach (var openedWindow in openedWindows)
                {
                    if (!openedWindow.IsActive)
                    {
                        continue;
                    }

                    if (windowContents.Contains(openedWindow.Content.GetType()))
                    {
                        var focusedElement = Keyboard.FocusedElement as FrameworkElement;
                        switch (args.Button)
                        {
                            case ControllerInput.A:
                                if (focusedElement is Button btn)
                                {
                                    var peer = new ButtonAutomationPeer(btn);

                                    if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider provider)
                                    {
                                        provider.Invoke();
                                    }
                                }
                                else if (focusedElement is RepeatButton repeatBtn)
                                {
                                    repeatBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                }
                                else if (focusedElement?.TemplatedParent is Expander expander)
                                {
                                    expander.IsExpanded = !expander.IsExpanded;
                                }
                                else if (focusedElement is CheckBox)
                                {
                                    var checkBoxFocused = focusedElement as CheckBox;
                                    checkBoxFocused.IsChecked = !checkBoxFocused.IsChecked;
                                }
                                else if (focusedElement is ComboBox)
                                {
                                    var comboBoxFocused = focusedElement as ComboBox;
                                    comboBoxFocused.IsDropDownOpen = !comboBoxFocused.IsDropDownOpen;
                                }
                                else if (focusedElement is ComboBoxItem)
                                {
                                    var parentComboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;
                                    parentComboBox.SelectedItem = parentComboBox.ItemContainerGenerator.ItemFromContainer(focusedElement);
                                    parentComboBox.IsDropDownOpen = false;
                                }
                                break;
                            case ControllerInput.B:
                                if (focusedElement is ComboBox)
                                {
                                    var comboBoxFocused = focusedElement as ComboBox;
                                    if (comboBoxFocused.IsDropDownOpen)
                                    {
                                        comboBoxFocused.IsDropDownOpen = false;
                                    }
                                }
                                else if (focusedElement is ComboBoxItem)
                                {
                                    var parentComboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;
                                    parentComboBox.IsDropDownOpen = false;
                                }
                                else
                                {
                                    openedWindow.Close();
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                    else if (openedWindow.Content is MessageCheckBoxDialog)
                    {
                        MessageCheckBoxDialog.HandleControllerInput(args.Button);
                    }
                }
            }
        }

        public static void UC_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var playniteApi = API.Instance;
            if (playniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                var focused = Keyboard.FocusedElement as UIElement;
                if (focused is TextBox)
                {
                    if (e.Key == Key.Left)
                    {
                        focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Right)
                    {
                        focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        e.Handled = true;
                    }
                }
                if (focused is CheckBox || focused is TabItem || focused is ComboBox)
                {
                    if (e.Key == Key.Up)
                    {
                        focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Down)
                    {
                        focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
