using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfAppTwoD
{
    public partial class MainWindow : Window
    {
        private double xMin = -10, xMax = 10, yMin = -10, yMax = 10;
        private double step = 1;

        private Ellipse pointer;
        private Border coordLabelBorder;
        private TextBlock coordLabelText;
        private double pointerRadius = 7;

        private bool isDragging = false;
        private Point dragStartMouse;
        private Point dragStartPointerCanvas;

        private double pointerX = 0;
        private double pointerY = 0;

        private Line guideLineX;
        private Line guideLineY;

        private readonly List<TextBlock> tickLabels = new();

        // 🔄 prevent recursive updates
        private bool isUpdatingUI = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                InitializeGraph();
                MovePointerTo(pointerX, pointerY, true);
            };

            this.SizeChanged += (s, e) =>
            {
                if (graphCanvas != null && pointer != null && coordLabelBorder != null)
                {
                    if (!graphCanvas.Children.Contains(pointer))
                        graphCanvas.Children.Add(pointer);

                    if (!graphCanvas.Children.Contains(coordLabelBorder))
                        graphCanvas.Children.Add(coordLabelBorder);

                    MovePointerTo(pointerX, pointerY, true);
                }
            };

            graphCanvas.Focus();

            // Hook UI events
            txtX.TextChanged += TxtXY_TextChanged;
            txtY.TextChanged += TxtXY_TextChanged;
            sliderX.ValueChanged += SliderXY_ValueChanged;
            sliderY.ValueChanged += SliderXY_ValueChanged;
            txtStep.TextChanged += txtStep_TextChanged;
            txtX.TextChanged += txtStep_TextChanged;
            txtY.TextChanged += txtStep_TextChanged;
            txtMinX.TextChanged += txtRangeChange_TextChanged;
            txtMaxX.TextChanged += txtRangeChange_TextChanged;
            txtMinY.TextChanged += txtRangeChange_TextChanged;
            txtMaxY.TextChanged += txtRangeChange_TextChanged;

            // Font Size events
            txtFontMin.TextChanged += FontSize_TextChanged;
            txtFontMax.TextChanged += FontSize_TextChanged;
            sliderFontSize.ValueChanged += SliderFontSize_ValueChanged;

            // Point Size events
            txtPointMin.TextChanged += PointSize_TextChanged;
            txtPointMax.TextChanged += PointSize_TextChanged;
            sliderPointSize.ValueChanged += SliderPointSize_ValueChanged;
        }

        private void InitializeGraph()
        {
            ParseGridInputs();
            DrawGrid();
            CreatePointerAt(0, 0);

            if (guideLineX == null)
            {
                guideLineX = new Line
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 0.8,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                graphCanvas.Children.Add(guideLineX);
            }

            if (guideLineY == null)
            {
                guideLineY = new Line
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 0.8,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                graphCanvas.Children.Add(guideLineY);
            }

            MovePointerTo(0, 0, true);
        }

        private void ParseGridInputs()
        {
            if (!double.TryParse(txtMinX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out xMin)) xMin = -10;
            if (!double.TryParse(txtMaxX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out xMax)) xMax = 10;
            if (!double.TryParse(txtMinY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out yMin)) yMin = -10;
            if (!double.TryParse(txtMaxY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out yMax)) yMax = 10;
            if (!double.TryParse(txtStep.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out step) || step <= 0) step = 1;

            if (xMax <= xMin) { xMax = xMin + 1; txtMaxX.Text = xMax.ToString(CultureInfo.InvariantCulture); }
            if (yMax <= yMin) { yMax = yMin + 1; txtMaxY.Text = yMax.ToString(CultureInfo.InvariantCulture); }

            // update sliders to match
            sliderX.Minimum = xMin;
            sliderX.Maximum = xMax;
            sliderY.Minimum = yMin;
            sliderY.Maximum = yMax;
        }

        private void BtnApplyGrid_Click(object sender, RoutedEventArgs e)
        {
            ParseGridInputs();
            DrawGrid();

            var center = CanvasToCoord(GetPointerCenterOnCanvas());
            double px = Math.Max(xMin, Math.Min(xMax, center.X));
            double py = Math.Max(yMin, Math.Min(yMax, center.Y));
            MovePointerTo(px, py, true);
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGrid();
            if (pointer != null)
            {
                MovePointerTo(pointerX, pointerY, true);
            }
        }

        private void DrawGrid()
        {
            var toRemove = graphCanvas.Children
                .OfType<UIElement>()
                .Where(el => el is Line || el is TextBlock)
                .Where(el => el != guideLineX && el != guideLineY)
                .Where(el => el != pointer)
                .Where(el => el != coordLabelBorder)
                .ToList();

            foreach (var el in toRemove)
                graphCanvas.Children.Remove(el);

            tickLabels.Clear();

            double w = graphCanvas.ActualWidth;
            double h = graphCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double xScale = w / (xMax - xMin);
            double yScale = h / (yMax - yMin);

            double pxAxis = (0 >= xMin && 0 <= xMax) ? (0 - xMin) * xScale : double.NaN;
            double pyAxis = (0 >= yMin && 0 <= yMax) ? h - (0 - yMin) * yScale : double.NaN;

            for (double x = xMin; x <= xMax + 1e-9; x += step)
            {
                double px = (x - xMin) * xScale;
                var line = new Line
                {
                    X1 = px,
                    Y1 = 0,
                    X2 = px,
                    Y2 = h,
                    Stroke = (Math.Abs(x) < 1e-9 ? Brushes.Black : Brushes.LightGray),
                    StrokeThickness = (Math.Abs(x) < 1e-9 ? 1.2 : 0.6)
                };
                graphCanvas.Children.Add(line);

                if (!double.IsNaN(pyAxis))
                {
                    var tb = new TextBlock
                    {
                        Text = x.ToString("0.###"),
                        FontSize = 12,
                        Foreground = Brushes.Black
                    };
                    graphCanvas.Children.Add(tb);
                    Canvas.SetLeft(tb, px - 10);
                    Canvas.SetTop(tb, pyAxis + 2);
                    tickLabels.Add(tb);
                }
            }

            for (double y = yMin; y <= yMax + 1e-9; y += step)
            {
                double py = h - (y - yMin) * yScale;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = py,
                    X2 = w,
                    Y2 = py,
                    Stroke = (Math.Abs(y) < 1e-9 ? Brushes.Black : Brushes.LightGray),
                    StrokeThickness = (Math.Abs(y) < 1e-9 ? 1.2 : 0.6)
                };
                graphCanvas.Children.Add(line);

                if (!double.IsNaN(pxAxis))
                {
                    var tb = new TextBlock
                    {
                        Text = y.ToString("0.###"),
                        FontSize = 12,
                        Foreground = Brushes.Black
                    };
                    graphCanvas.Children.Add(tb);
                    Canvas.SetLeft(tb, pxAxis + 4);
                    Canvas.SetTop(tb, py - 8);
                    tickLabels.Add(tb);
                }
            }

            if (!double.IsNaN(pyAxis))
                graphCanvas.Children.Add(new Line { X1 = 0, Y1 = pyAxis, X2 = w, Y2 = pyAxis, Stroke = Brushes.Black, StrokeThickness = 1.4 });

            if (!double.IsNaN(pxAxis))
                graphCanvas.Children.Add(new Line { X1 = pxAxis, Y1 = 0, X2 = pxAxis, Y2 = h, Stroke = Brushes.Black, StrokeThickness = 1.4 });

            // Draw axis labels (X, Y)
            if (!double.IsNaN(pyAxis)) // X-axis exists
            {
                var labelX = new TextBlock
                {
                    Text = "X",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = Brushes.Black
                };
                graphCanvas.Children.Add(labelX);
                Canvas.SetLeft(labelX, w - 20); // right edge
                Canvas.SetTop(labelX, pyAxis - 20);
            }

            if (!double.IsNaN(pxAxis)) // Y-axis exists
            {
                var labelY = new TextBlock
                {
                    Text = "Y",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = Brushes.Black
                };
                graphCanvas.Children.Add(labelY);
                Canvas.SetLeft(labelY, pxAxis + 10);
                Canvas.SetTop(labelY, 5); // top edge
            }

        }

        private void CreatePointerAt(double x, double y)
        {
            if (pointer != null) graphCanvas.Children.Remove(pointer);
            if (coordLabelBorder != null) graphCanvas.Children.Remove(coordLabelBorder);

            pointer = new Ellipse
            {
                Width = pointerRadius * 2,
                Height = pointerRadius * 2,
                Fill = Brushes.Red,
                Stroke = Brushes.DarkRed,
                StrokeThickness = 1,
                Cursor = Cursors.Hand
            };
            graphCanvas.Children.Add(pointer);

            coordLabelText = new TextBlock
            {
                Text = "(0, 0)",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0)
            };

            coordLabelBorder = new Border
            {
                Child = coordLabelText,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(6, 3, 6, 3),
                CornerRadius = new CornerRadius(4),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0)
            };

            graphCanvas.Children.Add(coordLabelBorder);

            MovePointerTo(x, y, true);
        }

        private Point GetPointerCenterOnCanvas()
        {
            if (pointer == null) return new Point(graphCanvas.ActualWidth / 2, graphCanvas.ActualHeight / 2);
            double left = Canvas.GetLeft(pointer);
            double top = Canvas.GetTop(pointer);
            return new Point(left + pointer.Width / 2, top + pointer.Height / 2);
        }

        private void MovePointerTo(double xCoord, double yCoord, bool updateUI)
        {
            pointerX = Math.Max(xMin, Math.Min(xMax, xCoord));
            pointerY = Math.Max(yMin, Math.Min(yMax, yCoord));

            double w = graphCanvas.ActualWidth;
            double h = graphCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double xScale = w / (xMax - xMin);
            double yScale = h / (yMax - yMin);

            double px = (pointerX - xMin) * xScale;
            double py = h - (pointerY - yMin) * yScale;

            Canvas.SetLeft(pointer, px - pointer.Width / 2);
            Canvas.SetTop(pointer, py - pointer.Height / 2);

            if (coordLabelText != null)
                coordLabelText.Text = $"({pointerX:0.###}, {pointerY:0.###})";

            coordLabelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size desired = coordLabelBorder.DesiredSize;

            double lblLeft = px + pointerRadius + 6;
            double lblTop = py - pointerRadius - 6 - desired.Height;

            if (lblLeft + desired.Width > w)
                lblLeft = px - pointerRadius - 6 - desired.Width;
            if (lblLeft < 0)
                lblLeft = 2;

            if (lblTop < 0)
                lblTop = py + pointerRadius + 6;
            if (lblTop + desired.Height > h)
                lblTop = Math.Max(2, h - desired.Height - 2);

            Canvas.SetLeft(coordLabelBorder, lblLeft);
            Canvas.SetTop(coordLabelBorder, lblTop);

            if (guideLineX != null && guideLineY != null)
            {
                guideLineX.X1 = 0;
                guideLineX.X2 = w;
                guideLineX.Y1 = guideLineX.Y2 = py;

                guideLineY.Y1 = 0;
                guideLineY.Y2 = h;
                guideLineY.X1 = guideLineY.X2 = px;
            }

            if (updateUI && !isUpdatingUI)
            {
                // Skip updating if user is in the middle of typing incomplete number
                if (txtX.Text.EndsWith(".") || txtX.Text == "-" ||
                    txtY.Text.EndsWith(".") || txtY.Text == "-")
                    return;

                isUpdatingUI = true;

                txtX.Text = pointerX.ToString("0.###");
                txtX.CaretIndex = txtX.Text.Length; // ✅ Move cursor after text

                txtY.Text = pointerY.ToString("0.###");
                txtY.CaretIndex = txtY.Text.Length; // ✅ Move cursor after text

                sliderX.Value = pointerX;
                sliderY.Value = pointerY;

                isUpdatingUI = false;
            }
        }

        private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = (e.Delta > 0) ? 0.8 : 1.25;

            // zoom around the center of the current view
            Zoom(factor, new Point((xMin + xMax) / 2, (yMin + yMax) / 2));
        }

        private void Zoom(double zoomFactor, Point center)
        {
            // current ranges
            double width = xMax - xMin;
            double height = yMax - yMin;

            double newWidth = width * zoomFactor;
            double newHeight = height * zoomFactor;

            // keep zoom centered around 'center'
            xMin = center.X - (center.X - xMin) * zoomFactor;
            xMax = xMin + newWidth;
            yMin = center.Y - (center.Y - yMin) * zoomFactor;
            yMax = yMin + newHeight;

            // redraw grid and pointer
            ParseGridInputs();
            DrawGrid();
            MovePointerTo(pointerX, pointerY, true);
        }

        public void ZoomIn()
        {
            Zoom(0.8, new Point((xMin + xMax) / 2, (yMin + yMax) / 2)); // center zoom
        }

        public void ZoomOut()
        {
            Zoom(1.25, new Point((xMin + xMax) / 2, (yMin + yMax) / 2)); // center zoom
        }


        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(graphCanvas);
            var center = GetPointerCenterOnCanvas();
            var dx = pos.X - center.X;
            var dy = pos.Y - center.Y;
            double distSq = dx * dx + dy * dy;

            if (distSq <= (pointerRadius + 2) * (pointerRadius + 2))
            {
                isDragging = true;
                dragStartMouse = pos;
                dragStartPointerCanvas = new Point(Canvas.GetLeft(pointer), Canvas.GetTop(pointer));
                graphCanvas.CaptureMouse();
            }
            else
            {
                var coord = CanvasToCoord(pos);
                if (chkSnap.IsChecked == true)
                {
                    coord = SnapToStep(coord);
                }
                MovePointerTo(coord.X, coord.Y, true);
            }
        }

        // ---------------- FONT SIZE ----------------
        private void FontSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtFontMin.Text, out double min) &&
                double.TryParse(txtFontMax.Text, out double max) &&
                min < max)
            {
                sliderFontSize.Minimum = min;
                sliderFontSize.Maximum = max;

                // keep slider value within new bounds
                if (sliderFontSize.Value < min) sliderFontSize.Value = min;
                if (sliderFontSize.Value > max) sliderFontSize.Value = max;
            }
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double fontSize = sliderFontSize.Value;

            // Update all tick labels
            foreach (var tb in tickLabels)
            {
                tb.FontSize = fontSize;
            }

            // Also update coordinate label near the pointer
            if (coordLabelText != null)
                coordLabelText.FontSize = fontSize;
        }

        // ---------------- POINT SIZE ----------------
        private void PointSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(txtPointMin.Text, out double min) &&
                double.TryParse(txtPointMax.Text, out double max) &&
                min < max)
            {
                sliderPointSize.Minimum = min;
                sliderPointSize.Maximum = max;

                if (sliderPointSize.Value < min) sliderPointSize.Value = min;
                if (sliderPointSize.Value > max) sliderPointSize.Value = max;
            }
        }

        private void SliderPointSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            pointerRadius = sliderPointSize.Value * 10; // scale factor (tweak as needed)

            if (pointer != null)
            {
                pointer.Width = pointerRadius * 2;
                pointer.Height = pointerRadius * 2;

                // Reposition pointer so it stays centered
                MovePointerTo(pointerX, pointerY, updateUI: true);
            }
        }

        private void txtRangeChange_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParseGridInputs();
            DrawGrid();

            // Always move pointer to (0,0) if it's within bounds
            if (0 >= xMin && 0 <= xMax && 0 >= yMin && 0 <= yMax)
            {
                MovePointerTo(0, 0, updateUI: true);
            }
            else
            {
                // If 0,0 is outside the visible range, clamp it to the nearest point
                double px = Math.Max(xMin, Math.Min(xMax, 0));
                double py = Math.Max(yMin, Math.Min(yMax, 0));
                MovePointerTo(px, py, updateUI: true);
            }
        }
        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            Point pos = e.GetPosition(graphCanvas);
            Vector delta = pos - dragStartMouse;

            double newLeft = dragStartPointerCanvas.X + delta.X;
            double newTop = dragStartPointerCanvas.Y + delta.Y;

            double centerX = newLeft + pointer.Width / 2;
            double centerY = newTop + pointer.Height / 2;

            var logical = CanvasToCoord(new Point(centerX, centerY));
            if (chkSnap.IsChecked == true) logical = SnapToStep(logical);

            MovePointerTo(logical.X, logical.Y, true);
        }

        private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                graphCanvas.ReleaseMouseCapture();
            }
        }

        private void TxtXY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingUI) return;

            TextBox tb = sender as TextBox;
            if (tb == null) return;

            string text = tb.Text;

            // Allow intermediate input states
            if (string.IsNullOrEmpty(text) || text == "-" || text.EndsWith("."))
                return;

            if (double.TryParse(txtX.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(txtY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                MovePointerTo(x, y, false);
            }
        }

        private void SliderXY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdatingUI) return;

            MovePointerTo(sliderX.Value, sliderY.Value, true);
        }

        private void txtStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParseGridInputs(); DrawGrid(); // reposition pointer to remain within range
            var center = CanvasToCoord(GetPointerCenterOnCanvas()); 
            double px = Math.Max(xMin, Math.Min(xMax, center.X)); 
            double py = Math.Max(yMin, Math.Min(yMax, center.Y)); 
            MovePointerTo(px, py, updateUI: true);
        }

        private Point CanvasToCoord(Point canvasPoint)
        {
            double w = graphCanvas.ActualWidth;
            double h = graphCanvas.ActualHeight;
            double xScale = w / (xMax - xMin);
            double yScale = h / (yMax - yMin);

            double x = xMin + canvasPoint.X / xScale;
            double y = yMin + (h - canvasPoint.Y) / yScale;
            return new Point(x, y);
        }

        private Point SnapToStep(Point p)
        {
            double sx = Math.Round(p.X / step) * step;
            double sy = Math.Round(p.Y / step) * step;
            return new Point(sx, sy);
        }

        // For numeric-only inputs
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            //e.Handled = !double.TryParse(((TextBox)sender).Text + e.Text, out _);

            string currentText = ((TextBox)sender).Text;
            string newText = currentText.Insert(((TextBox)sender as TextBox).SelectionStart, e.Text);

            // Allow empty, "-", ".", "-.", "0.", "1.", etc.
            if (string.IsNullOrEmpty(newText) || newText == "-" || newText == "." || newText == "-." || newText.EndsWith("."))
            {
                e.Handled = false;
                return;
            }

            // Final check with parsing
            e.Handled = !double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }        

    }
}
