using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CommonPlugin
{
    /// <summary>
    /// Interaction logic for NumericInput.xaml
    /// </summary>
    public partial class NumericInput : UserControl
    {
        public NumericInput()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(nameof(MinValue), typeof(int), typeof(NumericInput));
        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(nameof(MaxValue), typeof(int), typeof(NumericInput));
        public static readonly DependencyProperty StepSizeProperty = DependencyProperty.Register(nameof(StepSize), typeof(int), typeof(NumericInput), new FrameworkPropertyMetadata(
        1));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(NumericInput), new FrameworkPropertyMetadata(
        "", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public int MinValue
        {
            get => (int)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }
        public int MaxValue
        {
            get => (int)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }
        public int StepSize
        {
            get => (int)GetValue(StepSizeProperty);
            set => SetValue(StepSizeProperty, value);
        }
        private string lastGoodValue;
        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private void NumericTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NumericTxt.Text != "")
            {
                if (int.TryParse(NumericTxt.Text, out int number))
                {
                    lastGoodValue = NumericTxt.Text;
                    if (number > MaxValue)
                    {
                        NumericTxt.Text = MaxValue.ToString();
                    }
                    if (number < MinValue)
                    {
                        NumericTxt.Text = MinValue.ToString();
                    }
                }
                else
                {
                    NumericTxt.Text = lastGoodValue;
                }
                NumericTxt.SelectionStart = NumericTxt.Text.Length;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            lastGoodValue = !NumericTxt.Text.IsNullOrEmpty() ? NumericTxt.Text : MinValue.ToString();
        }

        public void Increment()
        {
            int number = !NumericTxt.Text.IsNullOrEmpty() ? Convert.ToInt32(NumericTxt.Text) : Convert.ToInt32(lastGoodValue);
            if (number < MaxValue)
            {
                NumericTxt.Text = Convert.ToString(number + StepSize);
            }
            NumericTxt.Focus();
        }

        private void IncrementBtn_Click(object sender, RoutedEventArgs e)
        {
            Increment();
        }

        public void Decrement()
        {
            int number = !NumericTxt.Text.IsNullOrEmpty() ? Convert.ToInt32(NumericTxt.Text) : Convert.ToInt32(lastGoodValue);
            if (number > MinValue)
            {
                NumericTxt.Text = Convert.ToString(number - StepSize);
            }
            NumericTxt.Focus();
        }

        private void DecrementBtn_Click(object sender, RoutedEventArgs e)
        {
            Decrement();
        }

        private void NumericTxt_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                Increment();
            }

            if (e.Key == Key.Down)
            {
                Decrement();
            }
        }
    }
}
