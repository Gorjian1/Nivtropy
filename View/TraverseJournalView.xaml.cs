using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
        private List<TraverseRow>? _currentTraverseRows;
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

        private TraverseCalculationViewModel? _subscribedCalculation;
        private readonly List<SharedPointLinkItem> _subscribedSharedPoints = new();

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

            // Подписка на изменения DataContext для отрисовки визуализации ходов
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue is TraverseJournalViewModel viewModel)
                {
                    _currentViewModel = viewModel.Calculation;
                    SubscribeToCalculation(viewModel.Calculation);
                    InitializeSystemsPanel();

                    // Начальная отрисовка с задержкой (когда UI полностью загрузится)
                    Dispatcher.BeginInvoke(new System.Action(() => DrawTraverseSystemVisualization()), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };

            // Подписка на Loaded для начальной отрисовки визуализации
            Loaded += (s, e) =>
            {
                if (TraverseVisualizationCanvas != null)
                {
                    TraverseVisualizationCanvas.SizeChanged += (_, __) => DrawTraverseSystemVisualization();
                }
                DrawTraverseSystemVisualization();
            };
        }

        private void SubscribeToCalculation(TraverseCalculationViewModel calculation)
        {
            if (!ReferenceEquals(_subscribedCalculation, calculation))
            {
                UnsubscribeFromCalculation();
                _subscribedCalculation = calculation;

                calculation.PropertyChanged += CalculationOnPropertyChanged;
                calculation.Systems.CollectionChanged += CalculationSystemsChanged;
                calculation.Runs.CollectionChanged += CalculationRunsChanged;
            }

            SubscribeSharedPointHandlers(calculation.SharedPoints);
        }

        private void UnsubscribeFromCalculation()
        {
            if (_subscribedCalculation != null)
            {
                _subscribedCalculation.PropertyChanged -= CalculationOnPropertyChanged;
                _subscribedCalculation.Systems.CollectionChanged -= CalculationSystemsChanged;
                _subscribedCalculation.Runs.CollectionChanged -= CalculationRunsChanged;

                _subscribedCalculation.SharedPoints.CollectionChanged -= SharedPointsOnCollectionChanged;
            }

            foreach (var item in _subscribedSharedPoints)
            {
                item.PropertyChanged -= SharedPointItemOnPropertyChanged;
            }
            _subscribedSharedPoints.Clear();
            _subscribedCalculation = null;
        }

        private void CalculationOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TraverseCalculationViewModel.Rows) ||
                e.PropertyName == nameof(TraverseCalculationViewModel.SelectedSystem))
            {
                Dispatcher.BeginInvoke(new Action(DrawTraverseSystemVisualization));
            }
        }

        private void CalculationSystemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(InitializeSystemsPanel));
        }

        private void CalculationRunsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshRunsList();
                DrawTraverseSystemVisualization();
            }));
        }

        private void SubscribeSharedPointHandlers(IEnumerable<SharedPointLinkItem> sharedPoints)
        {
            foreach (var item in _subscribedSharedPoints)
            {
                item.PropertyChanged -= SharedPointItemOnPropertyChanged;
            }
            _subscribedSharedPoints.Clear();

            foreach (var item in sharedPoints)
            {
                item.PropertyChanged += SharedPointItemOnPropertyChanged;
                _subscribedSharedPoints.Add(item);
            }

            if (_subscribedCalculation != null)
            {
                _subscribedCalculation.SharedPoints.CollectionChanged -= SharedPointsOnCollectionChanged;
                _subscribedCalculation.SharedPoints.CollectionChanged += SharedPointsOnCollectionChanged;
            }
        }

        private void SharedPointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is IEnumerable<SharedPointLinkItem> items)
            {
                SubscribeSharedPointHandlers(items);
            }

            Dispatcher.BeginInvoke(new Action(DrawTraverseSystemVisualization));
        }

        private void SharedPointItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SharedPointLinkItem.IsEnabled))
            {
                Dispatcher.BeginInvoke(new Action(DrawTraverseSystemVisualization));
            }
        }

        private void RunActivationChanged(object sender, RoutedEventArgs e)
        {
            DrawTraverseSystemVisualization();
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

        private TraverseCalculationViewModel? Calculation => _currentViewModel ?? (DataContext as TraverseJournalViewModel)?.Calculation;

        private void InitializeSystemsPanel()
        {
            if (Calculation == null)
                return;

            SystemsListBox.ItemsSource = Calculation.Systems;

            if (SystemsListBox.SelectedItem == null && Calculation.Systems.Count > 0)
            {
                SystemsListBox.SelectedIndex = 0;
            }
            else
            {
                RefreshRunsList();
            }

            DrawTraverseSystemVisualization();
        }

        private void SystemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshRunsList();
        }

        private void RefreshRunsList()
        {
            if (Calculation == null)
                return;

            if (SystemsListBox.SelectedItem is TraverseSystem selectedSystem)
            {
                RunsHeaderText.Text = $"Ходы системы: {selectedSystem.Name}";

                var runsInSystem = Calculation.Runs
                    .Where(r => r.SystemId == selectedSystem.Id)
                    .ToList();

                RunsListBox.ItemsSource = runsInSystem;
            }
            else
            {
                RunsListBox.ItemsSource = null;
                RunsHeaderText.Text = "Ходы системы";
            }
        }

        private void CreateSystem_Click(object sender, RoutedEventArgs e)
        {
            if (Calculation == null)
                return;

            var dialog = new InputDialog("Создание системы", "Введите название системы:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                var newSystem = new TraverseSystem(
                    Guid.NewGuid().ToString(),
                    dialog.ResponseText,
                    Calculation.Systems.Count
                );

                Calculation.Systems.Add(newSystem);
                SystemsListBox.SelectedItem = newSystem;
            }
        }

        private void RenameSystem_Click(object sender, RoutedEventArgs e)
        {
            if (Calculation == null)
                return;

            if (SystemsListBox.SelectedItem is TraverseSystem selectedSystem)
            {
                if (selectedSystem.Id == "system-default")
                {
                    MessageBox.Show(
                        "Систему по умолчанию нельзя переименовать.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var dialog = new InputDialog("Переименование системы", "Введите новое название:", selectedSystem.Name);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
                {
                    selectedSystem.Name = dialog.ResponseText;

                    SystemsListBox.Items.Refresh();
                    RefreshRunsList();
                }
            }
        }

        private void DeleteSystem_Click(object sender, RoutedEventArgs e)
        {
            if (Calculation == null)
                return;

            if (SystemsListBox.SelectedItem is TraverseSystem selectedSystem)
            {
                if (selectedSystem.Id == "system-default")
                {
                    MessageBox.Show(
                        "Систему по умолчанию нельзя удалить.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Удалить систему '{selectedSystem.Name}'?\n\nВсе ходы будут перемещены в систему по умолчанию.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var defaultSystem = Calculation.Systems.FirstOrDefault(s => s.Id == "system-default");
                    if (defaultSystem == null)
                        return;

                    foreach (var run in Calculation.Runs.Where(r => r.SystemId == selectedSystem.Id))
                    {
                        run.SystemId = defaultSystem.Id;
                        selectedSystem.RemoveRun(run.Index);
                        defaultSystem.AddRun(run.Index);
                    }

                    Calculation.Systems.Remove(selectedSystem);
                    SystemsListBox.SelectedItem = defaultSystem;
                }
            }
        }

        private void RefreshSystemsButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeSystemsPanel();
        }

        /// <summary>
        /// Отрисовка визуализации системы ходов с графом связей
        /// </summary>
        private void DrawTraverseSystemVisualization()
        {
            if (TraverseVisualizationCanvas == null)
                return;

            TraverseVisualizationCanvas.Children.Clear();

            if (DataContext is not TraverseJournalViewModel viewModel)
                return;

            var calculation = viewModel.Calculation;
            var runs = calculation.Runs.ToList();

            if (runs.Count == 0)
                return;

            var canvasWidth = TraverseVisualizationCanvas.ActualWidth;
            var canvasHeight = TraverseVisualizationCanvas.ActualHeight;

            if (canvasWidth < 10 || canvasHeight < 10)
                return;

            // Палитра цветов для систем
            var systemColors = new[]
            {
                Color.FromRgb(25, 118, 210),   // Синий
                Color.FromRgb(56, 142, 60),    // Зелёный
                Color.FromRgb(251, 140, 0),    // Оранжевый
                Color.FromRgb(142, 36, 170),   // Фиолетовый
                Color.FromRgb(0, 150, 136),    // Бирюзовый
                Color.FromRgb(211, 47, 47)     // Красный
            };

            var runsBySystem = runs.GroupBy(r => r.SystemId ?? "default").ToList();
            var runColors = new Dictionary<LineSummary, Color>();

            int colorIndex = 0;
            foreach (var systemGroup in runsBySystem)
            {
                var systemColor = systemColors[colorIndex % systemColors.Length];
                foreach (var run in systemGroup)
                {
                    runColors[run] = systemColor;
                }
                colorIndex++;
            }

            // Собираем последовательность точек для каждого хода
            var rows = calculation.Rows.ToList();
            var pointsByRun = new Dictionary<LineSummary, List<string>>();
            foreach (var run in runs)
            {
                var runRows = rows
                    .Where(r => r.LineSummary?.Index == run.Index)
                    .OrderBy(r => r.Index)
                    .ToList();

                var sequence = new List<string>();
                foreach (var row in runRows)
                {
                    if (!string.IsNullOrWhiteSpace(row.BackCode))
                    {
                        var back = row.BackCode.Trim();
                        if (sequence.Count == 0 || !string.Equals(sequence[^1], back, StringComparison.OrdinalIgnoreCase))
                            sequence.Add(back);
                    }

                    if (!string.IsNullOrWhiteSpace(row.ForeCode))
                    {
                        var fore = row.ForeCode.Trim();
                        if (sequence.Count == 0 || !string.Equals(sequence[^1], fore, StringComparison.OrdinalIgnoreCase))
                            sequence.Add(fore);
                    }
                }

                if (sequence.Count == 0 && !string.IsNullOrWhiteSpace(run.StartLabel))
                {
                    sequence.Add(run.StartLabel.Trim());
                    if (!string.Equals(run.StartLabel, run.EndLabel, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(run.EndLabel))
                    {
                        sequence.Add(run.EndLabel.Trim());
                    }
                }

                // Убираем подряд идущие дубликаты и при необходимости замыкаем ход
                var uniqueSequence = new List<string>();
                string? lastCode = null;
                foreach (var code in sequence)
                {
                    if (string.Equals(code, lastCode, StringComparison.OrdinalIgnoreCase))
                        continue;

                    uniqueSequence.Add(code);
                    lastCode = code;
                }

                if (uniqueSequence.Count > 2
                    && string.Equals(run.StartLabel, run.EndLabel, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uniqueSequence[0], uniqueSequence[^1], StringComparison.OrdinalIgnoreCase))
                {
                    uniqueSequence.Add(uniqueSequence[0]);
                }

                pointsByRun[run] = uniqueSequence;
            }

            // Общие точки (включенные) для связности
            const double padding = 18;
            double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
            Point ClampPoint(Point p, double margin)
            {
                return new Point(
                    Clamp(p.X, margin, canvasWidth - margin),
                    Clamp(p.Y, margin, canvasHeight - margin));
            }

            double GetMaxRadius(Point c, double margin)
            {
                var dx = Math.Min(c.X - margin, canvasWidth - margin - c.X);
                var dy = Math.Min(c.Y - margin, canvasHeight - margin - c.Y);
                return Math.Max(0, Math.Min(dx, dy));
            }

            var runShapeRadius = runs.ToDictionary(
                run => run,
                run => 22 + Math.Max(pointsByRun.TryGetValue(run, out var seq) ? seq.Count : 0, 2) * 6);

            var maxShapeRadius = runShapeRadius.Values.Count > 0 ? runShapeRadius.Values.Max() : 30;

            var sharedPoints = calculation.SharedPoints.Where(p => p.IsEnabled).ToList();

            var sharedPointPositions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            var center = new Point(canvasWidth / 2, canvasHeight / 2);
            var drawableRadius = Math.Min(canvasWidth, canvasHeight) / 2 - (padding + maxShapeRadius);
            var sharedRadius = Math.Max(24, drawableRadius);

            for (int i = 0; i < sharedPoints.Count; i++)
            {
                var angle = 2 * Math.PI * i / Math.Max(1, sharedPoints.Count);
                var point = new Point(
                    center.X + sharedRadius * Math.Cos(angle),
                    center.Y + sharedRadius * Math.Sin(angle));
                sharedPointPositions[sharedPoints[i].Code] = ClampPoint(point, padding + 6);
            }

            // Центры ходов: для связанных — около средних общих точек, для несвязанных — отдельное кольцо
            var runCenters = new Dictionary<LineSummary, Point>();
            var orbitBase = Math.Min(canvasWidth, canvasHeight) / 2 - (padding + maxShapeRadius);
            var orbitRadius = sharedPoints.Count > 0 ? Math.Max(18, sharedRadius * 0.82) : Math.Max(24, orbitBase);

            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                var sharedForRun = calculation.GetSharedPointsForRun(run)
                    .Where(p => p.IsEnabled)
                    .Select(p => p.Code)
                    .ToList();

                var anchors = sharedForRun
                    .Where(code => sharedPointPositions.ContainsKey(code))
                    .Select(code => sharedPointPositions[code])
                    .ToList();

                var shapeRadius = runShapeRadius.TryGetValue(run, out var r) ? r : 32;

                if (anchors.Count > 0)
                {
                    var avgX = anchors.Average(p => p.X);
                    var avgY = anchors.Average(p => p.Y);
                    var basePoint = new Point(avgX, avgY);

                    // Небольшое смещение, чтобы фигуры с одинаковыми общими точками не налегали друг на друга
                    var jitterAngle = ((run.Index * 37) % 360) * Math.PI / 180.0;
                    var jitterDistance = (anchors.Count > 1 ? 10 : 18) + (i % 3) * 3;
                    var offset = new Vector(Math.Cos(jitterAngle) * jitterDistance, Math.Sin(jitterAngle) * jitterDistance);
                    var proposed = basePoint + offset;
                    var margin = padding + shapeRadius;
                    runCenters[run] = ClampPoint(proposed, margin);
                }
                else
                {
                    var angle = 2 * Math.PI * i / Math.Max(1, runs.Count);
                    var radius = orbitRadius + (sharedPoints.Count > 0 ? 24 : 0);
                    var proposed = new Point(
                        center.X + radius * Math.Cos(angle),
                        center.Y + radius * Math.Sin(angle));
                    var margin = padding + shapeRadius;
                    runCenters[run] = ClampPoint(proposed, margin);
                }
            }

            // Лёгкое раздвижение центров фигур, чтобы они меньше перекрывались
            for (int iteration = 0; iteration < 18; iteration++)
            {
                foreach (var a in runs)
                {
                    foreach (var b in runs)
                    {
                        if (a == b) continue;

                        var ca = runCenters[a];
                        var cb = runCenters[b];
                        var delta = cb - ca;
                        var distance = delta.Length;
                        var target = runShapeRadius[a] + runShapeRadius[b] + padding * 0.6;

                        if (distance <= 0.01 || distance >= target)
                            continue;

                        delta.Normalize();
                        var push = (target - distance) / 2;
                        var shiftA = new Point(ca.X - delta.X * push, ca.Y - delta.Y * push);
                        var shiftB = new Point(cb.X + delta.X * push, cb.Y + delta.Y * push);

                        runCenters[a] = ClampPoint(shiftA, padding + runShapeRadius[a]);
                        runCenters[b] = ClampPoint(shiftB, padding + runShapeRadius[b]);
                    }
                }
            }

            var drawnSharedPoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Рисуем каждый ход как сглаженную фигуру
            foreach (var run in runs)
            {
                if (!pointsByRun.TryGetValue(run, out var pointSequence) || pointSequence.Count < 2)
                    continue;

                var runColor = runColors.TryGetValue(run, out var c) ? c : Colors.SteelBlue;
                var strokeColor = run.IsActive ? runColor : Color.FromRgb(140, 140, 140);
                var fillColor = Color.FromArgb(30, runColor.R, runColor.G, runColor.B);

                var centerPoint = runCenters[run];
                var pointCount = Math.Max(pointSequence.Count, 2);
                var shapeRadius = runShapeRadius.TryGetValue(run, out var radius) ? radius : 32;
                shapeRadius = Math.Min(shapeRadius, GetMaxRadius(centerPoint, padding));

                var vertices = new List<Point>();
                for (int i = 0; i < pointSequence.Count; i++)
                {
                    var code = pointSequence[i];
                    if (sharedPointPositions.TryGetValue(code, out var sharedPos))
                    {
                        vertices.Add(sharedPos);
                        continue;
                    }

                    var angle = 2 * Math.PI * i / pointCount;
                    var vertex = new Point(
                        centerPoint.X + shapeRadius * Math.Cos(angle),
                        centerPoint.Y + shapeRadius * Math.Sin(angle));
                    vertices.Add(ClampPoint(vertex, padding));
                }

                bool isClosed = string.Equals(pointSequence.First(), pointSequence.Last(), StringComparison.OrdinalIgnoreCase);
                var geometry = BuildSmoothGeometry(vertices, isClosed);

                var path = new Path
                {
                    Data = geometry,
                    Stroke = new SolidColorBrush(strokeColor),
                    StrokeThickness = run.IsActive ? 2.2 : 1.5,
                    Fill = new SolidColorBrush(fillColor),
                    StrokeDashArray = run.IsActive ? null : new DoubleCollection { 3, 3 },
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    ToolTip = run.Tooltip
                };

                TraverseVisualizationCanvas.Children.Add(path);

                // Подпись хода
                var centroid = new Point(vertices.Average(v => v.X), vertices.Average(v => v.Y));
                var label = new TextBlock
                {
                    Text = run.DisplayName,
                    FontSize = 10,
                    FontWeight = run.IsActive ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(strokeColor),
                    Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                };
                Canvas.SetLeft(label, centroid.X - 18);
                Canvas.SetTop(label, centroid.Y - 10);
                TraverseVisualizationCanvas.Children.Add(label);

                // Отрисовка точек фигуры
                var sharedForRun = new HashSet<string>(calculation.GetSharedPointsForRun(run)
                    .Where(p => p.IsEnabled)
                    .Select(p => p.Code), StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < pointSequence.Count; i++)
                {
                    var code = pointSequence[i];
                    var pointPos = vertices[i];

                    bool isShared = sharedForRun.Contains(code);
                    bool hasKnownHeight = calculation.HasKnownHeight(code) || isShared;

                    var pointColor = hasKnownHeight
                        ? Colors.Black
                        : isShared
                            ? Colors.OrangeRed
                            : Color.FromArgb(220, runColor.R, runColor.G, runColor.B);

                    if (!isShared || drawnSharedPoints.Add(code))
                    {
                        var node = new Ellipse
                        {
                            Width = hasKnownHeight ? 11 : 9,
                            Height = hasKnownHeight ? 11 : 9,
                            Fill = new SolidColorBrush(pointColor),
                            Stroke = Brushes.White,
                            StrokeThickness = 2,
                            ToolTip = $"{code}\n{(hasKnownHeight ? "Известная" : isShared ? "Общая" : "Точка хода" )}"
                        };

                        Canvas.SetLeft(node, pointPos.X - node.Width / 2);
                        Canvas.SetTop(node, pointPos.Y - node.Height / 2);
                        TraverseVisualizationCanvas.Children.Add(node);

                        var direction = pointPos - centerPoint;
                        if (direction.Length < 0.001)
                            direction = new Vector(0, -1);
                        else
                            direction.Normalize();

                        var labelOffset = direction * (node.Width / 2 + 6);
                        var pointLabel = new TextBlock
                        {
                            Text = code,
                            FontSize = 8,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brushes.Black,
                            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
                        };

                        Canvas.SetLeft(pointLabel, pointPos.X + labelOffset.X - 6);
                        Canvas.SetTop(pointLabel, pointPos.Y + labelOffset.Y - 8);
                        TraverseVisualizationCanvas.Children.Add(pointLabel);
                    }
                }
            }
        }

        private static StreamGeometry BuildSmoothGeometry(IReadOnlyList<Point> points, bool isClosed)
        {
            var geometry = new StreamGeometry();
            if (points.Count == 0)
                return geometry;

            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], true, isClosed);

                if (points.Count == 1)
                    return geometry;

                Point GetPoint(int index)
                {
                    if (index < 0)
                        return points[0];
                    if (index >= points.Count)
                        return points[^1];
                    return points[index];
                }

                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p0 = GetPoint(i - 1);
                    var p1 = GetPoint(i);
                    var p2 = GetPoint(i + 1);
                    var p3 = GetPoint(i + 2);

                    if (!isClosed)
                    {
                        if (i == 0)
                            p0 = p1;
                        if (i == points.Count - 2)
                            p3 = p2;
                    }

                    var cp1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
                    var cp2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);

                    context.BezierTo(cp1, cp2, p2, true, true);
                }

                if (isClosed)
                {
                    var pLast = points[^1];
                    var pFirst = points[0];
                    var pNext = points.Count > 1 ? points[1] : pFirst;

                    var cp1 = new Point(pLast.X + (pFirst.X - GetPoint(points.Count - 2).X) / 6,
                        pLast.Y + (pFirst.Y - GetPoint(points.Count - 2).Y) / 6);
                    var cp2 = new Point(pFirst.X - (pNext.X - pLast.X) / 6,
                        pFirst.Y - (pNext.Y - pLast.Y) / 6);

                    context.BezierTo(cp1, cp2, pFirst, true, true);
                }
            }

            geometry.Freeze();
            return geometry;
        }
    }
}
