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

        public string MinValue { get; private set; }
        public string MaxValue { get; private set; }
        public string axisCo { get; set; }
        public double step { get; set; }
        public int rounding { get; set; }
        public int expressionDigit { get; set; }

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
            sliderDigits.ValueChanged += sliderDigits_ValueChanged;
            // Wire TextChanged so we can validate/enforce per-mode rules as user types
            //MinValueBox.TextChanged += ValueBox_TextChanged;
            //MaxValueBox.TextChanged += ValueBox_TextChanged;
        }

        // Constructor with prefilled values
        public RangeDialog(RangeVM range) : this()
        {
            MinValueBox.Text = Convert.ToString(range.min);
            MaxValueBox.Text = Convert.ToString(range.max);
            this.Title = range.axis + " Range Settings";
            lblExpression.Text = "Expression (Use " + range.axis + " in Caps as the original value of the coordinate to generate the expression.)";
            axisCo = range.axis;
            txtStep.Text = Convert.ToString(range.step);
            if (range.rounding == 0)
            {
                btnR.IsChecked = true;
            }
            else if (range.rounding == 1)
            {
                btnN.IsChecked = true;
            }
            else if (range.rounding == 2)
            {
                btnE.IsChecked = true;
            }
            else if (range.rounding == 3)
            {
                btnO.IsChecked = true;
            }
            txtExpression.Text = range.expression;
            sliderDigits.Value = range.roundingDigits;
            sliderExpression.Value = range.expressionDigits;
            txtStep.Text = Convert.ToString(range.step);


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
                sliderDigits.Value = 0;
            }
            else if (btnE.IsChecked == true)
            {
                SelectedMode = RoundingMode.Even;
                sliderDigits.Visibility = Visibility.Collapsed;
                txtDigits.Visibility = Visibility.Collapsed;
                sliderDigits.Value = 0;


                txtStep.Text = "2";
                txtStep.CaretIndex = txtStep.Text.Length;
            }
            else if (btnO.IsChecked == true)
            {
                SelectedMode = RoundingMode.Odd;
                sliderDigits.Visibility = Visibility.Collapsed;
                txtDigits.Visibility = Visibility.Collapsed;
                sliderDigits.Value = 0;

                txtStep.Text = "2";
                txtStep.CaretIndex = txtStep.Text.Length;
            }


            ValidateDecimalPlaces(MinValueBox);
            ValidateDecimalPlaces(MaxValueBox);
            //ValidateDecimalPlaces(txtStep);
            // Re-validate current box contents immediately with the newly selected mode
            // (safe because ValueBox_TextChanged uses isUpdatingText guard)
            //ValueBox_TextChanged(MinValueBox, null);
            //ValueBox_TextChanged(MaxValueBox, null);
        }

        private void sliderDigits_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int digits = (int)e.NewValue;

            ApplyDigitFormatting(MinValueBox, digits);
            ApplyDigitFormatting(MaxValueBox, digits);
        }

        private void ApplyDigitFormatting(TextBox box, int digits)
        {
            if (string.IsNullOrWhiteSpace(box.Text))
                return;

            if (double.TryParse(box.Text, out double value))
            {
                string format;

                if (digits == 0)
                    format = "0"; // integer only
                else
                    format = "0." + new string('0', digits); // fixed decimals

                box.Text = value.ToString(format, CultureInfo.InvariantCulture);
                box.CaretIndex = box.Text.Length;
            }
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

            if (min >= max)
            {
                MessageBox.Show("Minimum value must be smaller than Maximum value.", "Invalid Range",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // validate step
            if (!double.TryParse(txtStep.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                     CultureInfo.InvariantCulture, out double stepTxt))
            {
                MessageBox.Show("Please enter a valid numeric step value.", "Invalid Input",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (stepTxt <= 0)
            {
                MessageBox.Show("Step value must be positive and cannot be 0.", "Invalid Step",
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

            MinValue = MinValueBox.Text;
            MaxValue = MaxValueBox.Text;
            Digits = (int)sliderDigits.Value;
            Expression = txtExpression.Text.Trim();
            step = Convert.ToDouble(txtStep.Text.Trim());
            rounding = (int)SelectedMode;
            expressionDigit = (int)sliderExpression.Value;

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
                    sliderDigits.Value = 0;
                    if (text.Contains("."))
                    {
                        textBox.Text = text.Split('.')[0]; // remove decimal part
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    return;
                }

                // === Even / Odd modes: integers only ===
                if (text.Contains("."))
                {
                    // strip decimal part entirely
                    textBox.Text = text.Split('.')[0];
                    textBox.CaretIndex = textBox.Text.Length;
                    text = textBox.Text;
                }

                if (int.TryParse(text, out int num))
                {
                    // Always force txtStep = 2 in Even/Odd mode
                    if (textBox.Name == "txtStep")
                    {
                        //textBox.Text = "2";
                        //textBox.CaretIndex = textBox.Text.Length;
                        return;
                    }

                    if (SelectedMode == RoundingMode.Even && num % 2 != 0)
                    {
                        sliderDigits.Value = 0;
                        // Snap to nearest even
                        num = (num > 0) ? num - 1 : num + 1;
                        textBox.Text = num.ToString(CultureInfo.InvariantCulture);
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    else if (SelectedMode == RoundingMode.Odd && num % 2 == 0)
                    {
                        sliderDigits.Value = 0;
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
            // ValidateDecimalPlaces(MinValueBox);
        }

        private void MaxValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //ValidateDecimalPlaces(MaxValueBox);
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

        private void MaxValueBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (SelectedMode == RoundingMode.Real)
            {
                ValidateDecimalPlaces(MaxValueBox);

                int digits = (int)sliderDigits.Value;

                ApplyDigitFormatting(MaxValueBox, digits);
            }
            else
                ValidateDecimalPlaces(MaxValueBox);
        }

        private void MinValueBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (SelectedMode == RoundingMode.Real)
            {
                ValidateDecimalPlaces(MinValueBox);

                int digits = (int)sliderDigits.Value;

                ApplyDigitFormatting(MinValueBox, digits);
            }
            else
                ValidateDecimalPlaces(MinValueBox);
        }

        private void txtStep_LostFocus(object sender, RoutedEventArgs e)
        {
            // if parse fails or step <= 0, reset to a sensible default (1)
            if (!double.TryParse(txtStep.Text, NumberStyles.Float | NumberStyles.AllowThousands,
                                 CultureInfo.InvariantCulture, out double step) || step <= 0)
            {
                MessageBox.Show("Step must be a positive number. Resetting to 1.", "Invalid Step",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                txtStep.Text = "1";
            }

            ValidateDecimalPlaces(txtStep);
        }
    }
}
