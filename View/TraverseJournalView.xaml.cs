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
        private ProfileStatistics? _currentStatistics;

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
        private bool _showAnomalies = true;
        private double _sensitivitySigma = 2.5;

        // Debouncing для отложенной перерисовки профиля
        private System.Windows.Threading.DispatcherTimer? _redrawTimer;
        private bool _redrawPending;

        public TraverseJournalView()
        {
            InitializeComponent();

            PreviewKeyDown += TraverseJournalView_PreviewKeyDown;
            PreviewMouseWheel += TraverseJournalView_PreviewMouseWheel;

            // Инициализация таймера для debouncing перерисовки
            _redrawTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _redrawTimer.Tick += (s, e) =>
            {
                _redrawTimer.Stop();
                if (_redrawPending && _currentTraverseRows != null)
                {
                    _redrawPending = false;
                    DrawProportionalProfileImmediate(_currentTraverseRows);
                }
            };
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

                    // Сбрасываем настройки масштаба для нового профиля (автомасштаб)
                    _manualMinHeight = null;
                    _manualMaxHeight = null;

                    // Вычисляем автоматический масштаб для текущего хода
                    var heights = new System.Collections.Generic.List<double>();

                    if (_showZ)
                    {
                        heights.AddRange(traverseRows
                            .Select(r => r.IsVirtualStation ? r.BackHeight : r.ForeHeight)
                            .Where(h => h.HasValue)
                            .Select(h => h!.Value));
                    }

                    if (_showZ0)
                    {
                        heights.AddRange(traverseRows
                            .Select(r => r.IsVirtualStation ? r.BackHeightZ0 : r.ForeHeightZ0)
                            .Where(h => h.HasValue)
                            .Select(h => h!.Value));
                    }

                    if (heights.Any())
                    {
                        var (minHeight, maxHeight) = CalculateExtendedRange(heights);
                        MinHeightTextBox.Text = minHeight.ToString("F2");
                        MaxHeightTextBox.Text = maxHeight.ToString("F2");
                    }

                    // Вычисляем статистику и обнаруживаем аномалии
                    _currentStatistics = CalculateStatistics(traverseRows);

                    TraverseDetailsPopup.IsOpen = true;
                    DrawProportionalProfile(traverseRows);
                    UpdateStatisticsPanel();
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

        /// <summary>
        /// Запланировать перерисовку профиля с debouncing (отложенная перерисовка)
        /// </summary>
        private void DrawProportionalProfile(System.Collections.Generic.List<TraverseRow> rows)
        {
            _currentTraverseRows = rows;
            _redrawPending = true;
            _redrawTimer?.Stop();
            _redrawTimer?.Start();
        }

        /// <summary>
        /// Немедленная перерисовка профиля (без debouncing)
        /// </summary>
        private void DrawProportionalProfileImmediate(System.Collections.Generic.List<TraverseRow> rows)
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
            var pointDistances = new System.Collections.Generic.List<double>(rows.Count);
            var segmentStarts = new System.Collections.Generic.List<double>(rows.Count);
            var segmentLengths = new System.Collections.Generic.List<double>(rows.Count);
            double cumulativeDistance = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var stationLength = row.StationLength_m ?? 0;
                var heightZ = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                var heightZ0 = row.IsVirtualStation ? row.BackHeightZ0 : row.ForeHeightZ0;
                var pointCode = row.PointCode ?? "";

                segmentStarts.Add(cumulativeDistance);

                if (!row.IsVirtualStation)
                {
                    cumulativeDistance += stationLength;
                }

                segmentLengths.Add(row.IsVirtualStation ? 0 : stationLength);
                pointDistances.Add(cumulativeDistance);

                if (heightZ.HasValue && _showZ)
                {
                    pointsZ.Add((heightZ.Value, cumulativeDistance, pointCode, i + 1));
                }

                if (heightZ0.HasValue && _showZ0)
                {
                    pointsZ0.Add((heightZ0.Value, cumulativeDistance, pointCode, i + 1));
                }
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

            // Рисуем цветовую индикацию разности плеч (толстые фоновые линии)
            for (int i = 0; i < rows.Count - 1; i++)
            {
                var row = rows[i];
                if (row.IsVirtualStation)
                    continue;

                var armDiff = Math.Abs(row.ArmDifference_m ?? 0);
                var stationLength = segmentLengths[i];

                // Определяем допуск для разности плеч
                // Обычно допуск составляет около 5м для технического нивелирования
                // Используем значение из флага IsArmDifferenceExceeded если он установлен
                var armDiffExceeded = row.IsArmDifferenceExceeded;

                // Цветовая градация: зелёный -> жёлтый -> оранжевый -> красный
                Color segmentColor;
                if (armDiffExceeded)
                {
                    segmentColor = Color.FromRgb(255, 69, 58); // Красный
                }
                else if (armDiff > 3.0)
                {
                    segmentColor = Color.FromRgb(255, 159, 10); // Оранжевый
                }
                else if (armDiff > 1.5)
                {
                    segmentColor = Color.FromRgb(255, 204, 0); // Жёлтый
                }
                else
                {
                    segmentColor = Color.FromRgb(52, 199, 89); // Зелёный
                }

                // Рисуем толстую полупрозрачную линию как фон
                var x1 = margin + (segmentStarts[i] / totalDistance) * plotWidth;
                var x2 = margin + ((segmentStarts[i] + stationLength) / totalDistance) * plotWidth;

                // Рисуем вертикальную полосу по всей высоте графика
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(x2 - x1, 2),
                    Height = plotHeight,
                    Fill = new SolidColorBrush(Color.FromArgb(30, segmentColor.R, segmentColor.G, segmentColor.B)), // 30 = ~12% прозрачности
                    ToolTip = $"Ст. {row.Index}: разность плеч = {armDiff:F3} м"
                };
                Canvas.SetLeft(rect, x1);
                Canvas.SetTop(rect, margin);
                ProfileCanvas.Children.Add(rect);

            }

            // Рисуем линию Z0 (пунктиром, серая)
            if (_showZ0 && pointsZ0.Count >= 2)
            {
                var grayBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));

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
                        Stroke = grayBrush,
                        StrokeThickness = 2.0,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    ProfileCanvas.Children.Add(line);
                }
            }

            // Рисуем линию Z (сплошной, белая с чёрной обводкой)
            if (_showZ && pointsZ.Count >= 2)
            {
                for (int i = 0; i < pointsZ.Count - 1; i++)
                {
                    var x1 = margin + (pointsZ[i].distance / totalDistance) * plotWidth;
                    var y1 = canvasHeight - margin - ((pointsZ[i].height - minHeight) / heightRange * plotHeight);
                    var x2 = margin + (pointsZ[i + 1].distance / totalDistance) * plotWidth;
                    var y2 = canvasHeight - margin - ((pointsZ[i + 1].height - minHeight) / heightRange * plotHeight);

                    // Сначала рисуем чёрную обводку (толще)
                    var outlineLine = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = Brushes.Black,
                        StrokeThickness = 3.5
                    };
                    ProfileCanvas.Children.Add(outlineLine);

                    // Затем белую линию сверху (тоньше)
                    var whiteLine = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = Brushes.White,
                        StrokeThickness = 2.0
                    };
                    ProfileCanvas.Children.Add(whiteLine);
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

            // Визуализация аномалий (красные кружки)
            if (_showAnomalies && _currentStatistics != null && _currentStatistics.HasOutliers)
            {
                var outlierStations = _currentStatistics.Outliers.Select(o => o.StationIndex).ToHashSet();

                for (int i = 0; i < rows.Count; i++)
                {
                    if (outlierStations.Contains(rows[i].Index))
                    {
                        var height = rows[i].IsVirtualStation ? rows[i].BackHeight : rows[i].ForeHeight;
                        if (height.HasValue)
                        {
                            var x = margin + (pointDistances[i] / totalDistance) * plotWidth;
                            var y = canvasHeight - margin - ((height.Value - minHeight) / heightRange * plotHeight);

                            // Находим все аномалии для этой станции
                            var stationOutliers = _currentStatistics.Outliers.Where(o => o.StationIndex == rows[i].Index).ToList();
                            var maxSeverity = stationOutliers.Max(o => o.Severity);
                            var outlierDescriptions = string.Join("\n", stationOutliers.Select(o => o.Description));

                            // Рисуем индикатор аномалии
                            var indicatorColor = maxSeverity >= 3 ? Colors.Red : (maxSeverity >= 2 ? Colors.OrangeRed : Colors.Orange);
                            var outerCircle = new Ellipse
                            {
                                Width = 16,
                                Height = 16,
                                Stroke = new SolidColorBrush(indicatorColor),
                                StrokeThickness = 2,
                                Fill = Brushes.Transparent,
                                ToolTip = $"⚠ Аномалия на станции {rows[i].Index}\n{outlierDescriptions}"
                            };
                            Canvas.SetLeft(outerCircle, x - 8);
                            Canvas.SetTop(outerCircle, y - 8);
                            ProfileCanvas.Children.Add(outerCircle);
                        }
                    }
                }
            }
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

            if (_currentTraverseRows != null)
                DrawProportionalProfile(_currentTraverseRows);
        }

        private void ShowZ0CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _showZ0 = ShowZ0CheckBox?.IsChecked ?? true;
            _savedShowZ0 = _showZ0;

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
                    var (minHeight, maxHeight) = CalculateExtendedRange(heights);
                    MinHeightTextBox.Text = minHeight.ToString("F2");
                    MaxHeightTextBox.Text = maxHeight.ToString("F2");
                }
            }
        }

        /// <summary>
        /// Вычисляет расширенный диапазон высот для лучшей визуализации
        /// Добавляет ±50% от диапазона данных
        /// </summary>
        private (double min, double max) CalculateExtendedRange(System.Collections.Generic.List<double> heights)
        {
            if (!heights.Any()) return (0, 0);

            var actualMin = heights.Min();
            var actualMax = heights.Max();
            var range = actualMax - actualMin;

            // Если диапазон очень маленький (менее 1см), расширяем минимум на ±0.5м
            if (range < 0.01)
            {
                return (actualMin - 0.5, actualMax + 0.5);
            }

            // Добавляем половину диапазона сверху и снизу
            var expansion = range * 0.5;
            var minHeight = actualMin - expansion;
            var maxHeight = actualMax + expansion;

            return (minHeight, maxHeight);
        }

        /// <summary>
        /// Вычисляет статистику и обнаруживает аномалии в данных хода
        /// </summary>
        private ProfileStatistics CalculateStatistics(System.Collections.Generic.List<TraverseRow> rows, double sensitivitySigma = 2.5)
        {
            var stats = new ProfileStatistics
            {
                StationCount = rows.Count
            };

            if (rows.Count < 2)
                return stats;

            // Собираем данные
            var heights = new System.Collections.Generic.List<double>();
            var deltaHs = new System.Collections.Generic.List<double>();
            var stationLengths = new System.Collections.Generic.List<double>();
            var armDifferences = new System.Collections.Generic.List<double>();

            foreach (var row in rows)
            {
                var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                if (height.HasValue) heights.Add(height.Value);

                if (row.DeltaH.HasValue) deltaHs.Add(row.DeltaH.Value);
                if (row.StationLength_m.HasValue) stationLengths.Add(row.StationLength_m.Value);
                if (row.ArmDifference_m.HasValue) armDifferences.Add(row.ArmDifference_m.Value);
            }

            // Статистика высот
            if (heights.Count > 0)
            {
                stats.MinHeight = heights.Min();
                stats.MaxHeight = heights.Max();
                stats.MeanHeight = heights.Average();
                stats.StdDevHeight = CalculateStdDev(heights, stats.MeanHeight);
            }

            // Статистика превышений
            if (deltaHs.Count > 0)
            {
                stats.MinDeltaH = deltaHs.Min();
                stats.MaxDeltaH = deltaHs.Max();
                stats.MeanDeltaH = deltaHs.Average();
                stats.StdDevDeltaH = CalculateStdDev(deltaHs, stats.MeanDeltaH);
                stats.MaxAbsDeltaH = deltaHs.Max(Math.Abs);
            }

            // Статистика длин станций
            if (stationLengths.Count > 0)
            {
                stats.MinStationLength = stationLengths.Min();
                stats.MaxStationLength = stationLengths.Max();
                stats.MeanStationLength = stationLengths.Average();
                stats.StdDevStationLength = CalculateStdDev(stationLengths, stats.MeanStationLength);
                stats.TotalLength = stationLengths.Sum();
            }

            // Статистика разности плеч
            if (armDifferences.Count > 0)
            {
                stats.MinArmDifference = armDifferences.Min();
                stats.MaxArmDifference = armDifferences.Max();
                stats.MeanArmDifference = armDifferences.Average();
                stats.StdDevArmDifference = CalculateStdDev(armDifferences, stats.MeanArmDifference);
            }

            // Поиск аномалий
            DetectOutliers(rows, stats, sensitivitySigma);

            return stats;
        }

        /// <summary>
        /// Вычисляет стандартное отклонение
        /// </summary>
        private double CalculateStdDev(System.Collections.Generic.List<double> values, double mean)
        {
            if (values.Count < 2) return 0;
            var sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / (values.Count - 1));
        }

        /// <summary>
        /// Обнаруживает аномалии (выбросы) в данных
        /// </summary>
        private void DetectOutliers(System.Collections.Generic.List<TraverseRow> rows, ProfileStatistics stats, double sensitivitySigma)
        {
            // 1. Резкие перепады превышений (анализ последовательных разностей)
            for (int i = 1; i < rows.Count; i++)
            {
                var prevDeltaH = rows[i - 1].DeltaH;
                var currDeltaH = rows[i].DeltaH;

                if (prevDeltaH.HasValue && currDeltaH.HasValue)
                {
                    var diff = Math.Abs(currDeltaH.Value - prevDeltaH.Value);
                    var threshold = sensitivitySigma * stats.StdDevDeltaH;

                    if (threshold > 0.001 && diff > threshold)
                    {
                        var deviation = diff / stats.StdDevDeltaH;
                        stats.Outliers.Add(new OutlierPoint
                        {
                            StationIndex = rows[i].Index,
                            PointCode = rows[i].PointCode ?? "—",
                            Value = currDeltaH.Value,
                            ExpectedValue = prevDeltaH.Value,
                            DeviationInSigma = deviation,
                            Type = OutlierType.HeightJump,
                            Description = $"Резкий перепад: Δh = {diff:F4} м ({deviation:F1}σ)",
                            Severity = deviation > 4 ? 3 : (deviation > 3 ? 2 : 1)
                        });
                    }
                }
            }

            // 2. Аномальные длины станций
            if (stats.StdDevStationLength > 0.001)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var length = rows[i].StationLength_m;
                    if (length.HasValue)
                    {
                        var diff = Math.Abs(length.Value - stats.MeanStationLength);
                        var deviation = diff / stats.StdDevStationLength;

                        if (deviation > sensitivitySigma)
                        {
                            stats.Outliers.Add(new OutlierPoint
                            {
                                StationIndex = rows[i].Index,
                                PointCode = rows[i].PointCode ?? "—",
                                Value = length.Value,
                                ExpectedValue = stats.MeanStationLength,
                                DeviationInSigma = deviation,
                                Type = OutlierType.StationLength,
                                Description = $"Аномальная длина: {length.Value:F2} м ({deviation:F1}σ)",
                                Severity = deviation > 4 ? 2 : 1
                            });
                        }
                    }
                }
            }

            // 3. Превышение разности плеч (если есть допуск из класса нивелирования)
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsArmDifferenceExceeded)
                {
                    var armDiff = rows[i].ArmDifference_m;
                    if (armDiff.HasValue)
                    {
                        stats.Outliers.Add(new OutlierPoint
                        {
                            StationIndex = rows[i].Index,
                            PointCode = rows[i].PointCode ?? "—",
                            Value = Math.Abs(armDiff.Value),
                            ExpectedValue = 0,
                            DeviationInSigma = 0,
                            Type = OutlierType.ArmDifference,
                            Description = $"Превышена разность плеч: {Math.Abs(armDiff.Value):F2} м",
                            Severity = 2
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет панель статистики
        /// </summary>
        private void UpdateStatisticsPanel()
        {
            if (_currentStatistics == null) return;

            var stats = _currentStatistics;

            // Обновляем текстовые значения
            if (StatsMinHeight != null) StatsMinHeight.Text = stats.MinHeight.ToString("F2");
            if (StatsMaxHeight != null) StatsMaxHeight.Text = stats.MaxHeight.ToString("F2");
            if (StatsMeanHeight != null) StatsMeanHeight.Text = stats.MeanHeight.ToString("F2");
            if (StatsStdDevHeight != null) StatsStdDevHeight.Text = stats.StdDevHeight.ToString("F3");

            if (StatsMaxDeltaH != null) StatsMaxDeltaH.Text = stats.MaxAbsDeltaH.ToString("F4");
            if (StatsMeanDeltaH != null) StatsMeanDeltaH.Text = stats.MeanDeltaH.ToString("F4");

            if (StatsMeanLength != null) StatsMeanLength.Text = stats.MeanStationLength.ToString("F2");
            if (StatsTotalLength != null) StatsTotalLength.Text = stats.TotalLength.ToString("F2");

            // Обновляем счётчик аномалий
            if (AnomaliesCountText != null)
            {
                AnomaliesCountText.Text = $" {stats.TotalOutliers}";
            }

            // Обновляем цвет иконки аномалий
            if (AnomaliesCountIcon != null)
            {
                if (stats.HasCriticalOutliers)
                {
                    AnomaliesCountIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x36)); // Красный
                }
                else if (stats.HasOutliers)
                {
                    AnomaliesCountIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)); // Желтый
                }
                else
                {
                    AnomaliesCountIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)); // Серый
                }
            }
        }

        /// <summary>
        /// Обработчик изменения чекбокса отображения аномалий
        /// </summary>
        private void ShowAnomaliesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _showAnomalies = ShowAnomaliesCheckBox?.IsChecked ?? true;

            if (_currentTraverseRows != null)
            {
                DrawProportionalProfile(_currentTraverseRows);
            }
        }

        /// <summary>
        /// Обработчик изменения чувствительности анализа
        /// </summary>
        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _sensitivitySigma = e.NewValue;

            if (SensitivityValueText != null)
            {
                SensitivityValueText.Text = _sensitivitySigma.ToString("F1");
            }

            // Пересчитываем статистику с новой чувствительностью
            if (_currentTraverseRows != null)
            {
                _currentStatistics = CalculateStatistics(_currentTraverseRows, _sensitivitySigma);
                DrawProportionalProfile(_currentTraverseRows);
                UpdateStatisticsPanel();
            }
        }

        /// <summary>
        /// Обработчик удаления репера из списка
        /// </summary>
        private void RemoveBenchmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.Tag is BenchmarkItem benchmark &&
                DataContext is TraverseJournalViewModel viewModel)
            {
                viewModel.Calculation.RemoveBenchmarkCommand.Execute(benchmark);
            }
        }

        /// <summary>
        /// Обработчик нажатия Enter в поле ввода высоты репера
        /// </summary>
        private void BenchmarkHeightTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is TraverseJournalViewModel viewModel)
            {
                // Попытка выполнить команду добавления репера
                if (viewModel.Calculation.AddBenchmarkCommand.CanExecute(null))
                {
                    viewModel.Calculation.AddBenchmarkCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
