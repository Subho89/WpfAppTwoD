using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfAppTwoD
{
    /// <summary>
    /// Interaction logic for RangeDialog.xaml
    /// </summary>
    public partial class RangeDialog : Window
    {
        public enum RoundingMode { Real, Integer, Even, Odd }

        public RoundingMode SelectedMode { get; private set; }
        public int Digits { get; private set; }
        public string Expression { get; private set; }

        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }
        public string axisCo { get; set; }
        public double step { get; set; }

        // guard to prevent re-entrancy when we update Text programmatically
        private bool isUpdatingText = false;

        public RangeDialog()
        {
            InitializeComponent();

            // defaults
            btnN.IsChecked = true; // Integer as default
            SelectedMode = RoundingMode.Integer;
            sliderDigits.Value = 3;
            sliderDigits.Visibility = Visibility.Collapsed;
            txtDigits.Visibility = Visibility.Collapsed;

            // Wire TextChanged so we can validate/enforce per-mode rules as user types
            //MinValueBox.TextChanged += ValueBox_TextChanged;
            //MaxValueBox.TextChanged += ValueBox_TextChanged;
        }

        // Constructor with prefilled values
        public RangeDialog(double min, double max, double step, string axis) : this()
        {
            MinValueBox.Text = min.ToString(CultureInfo.InvariantCulture);
            MaxValueBox.Text = max.ToString(CultureInfo.InvariantCulture);
            lblExpression.Text = "Expression (Use " + axis + " in Caps as the original value of the coordinate to generate the expression.)";
            axisCo = axis;
            txtStep.Text = step.ToString(CultureInfo.InvariantCulture);
        }

        private void Rounding_Checked(object sender, RoutedEventArgs e)
        {
            // don't reset contents automatically here (optional) — just update UI
            if (btnR.IsChecked == true)
            {
                SelectedMode = RoundingMode.Real;
                sliderDigits.Visibility = Visibility.Visible;
                txtDigits.Visibility = Visibility.Visible;
            }
            else if (btnN.IsChecked == true)
            {
                SelectedMode = RoundingMode.Integer;
                sliderDigits.Visibility = Visibility.Collapsed;
                txtDigits.Visibility = Visibility.Collapsed;
            }
            else if (btnE.IsChecked == true)
            {
                SelectedMode = RoundingMode.Even;
                sliderDigits.Visibility = Visibility.Collapsed;
                txtDigits.Visibility = Visibility.Collapsed;
            }
            else if (btnO.IsChecked == true)
            {
                SelectedMode = RoundingMode.Odd;
                sliderDigits.Visibility = Visibility.Collapsed;
                txtDigits.Visibility = Visibility.Collapsed;
            }

            // Re-validate current box contents immediately with the newly selected mode
            // (safe because ValueBox_TextChanged uses isUpdatingText guard)
            //ValueBox_TextChanged(MinValueBox, null);
            //ValueBox_TextChanged(MaxValueBox, null);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // validate min/max
            if (!double.TryParse(MinValueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double min) ||
                !double.TryParse(MaxValueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
            {
                MessageBox.Show("Please enter valid numeric min/max values.", "Invalid Input",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Additional final validations per mode (optional)
            if (SelectedMode == RoundingMode.Integer)
            {
                if (min % 1 != 0 || max % 1 != 0)
                {
                    MessageBox.Show("Min and Max must be integers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (SelectedMode == RoundingMode.Even)
            {
                if (((long)Math.Round(min)) % 2 != 0 || ((long)Math.Round(max)) % 2 != 0)
                {
                    MessageBox.Show("Min and Max must be even numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (SelectedMode == RoundingMode.Odd)
            {
                if (((long)Math.Round(min)) % 2 == 0 || ((long)Math.Round(max)) % 2 == 0)
                {
                    MessageBox.Show("Min and Max must be odd numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            MinValue = min;
            MaxValue = max;
            Digits = (int)sliderDigits.Value;
            Expression = txtExpression.Text.Trim();
            step = Convert.ToDouble(txtStep.Text.Trim());

            if (!string.IsNullOrEmpty(Expression))
            {
                if (!Expression.Contains(axisCo))
                {
                    MessageBox.Show("You have not used " + axisCo + " in the expression which reflects the original value of the coordinate");
                    return;
                }
            }

            DialogResult = true;
        }

        private void ValidateDecimalPlaces(TextBox textBox)
        {
            if (isUpdatingText) return;

            try
            {
                isUpdatingText = true;

                string text = textBox.Text;

                // Allow user to type "-" as intermediate input
                if (string.IsNullOrWhiteSpace(text) || text == "-")
                    return;

                // === Real mode: decimals allowed up to txtDigits ===
                if (SelectedMode == RoundingMode.Real)
                {
                    if (text.Contains("."))
                    {
                        int index = text.IndexOf('.');
                        int rCount = text.Length - index - 1;
                        int allowed = Convert.ToInt32(txtDigits.Text);

                        if (rCount > allowed)
                        {
                            textBox.Text = text.Remove(text.Length - 1);
                            textBox.CaretIndex = textBox.Text.Length;
                        }
                    }
                    return;
                }

                // === Integer mode: only integers allowed ===
                if (SelectedMode == RoundingMode.Integer)
                {
                    if (text.Contains("."))
                    {
                        textBox.Text = text.Split('.')[0]; // remove decimal
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    return;
                }

                // === Even / Odd mode: integers only ===
                if (text.Contains("."))
                {
                    // strip off decimal part entirely
                    textBox.Text = text.Split('.')[0];
                    textBox.CaretIndex = textBox.Text.Length;
                    text = textBox.Text;
                }

                if (int.TryParse(text, out int num))
                {
                    if (SelectedMode == RoundingMode.Even && num % 2 != 0)
                    {
                        // Snap to nearest even
                        num = (num > 0) ? num - 1 : num + 1;
                        textBox.Text = num.ToString(CultureInfo.InvariantCulture);
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    else if (SelectedMode == RoundingMode.Odd && num % 2 == 0)
                    {
                        // Snap to nearest odd
                        num = (num > 0) ? num + 1 : num - 1;
                        textBox.Text = num.ToString(CultureInfo.InvariantCulture);
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                }
            }
            finally
            {
                isUpdatingText = false;
            }
        }



        private void MinValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDecimalPlaces(MinValueBox);
        }

        private void MaxValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDecimalPlaces(MaxValueBox);
        }




        private static readonly Regex _regex = new Regex("^[0-9+-.]+$"); // Matches digits, +, -, .
        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Check if the new input, combined with the existing text, would be valid
            // This handles cases like pasting text or entering multiple signs/decimals
            string newText = (sender as TextBox).Text.Insert((sender as TextBox).CaretIndex, e.Text);

            if (!_regex.IsMatch(newText) ||
                (e.Text == "." && (sender as TextBox).Text.Contains(".")) || // Prevent multiple decimal points
                (e.Text == "+" && (sender as TextBox).Text.Contains("+")) || // Prevent multiple plus signs
                (e.Text == "-" && (sender as TextBox).Text.Contains("-")) || // Prevent multiple minus signs
                (e.Text == "+" && (sender as TextBox).CaretIndex != 0) || // + only at start
                (e.Text == "-" && (sender as TextBox).CaretIndex != 0)) // - only at start
            {
                e.Handled = true; // Prevent the input
            }
        }

        private static readonly Regex _numericRegex = new Regex(@"^[0-9]*(\.[0-9]*)?$");

        // Allow only digits and one decimal point
        private void TxtStep_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !_numericRegex.IsMatch(newText);
        }

        // Validate pasted text
        private void TxtStep_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string pasteText = (string)e.DataObject.GetData(typeof(string));
                if (!_numericRegex.IsMatch(pasteText))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}
