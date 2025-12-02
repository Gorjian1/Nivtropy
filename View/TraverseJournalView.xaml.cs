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
        private System.Collections.Generic.List<TraverseRow>? _currentTraverseRows;
        private TraverseCalculationViewModel? _currentViewModel;

        private static Color _savedProfileColor = Color.FromRgb(0x19, 0x76, 0xD2);
        private static Color _savedProfileZ0Color = Color.FromRgb(0x80, 0x80, 0x80);
        private static int _savedColorIndex = 0;
        private static int _savedZ0ColorIndex = 0;
        private static double? _savedMinHeight;
        private static double? _savedMaxHeight;
        private static bool _savedShowZ = true;
        private static bool _savedShowZ0 = true;

        private Color _profileColor;
        private Color _profileZ0Color;
        private double? _manualMinHeight;
        private double? _manualMaxHeight;
        private bool _showZ = true;
        private bool _showZ0 = true;

        public TraverseJournalView()
        {
            InitializeComponent();

            PreviewKeyDown += TraverseJournalView_PreviewKeyDown;
            PreviewMouseWheel += TraverseJournalView_PreviewMouseWheel;
            Focusable = true;

            _profileColor = _savedProfileColor;
            _profileZ0Color = _savedProfileZ0Color;
            _manualMinHeight = _savedMinHeight;
            _manualMaxHeight = _savedMaxHeight;
            _showZ = _savedShowZ;
            _showZ0 = _savedShowZ0;

            Loaded += (s, e) =>
            {
                if (ProfileColorComboBox != null)
                    ProfileColorComboBox.SelectedIndex = _savedColorIndex;
                if (ProfileZ0ColorComboBox != null)
                    ProfileZ0ColorComboBox.SelectedIndex = _savedZ0ColorIndex;
                if (ShowZCheckBox != null)
                    ShowZCheckBox.IsChecked = _savedShowZ;
                if (ShowZ0CheckBox != null)
                    ShowZ0CheckBox.IsChecked = _savedShowZ0;

                UpdateLegendVisibility();
            };
        }

        private void ShowTraverseDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string traverseName && DataContext is TraverseJournalViewModel viewModel)
            {
                var calculationViewModel = viewModel.Calculation;
                var traverseRows = calculationViewModel.Rows.Where(r => r.LineName == traverseName).ToList();
                if (traverseRows.Any())
                {
                    _currentTraverseRows = traverseRows;
                    _currentViewModel = calculationViewModel;

                    ProfileTitleText.Text = $"Профиль хода: {traverseName}";

                    if (!_savedMinHeight.HasValue || !_savedMaxHeight.HasValue)
                    {
                        var heights = traverseRows
                            .Select(r => r.IsVirtualStation ? r.BackHeight : r.ForeHeight)
                            .Where(h => h.HasValue)
                            .Select(h => h!.Value)
                            .ToList();

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

        private void ShowSharedPoints_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LineSummary lineSummary && DataContext is TraverseJournalViewModel viewModel)
            {
                var sharedItems = viewModel.Calculation.GetSharedPointsForRun(lineSummary);

                SharedPointsList.ItemsSource = sharedItems;
                SharedPointsEmptyText.Visibility = sharedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                SharedPointsPopup.PlacementTarget = button;
                SharedPointsPopup.IsOpen = true;
            }
        }

        private void ProfileCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_currentTraverseRows != null)
                DrawProportionalProfile(_currentTraverseRows);
        }

        private void DrawProportionalProfile(System.Collections.Generic.List<TraverseRow> rows)
        {
            ProfileCanvas.Children.Clear();

            if (rows.Count < 2)
                return;

            var canvasWidth = ProfileCanvas.ActualWidth;
            var canvasHeight = ProfileCanvas.ActualHeight;

            if (canvasWidth < 10 || canvasHeight < 10)
                return;

            // Собираем точки для Z и Z0
            var pointsZ = new System.Collections.Generic.List<(double height, double distance, string pointCode, int index)>();
            var pointsZ0 = new System.Collections.Generic.List<(double height, double distance, string pointCode, int index)>();
            double cumulativeDistance = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var heightZ = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                var heightZ0 = row.IsVirtualStation ? row.BackHeightZ0 : row.ForeHeightZ0;
                var pointCode = row.PointCode ?? "";

                if (heightZ.HasValue && _showZ)
                {
                    pointsZ.Add((heightZ.Value, cumulativeDistance, pointCode, i + 1));
                }

                if (heightZ0.HasValue && _showZ0)
                {
                    pointsZ0.Add((heightZ0.Value, cumulativeDistance, pointCode, i + 1));
                }

                cumulativeDistance += row.StationLength_m ?? 0;
            }

            if (pointsZ.Count < 2 && pointsZ0.Count < 2)
                return;

            // Определяем диапазон высот для масштабирования
            var allHeights = new System.Collections.Generic.List<double>();
            if (_showZ && pointsZ.Any())
                allHeights.AddRange(pointsZ.Select(p => p.height));
            if (_showZ0 && pointsZ0.Any())
                allHeights.AddRange(pointsZ0.Select(p => p.height));

            var minHeight = _manualMinHeight ?? allHeights.Min();
            var maxHeight = _manualMaxHeight ?? allHeights.Max();
            var heightRange = maxHeight - minHeight;
            if (heightRange < 0.001)
                heightRange = 1.0;

            var totalDistance = cumulativeDistance;
            if (totalDistance < 0.001)
                totalDistance = 1.0;

            var margin = 50;
            var plotWidth = canvasWidth - 2 * margin;
            var plotHeight = canvasHeight - 2 * margin;

            DrawGrid(canvasWidth, canvasHeight, margin, plotWidth, plotHeight, minHeight, maxHeight, totalDistance);

            var knownHeightPoints = new System.Collections.Generic.HashSet<string>();
            if (_currentViewModel != null)
            {
                foreach (var benchmark in _currentViewModel.Benchmarks)
                {
                    knownHeightPoints.Add(benchmark.Code);
                }
            }

            // Рисуем линию Z0 (пунктиром)
            if (_showZ0 && pointsZ0.Count >= 2)
            {
                var profileZ0Brush = new SolidColorBrush(_profileZ0Color);

                for (int i = 0; i < pointsZ0.Count - 1; i++)
                {
                    var x1 = margin + (pointsZ0[i].distance / totalDistance) * plotWidth;
                    var y1 = canvasHeight - margin - ((pointsZ0[i].height - minHeight) / heightRange * plotHeight);
                    var x2 = margin + (pointsZ0[i + 1].distance / totalDistance) * plotWidth;
                    var y2 = canvasHeight - margin - ((pointsZ0[i + 1].height - minHeight) / heightRange * plotHeight);

                    var line = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = profileZ0Brush,
                        StrokeThickness = 2.0,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    ProfileCanvas.Children.Add(line);
                }
            }

            // Рисуем линию Z (сплошной)
            if (_showZ && pointsZ.Count >= 2)
            {
                var profileBrush = new SolidColorBrush(_profileColor);

                for (int i = 0; i < pointsZ.Count - 1; i++)
                {
                    var x1 = margin + (pointsZ[i].distance / totalDistance) * plotWidth;
                    var y1 = canvasHeight - margin - ((pointsZ[i].height - minHeight) / heightRange * plotHeight);
                    var x2 = margin + (pointsZ[i + 1].distance / totalDistance) * plotWidth;
                    var y2 = canvasHeight - margin - ((pointsZ[i + 1].height - minHeight) / heightRange * plotHeight);

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
                }
            }

            // Рисуем точки (используем Z если доступен, иначе Z0)
            var displayPoints = _showZ && pointsZ.Any() ? pointsZ : pointsZ0;
            if (displayPoints.Any())
            {
                var allPointCodes = displayPoints.Select(p => p.pointCode).ToList();
                var sharedPoints = allPointCodes.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
                var profileBrush = new SolidColorBrush(_showZ ? _profileColor : _profileZ0Color);

                foreach (var point in displayPoints)
                {
                    var x = margin + (point.distance / totalDistance) * plotWidth;
                    var y = canvasHeight - margin - ((point.height - minHeight) / heightRange * plotHeight);

                    DrawPoint(x, y, point.pointCode, point.index, point.height,
                             sharedPoints.Contains(point.pointCode),
                             knownHeightPoints.Contains(point.pointCode),
                             profileBrush);
                }
            }

            UpdateLegendColors();
        }

        private void DrawGrid(double canvasWidth, double canvasHeight, double margin,
                              double plotWidth, double plotHeight,
                              double minHeight, double maxHeight, double totalDistance)
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

            int verticalLines = 10;
            for (int i = 0; i <= verticalLines; i++)
            {
                var x = margin + (i / (double)verticalLines) * plotWidth;
                var line = new Line
                {
                    X1 = x,
                    Y1 = margin,
                    X2 = x,
                    Y2 = canvasHeight - margin,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                ProfileCanvas.Children.Add(line);

                if (i % 2 == 0)
                {
                    var distance = (i / (double)verticalLines) * totalDistance;
                    var label = new TextBlock
                    {
                        Text = $"{distance:F0}",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
                    };
                    Canvas.SetLeft(label, x - 10);
                    Canvas.SetTop(label, canvasHeight - margin + 5);
                    ProfileCanvas.Children.Add(label);
                }
            }

            int horizontalLines = 8;
            for (int i = 0; i <= horizontalLines; i++)
            {
                var y = margin + (i / (double)horizontalLines) * plotHeight;
                var line = new Line
                {
                    X1 = margin,
                    Y1 = y,
                    X2 = canvasWidth - margin,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                ProfileCanvas.Children.Add(line);

                if (i % 2 == 0)
                {
                    var height = maxHeight - (i / (double)horizontalLines) * (maxHeight - minHeight);
                    var label = new TextBlock
                    {
                        Text = $"{height:F2}",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
                    };
                    Canvas.SetRight(label, canvasWidth - margin + 5);
                    Canvas.SetTop(label, y - 7);
                    ProfileCanvas.Children.Add(label);
                }
            }

            var axisLine = new Line
            {
                X1 = margin,
                Y1 = canvasHeight - margin,
                X2 = canvasWidth - margin,
                Y2 = canvasHeight - margin,
                Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = 1.5
            };
            ProfileCanvas.Children.Add(axisLine);

            var leftAxisLine = new Line
            {
                X1 = margin,
                Y1 = margin,
                X2 = margin,
                Y2 = canvasHeight - margin,
                Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = 1.5
            };
            ProfileCanvas.Children.Add(leftAxisLine);

            var xAxisLabel = new TextBlock
            {
                Text = "Длина, м",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40))
            };
            Canvas.SetLeft(xAxisLabel, canvasWidth / 2 - 30);
            Canvas.SetTop(xAxisLabel, canvasHeight - 15);
            ProfileCanvas.Children.Add(xAxisLabel);

            var yAxisLabel = new TextBlock
            {
                Text = "Высота, м",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)),
                RenderTransform = new RotateTransform(-90)
            };
            Canvas.SetLeft(yAxisLabel, 10);
            Canvas.SetTop(yAxisLabel, canvasHeight / 2 + 30);
            ProfileCanvas.Children.Add(yAxisLabel);
        }

        private void DrawPoint(double x, double y, string pointCode, int index, double height,
                               bool isShared, bool hasKnownHeight, SolidColorBrush brush)
        {
            double size = isShared ? 10 : 7;

            if (isShared && hasKnownHeight)
            {
                var outerCircle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Stroke = brush,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(outerCircle, x - size / 2);
                Canvas.SetTop(outerCircle, y - size / 2);
                ProfileCanvas.Children.Add(outerCircle);

                var cross1 = new Line
                {
                    X1 = x - 3,
                    Y1 = y - 3,
                    X2 = x + 3,
                    Y2 = y + 3,
                    Stroke = brush,
                    StrokeThickness = 1.5
                };
                ProfileCanvas.Children.Add(cross1);

                var cross2 = new Line
                {
                    X1 = x - 3,
                    Y1 = y + 3,
                    X2 = x + 3,
                    Y2 = y - 3,
                    Stroke = brush,
                    StrokeThickness = 1.5
                };
                ProfileCanvas.Children.Add(cross2);

                outerCircle.ToolTip = $"Точка {pointCode} (№{index})\nВысота: {height:F4} м\n(общая, с известной высотой)";
            }
            else if (isShared)
            {
                var outerCircle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Stroke = brush,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(outerCircle, x - size / 2);
                Canvas.SetTop(outerCircle, y - size / 2);
                outerCircle.ToolTip = $"Точка {pointCode} (№{index})\nВысота: {height:F4} м\n(общая с другими ходами)";
                ProfileCanvas.Children.Add(outerCircle);
            }
            else if (hasKnownHeight)
            {
                var circle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(circle, x - size / 2);
                Canvas.SetTop(circle, y - size / 2);
                circle.ToolTip = $"Точка {pointCode} (№{index})\nВысота: {height:F4} м\n(известная высота)";
                ProfileCanvas.Children.Add(circle);

                var cross1 = new Line
                {
                    X1 = x - 2.5,
                    Y1 = y - 2.5,
                    X2 = x + 2.5,
                    Y2 = y + 2.5,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.2
                };
                ProfileCanvas.Children.Add(cross1);

                var cross2 = new Line
                {
                    X1 = x - 2.5,
                    Y1 = y + 2.5,
                    X2 = x + 2.5,
                    Y2 = y - 2.5,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.2
                };
                ProfileCanvas.Children.Add(cross2);
            }
            else
            {
                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(ellipse, x - size / 2);
                Canvas.SetTop(ellipse, y - size / 2);
                ellipse.ToolTip = $"Точка {pointCode} (№{index})\nВысота: {height:F4} м";
                ProfileCanvas.Children.Add(ellipse);
            }
        }

        private void ProfileColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileColorComboBox.SelectedItem is ComboBoxItem item && item.Tag is string colorHex)
            {
                _profileColor = (Color)ColorConverter.ConvertFromString(colorHex);

                _savedProfileColor = _profileColor;
                _savedColorIndex = ProfileColorComboBox.SelectedIndex;

                UpdateLegendColors();

                if (_currentTraverseRows != null)
                    DrawProportionalProfile(_currentTraverseRows);
            }
        }

        private void HeightLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentTraverseRows == null)
                return;

            if (double.TryParse(MinHeightTextBox.Text, out var minH))
            {
                _manualMinHeight = minH;
                _savedMinHeight = minH;
            }
            else
            {
                _manualMinHeight = null;
                _savedMinHeight = null;
            }

            if (double.TryParse(MaxHeightTextBox.Text, out var maxH))
            {
                _manualMaxHeight = maxH;
                _savedMaxHeight = maxH;
            }
            else
            {
                _manualMaxHeight = null;
                _savedMaxHeight = null;
            }

            DrawProportionalProfile(_currentTraverseRows);
        }

        private void CloseProfilePopup_Click(object sender, RoutedEventArgs e)
        {
            TraverseDetailsPopup.IsOpen = false;
        }

        private void TraverseJournalView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is TraverseJournalViewModel viewModel)
            {
                var settings = viewModel.Settings;

                if ((e.Key == Key.Add || e.Key == Key.OemPlus) && settings.TableFontSize < 20)
                {
                    settings.TableFontSize += 1;
                    e.Handled = true;
                }
                else if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && settings.TableFontSize > 10)
                {
                    settings.TableFontSize -= 1;
                    e.Handled = true;
                }
            }
        }

        private void TraverseJournalView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && DataContext is TraverseJournalViewModel viewModel)
            {
                var settings = viewModel.Settings;

                if (e.Delta > 0 && settings.TableFontSize < 20)
                {
                    settings.TableFontSize += 1;
                }
                else if (e.Delta < 0 && settings.TableFontSize > 10)
                {
                    settings.TableFontSize -= 1;
                }

                e.Handled = true;
            }
        }

        private void ShowZCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _showZ = ShowZCheckBox?.IsChecked ?? true;
            _savedShowZ = _showZ;

            UpdateLegendVisibility();

            if (_currentTraverseRows != null)
                DrawProportionalProfile(_currentTraverseRows);
        }

        private void ShowZ0CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _showZ0 = ShowZ0CheckBox?.IsChecked ?? true;
            _savedShowZ0 = _showZ0;

            UpdateLegendVisibility();

            if (_currentTraverseRows != null)
                DrawProportionalProfile(_currentTraverseRows);
        }

        private void ProfileZ0ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileZ0ColorComboBox.SelectedItem is ComboBoxItem item && item.Tag is string colorHex)
            {
                _profileZ0Color = (Color)ColorConverter.ConvertFromString(colorHex);
                _savedProfileZ0Color = _profileZ0Color;
                _savedZ0ColorIndex = ProfileZ0ColorComboBox.SelectedIndex;

                UpdateLegendColors();

                if (_currentTraverseRows != null)
                    DrawProportionalProfile(_currentTraverseRows);
            }
        }

        private void AutoScaleButton_Click(object sender, RoutedEventArgs e)
        {
            _manualMinHeight = null;
            _manualMaxHeight = null;
            _savedMinHeight = null;
            _savedMaxHeight = null;

            if (_currentTraverseRows != null && _currentTraverseRows.Any())
            {
                var heights = new System.Collections.Generic.List<double>();

                if (_showZ)
                {
                    heights.AddRange(_currentTraverseRows
                        .Select(r => r.IsVirtualStation ? r.BackHeight : r.ForeHeight)
                        .Where(h => h.HasValue)
                        .Select(h => h!.Value));
                }

                if (_showZ0)
                {
                    heights.AddRange(_currentTraverseRows
                        .Select(r => r.IsVirtualStation ? r.BackHeightZ0 : r.ForeHeightZ0)
                        .Where(h => h.HasValue)
                        .Select(h => h!.Value));
                }

                if (heights.Any())
                {
                    MinHeightTextBox.Text = heights.Min().ToString("F2");
                    MaxHeightTextBox.Text = heights.Max().ToString("F2");
                }
            }
        }

        private void UpdateLegendVisibility()
        {
            if (LegendZItem != null)
                LegendZItem.Visibility = _showZ ? Visibility.Visible : Visibility.Collapsed;

            if (LegendZ0Item != null)
                LegendZ0Item.Visibility = _showZ0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLegendColors()
        {
            if (LegendZLine != null)
                LegendZLine.Fill = new SolidColorBrush(_profileColor);

            if (LegendZ0Line != null)
                LegendZ0Line.Fill = new SolidColorBrush(_profileZ0Color);
        }
    }
}
