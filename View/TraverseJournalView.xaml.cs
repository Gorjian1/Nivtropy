using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Models;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    /// <summary>
    /// Логика взаимодействия для TraverseJournalView.xaml
    /// </summary>
    public partial class TraverseJournalView : UserControl
    {
        private System.Collections.Generic.List<Models.TraverseRow>? _currentTraverseRows;
        private TraverseCalculationViewModel? _currentViewModel;

        private static Color _savedProfileColor = Color.FromRgb(0x19, 0x76, 0xD2);
        private static int _savedColorIndex = 0;
        private static double? _savedMinHeight;
        private static double? _savedMaxHeight;

        private Color _profileColor;
        private double? _manualMinHeight;
        private double? _manualMaxHeight;

        public TraverseJournalView()
        {
            InitializeComponent();

            PreviewKeyDown += TraverseJournalView_PreviewKeyDown;
            PreviewMouseWheel += TraverseJournalView_PreviewMouseWheel;
            Focusable = true;

            _profileColor = _savedProfileColor;
            _manualMinHeight = _savedMinHeight;
            _manualMaxHeight = _savedMaxHeight;

            Loaded += (s, e) =>
            {
                if (ProfileColorComboBox != null)
                    ProfileColorComboBox.SelectedIndex = _savedColorIndex;
            };
        }

        private void ShowTraverseDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string traverseName && DataContext is TraverseJournalViewModel viewModel)
            {
                var calcViewModel = viewModel.Calculation;
                var traverseRows = calcViewModel.Rows.Where(r => r.LineName == traverseName).ToList();
                if (traverseRows.Any())
                {
                    _currentTraverseRows = traverseRows;
                    _currentViewModel = calcViewModel;

                    ProfileTitleText.Text = $"Профиль хода: {traverseName}";

                    if (!_savedMinHeight.HasValue || !_savedMaxHeight.HasValue)
                    {
                        var heights = new System.Collections.Generic.List<double>();
                        foreach (var row in traverseRows)
                        {
                            var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                            if (height.HasValue)
                                heights.Add(height.Value);
                        }

                        if (heights.Any())
                        {
                            MinHeightTextBox.Text = (_savedMinHeight ?? heights.Min()).ToString("F2");
                            MaxHeightTextBox.Text = (_savedMaxHeight ?? heights.Max()).ToString("F2");
                        }
                    }
                    else
                    {
                        MinHeightTextBox.Text = _savedMinHeight.Value.ToString("F2");
                        MaxHeightTextBox.Text = _savedMaxHeight.Value.ToString("F2");
                    }

                    TraverseDetailsPopup.IsOpen = true;
                    DrawProportionalProfile(traverseRows);
                }
            }
        }

        private void ProfileCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_currentTraverseRows != null)
                DrawProportionalProfile(_currentTraverseRows);
        }

        private void DrawProportionalProfile(System.Collections.Generic.List<Models.TraverseRow> rows)
        {
            ProfileCanvas.Children.Clear();

            if (rows.Count < 2)
                return;

            var canvasWidth = ProfileCanvas.ActualWidth;
            var canvasHeight = ProfileCanvas.ActualHeight;

            if (canvasWidth < 10 || canvasHeight < 10)
                return;

            var points = new System.Collections.Generic.List<(double height, double distance, string pointCode, int index)>();
            double cumulativeDistance = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                var pointCode = row.PointCode ?? string.Empty;

                if (height.HasValue)
                {
                    points.Add((height.Value, cumulativeDistance, pointCode, i + 1));
                    cumulativeDistance += row.StationLength_m ?? 0;
                }
            }

            if (points.Count < 2)
                return;

            var minHeight = _manualMinHeight ?? points.Min(p => p.height);
            var maxHeight = _manualMaxHeight ?? points.Max(p => p.height);
            var heightRange = maxHeight - minHeight;
            if (heightRange < 0.001)
                heightRange = 1.0;

            var totalDistance = points[^1].distance;
            if (totalDistance < 0.001)
                totalDistance = 1.0;

            var margin = 50;
            var plotWidth = canvasWidth - 2 * margin;
            var plotHeight = canvasHeight - 2 * margin;

            DrawGrid(canvasWidth, canvasHeight, margin, plotWidth, plotHeight, minHeight, maxHeight, totalDistance);

            var allPointCodes = points.Select(p => p.pointCode).ToList();
            var sharedPoints = allPointCodes.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            var knownHeightPoints = new System.Collections.Generic.HashSet<string>();
            if (_currentViewModel != null)
            {
                foreach (var benchmark in _currentViewModel.Benchmarks)
                {
                    knownHeightPoints.Add(benchmark.Code);
                }
            }

            var profileBrush = new SolidColorBrush(_profileColor);

            for (int i = 0; i < points.Count - 1; i++)
            {
                var x1 = margin + (points[i].distance / totalDistance) * plotWidth;
                var y1 = canvasHeight - margin - ((points[i].height - minHeight) / heightRange * plotHeight);
                var x2 = margin + (points[i + 1].distance / totalDistance) * plotWidth;
                var y2 = canvasHeight - margin - ((points[i + 1].height - minHeight) / heightRange * plotHeight);

                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = profileBrush,
                    StrokeThickness = 2.5
                };
                ProfileCanvas.Children.Add(line);

                DrawPoint(x1, y1, points[i].pointCode, points[i].index, points[i].height,
                         sharedPoints.Contains(points[i].pointCode),
                         knownHeightPoints.Contains(points[i].pointCode),
                         profileBrush);
            }

            var lastX = margin + (points[^1].distance / totalDistance) * plotWidth;
            var lastY = canvasHeight - margin - ((points[^1].height - minHeight) / heightRange * plotHeight);
            DrawPoint(lastX, lastY, points[^1].pointCode, points[^1].index, points[^1].height,
                     sharedPoints.Contains(points[^1].pointCode),
                     knownHeightPoints.Contains(points[^1].pointCode),
                     profileBrush);
        }

        private void DrawGrid(double canvasWidth, double canvasHeight, double margin, double plotWidth, double plotHeight, double minHeight, double maxHeight, double totalDistance)
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            var axisBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

            var verticalLine = new Line
            {
                X1 = margin,
                Y1 = margin,
                X2 = margin,
                Y2 = canvasHeight - margin,
                Stroke = axisBrush,
                StrokeThickness = 1.5
            };
            ProfileCanvas.Children.Add(verticalLine);

            var horizontalLine = new Line
            {
                X1 = margin,
                Y1 = canvasHeight - margin,
                X2 = canvasWidth - margin,
                Y2 = canvasHeight - margin,
                Stroke = axisBrush,
                StrokeThickness = 1.5
            };
            ProfileCanvas.Children.Add(horizontalLine);

            int gridLines = 4;
            for (int i = 1; i <= gridLines; i++)
            {
                double x = margin + i * (plotWidth / gridLines);
                var vLine = new Line
                {
                    X1 = x,
                    Y1 = margin,
                    X2 = x,
                    Y2 = canvasHeight - margin,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                };
                ProfileCanvas.Children.Add(vLine);

                double y = margin + i * (plotHeight / gridLines);
                var hLine = new Line
                {
                    X1 = margin,
                    Y1 = y,
                    X2 = canvasWidth - margin,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                };
                ProfileCanvas.Children.Add(hLine);
            }

            for (int i = 0; i <= gridLines; i++)
            {
                double distance = (totalDistance / gridLines) * i;
                double x = margin + i * (plotWidth / gridLines);
                var distanceLabel = new TextBlock
                {
                    Text = $"{distance:0.0}",
                    FontSize = 11,
                    Foreground = Brushes.Gray
                };
                Canvas.SetLeft(distanceLabel, x - 10);
                Canvas.SetTop(distanceLabel, canvasHeight - margin + 4);
                ProfileCanvas.Children.Add(distanceLabel);

                double height = minHeight + (maxHeight - minHeight) * i / gridLines;
                double y = canvasHeight - margin - i * (plotHeight / gridLines);
                var heightLabel = new TextBlock
                {
                    Text = $"{height:0.00}",
                    FontSize = 11,
                    Foreground = Brushes.Gray
                };
                Canvas.SetLeft(heightLabel, 4);
                Canvas.SetTop(heightLabel, y - 8);
                ProfileCanvas.Children.Add(heightLabel);
            }
        }

        private void DrawPoint(double x, double y, string pointCode, int index, double height, bool isShared, bool isKnownHeight, SolidColorBrush profileBrush)
        {
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = isKnownHeight ? Brushes.Red : (isShared ? Brushes.DarkOrange : profileBrush),
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(ellipse, x - 4);
            Canvas.SetTop(ellipse, y - 4);
            ProfileCanvas.Children.Add(ellipse);

            var label = new TextBlock
            {
                Text = $"{index}: {pointCode} ({height:0.000})",
                FontSize = 11,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(label, x + 6);
            Canvas.SetTop(label, y - 8);
            ProfileCanvas.Children.Add(label);
        }

        private void CloseProfilePopup_Click(object sender, RoutedEventArgs e)
        {
            TraverseDetailsPopup.IsOpen = false;

            _savedProfileColor = _profileColor;
            _savedColorIndex = ProfileColorComboBox.SelectedIndex;
            _savedMinHeight = _manualMinHeight;
            _savedMaxHeight = _manualMaxHeight;
        }

        private void ProfileColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileColorComboBox.SelectedItem is ComboBoxItem item && item.Tag is string hex)
            {
                _profileColor = (Color)ColorConverter.ConvertFromString(hex);
                _savedColorIndex = ProfileColorComboBox.SelectedIndex;
                if (_currentTraverseRows != null)
                    DrawProportionalProfile(_currentTraverseRows);
            }
        }

        private void HeightLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(MinHeightTextBox.Text, out double minValue))
                _manualMinHeight = minValue;
            else
                _manualMinHeight = null;

            if (double.TryParse(MaxHeightTextBox.Text, out double maxValue))
                _manualMaxHeight = maxValue;
            else
                _manualMaxHeight = null;

            _savedMinHeight = _manualMinHeight;
            _savedMaxHeight = _manualMaxHeight;

            if (_currentTraverseRows != null)
                DrawProportionalProfile(_currentTraverseRows);
        }

        private void TraverseJournalView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (TraverseDetailsPopup.IsOpen)
            {
                double zoomStep = e.KeyboardDevice.Modifiers == ModifierKeys.Shift ? 0.05 : 0.1;
                if (e.Key == Key.Add || e.Key == Key.OemPlus)
                {
                    ZoomProfile(1 + zoomStep);
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                {
                    ZoomProfile(1 - zoomStep);
                    e.Handled = true;
                }
            }
        }

        private void TraverseJournalView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TraverseDetailsPopup.IsOpen)
            {
                double delta = e.Delta > 0 ? 1.1 : 0.9;
                ZoomProfile(delta);
                e.Handled = true;
            }
        }

        private void ZoomProfile(double factor)
        {
            if (_currentTraverseRows == null)
                return;

            if (!_manualMinHeight.HasValue || !_manualMaxHeight.HasValue)
            {
                var heights = _currentTraverseRows
                    .Select(r => r.IsVirtualStation ? r.BackHeight : r.ForeHeight)
                    .Where(h => h.HasValue)
                    .Select(h => h!.Value)
                    .ToList();

                if (heights.Count < 2)
                    return;

                _manualMinHeight = heights.Min();
                _manualMaxHeight = heights.Max();
            }

            var heightRange = _manualMaxHeight.Value - _manualMinHeight.Value;
            if (heightRange <= 0)
                heightRange = 1;

            var center = _manualMinHeight.Value + heightRange / 2;
            var newRange = heightRange * factor;

            _manualMinHeight = center - newRange / 2;
            _manualMaxHeight = center + newRange / 2;

            MinHeightTextBox.Text = _manualMinHeight.Value.ToString("F2");
            MaxHeightTextBox.Text = _manualMaxHeight.Value.ToString("F2");

            _savedMinHeight = _manualMinHeight;
            _savedMaxHeight = _manualMaxHeight;

            DrawProportionalProfile(_currentTraverseRows);
        }
    }
}
