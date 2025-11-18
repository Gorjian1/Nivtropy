using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseCalculationView : UserControl
    {
        private System.Collections.Generic.List<Models.TraverseRow>? _currentTraverseRows;
        private TraverseCalculationViewModel? _currentViewModel;

        // Сохраняемые настройки
        private static Color _savedProfileColor = Color.FromRgb(0x19, 0x76, 0xD2);
        private static int _savedColorIndex = 0;
        private static double? _savedMinHeight;
        private static double? _savedMaxHeight;

        private Color _profileColor;
        private double? _manualMinHeight;
        private double? _manualMaxHeight;

        public TraverseCalculationView()
        {
            InitializeComponent();

            // Добавляем обработчики для зума
            this.PreviewKeyDown += TraverseCalculationView_PreviewKeyDown;
            this.PreviewMouseWheel += TraverseCalculationView_PreviewMouseWheel;
            this.Focusable = true;

            // Восстанавливаем сохранённые настройки
            _profileColor = _savedProfileColor;
            _manualMinHeight = _savedMinHeight;
            _manualMaxHeight = _savedMaxHeight;

            // Loaded event для установки SelectedIndex после инициализации
            this.Loaded += (s, e) =>
            {
                if (ProfileColorComboBox != null)
                    ProfileColorComboBox.SelectedIndex = _savedColorIndex;
            };
        }

        /// <summary>
        /// Показать детали хода (всплывающее окно)
        /// </summary>
        private void ShowTraverseDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string traverseName && DataContext is TraverseCalculationViewModel viewModel)
            {
                // Находим данные хода
                var traverseRows = viewModel.Rows.Where(r => r.LineName == traverseName).ToList();
                if (traverseRows.Any())
                {
                    _currentTraverseRows = traverseRows;
                    _currentViewModel = viewModel;

                    // Устанавливаем заголовок
                    ProfileTitleText.Text = $"Профиль хода: {traverseName}";

                    // Автоматически заполняем поля мин/макс высоты, если они не были сохранены
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

                    // Показываем popup
                    TraverseDetailsPopup.IsOpen = true;

                    // Рисуем профиль после открытия
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

            // Собираем высоты и расстояния
            var points = new System.Collections.Generic.List<(double height, double distance, string pointCode, int index)>();
            double cumulativeDistance = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                var pointCode = row.PointCode ?? "";

                if (height.HasValue)
                {
                    points.Add((height.Value, cumulativeDistance, pointCode, i + 1));
                    cumulativeDistance += row.StationLength_m ?? 0;
                }
            }

            if (points.Count < 2)
                return;

            // Определяем диапазон высот
            var minHeight = _manualMinHeight ?? points.Min(p => p.height);
            var maxHeight = _manualMaxHeight ?? points.Max(p => p.height);
            var heightRange = maxHeight - minHeight;
            if (heightRange < 0.001)
                heightRange = 1.0;

            // Определяем общую длину
            var totalDistance = points[^1].distance;
            if (totalDistance < 0.001)
                totalDistance = 1.0;

            var margin = 50;
            var plotWidth = canvasWidth - 2 * margin;
            var plotHeight = canvasHeight - 2 * margin;

            // Рисуем сетку
            DrawGrid(canvasWidth, canvasHeight, margin, plotWidth, plotHeight, minHeight, maxHeight, totalDistance);

            // Определяем общие точки и точки с известной высотой
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

            // Рисуем профиль
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

            // Последняя точка
            var lastX = margin + (points[^1].distance / totalDistance) * plotWidth;
            var lastY = canvasHeight - margin - ((points[^1].height - minHeight) / heightRange * plotHeight);
            DrawPoint(lastX, lastY, points[^1].pointCode, points[^1].index, points[^1].height,
                     sharedPoints.Contains(points[^1].pointCode),
                     knownHeightPoints.Contains(points[^1].pointCode),
                     profileBrush);
        }

        private void DrawGrid(double canvasWidth, double canvasHeight, double margin,
                              double plotWidth, double plotHeight,
                              double minHeight, double maxHeight, double totalDistance)
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

            // Вертикальные линии сетки
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

            // Горизонтальные линии сетки
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

            // Оси
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

            // Подписи осей
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

                // Сохраняем настройки
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

        /// <summary>
        /// Обработка нажатий клавиш для зума (+/-)
        /// </summary>
        private void TraverseCalculationView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is TraverseCalculationViewModel viewModel)
            {
                var settings = viewModel.Settings;

                // Обработка клавиш + и =
                if ((e.Key == Key.Add || e.Key == Key.OemPlus) && settings.TableFontSize < 20)
                {
                    settings.TableFontSize += 1;
                    e.Handled = true;
                }
                // Обработка клавиши -
                else if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && settings.TableFontSize > 10)
                {
                    settings.TableFontSize -= 1;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Обработка Ctrl+MouseWheel для зума
        /// </summary>
        private void TraverseCalculationView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is TraverseCalculationViewModel viewModel)
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
        }
    }
}
