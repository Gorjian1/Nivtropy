using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseCalculationView : UserControl
    {
        private System.Collections.Generic.List<Models.TraverseRow>? _currentTraverseRows;
        private Color _profileColor = Color.FromRgb(0x19, 0x76, 0xD2); // Синий по умолчанию
        private double? _manualMinHeight;
        private double? _manualMaxHeight;

        public TraverseCalculationView()
        {
            InitializeComponent();

            // Добавляем обработчики для зума
            this.PreviewKeyDown += TraverseCalculationView_PreviewKeyDown;
            this.PreviewMouseWheel += TraverseCalculationView_PreviewMouseWheel;
            this.Focusable = true;
        }

        /// <summary>
        /// Показать детали хода (всплывающее окно)
        /// </summary>
        private void ShowTraverseDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string traverseName && DataContext is TraverseCalculationViewModel viewModel)
            {
                // Устанавливаем PlacementTarget
                TraverseDetailsPopup.PlacementTarget = button;

                // Обновляем заголовок
                PopupTitle.Text = $"Профиль хода: {traverseName}";

                // Находим данные хода
                var traverseRows = viewModel.Rows.Where(r => r.LineName == traverseName).ToList();
                if (traverseRows.Any())
                {
                    _currentTraverseRows = traverseRows;

                    // Автоматически заполняем поля мин/макс высоты
                    var heights = new System.Collections.Generic.List<double>();
                    foreach (var row in traverseRows)
                    {
                        var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                        if (height.HasValue)
                            heights.Add(height.Value);
                    }

                    if (heights.Any())
                    {
                        MinHeightTextBox.Text = heights.Min().ToString("F2");
                        MaxHeightTextBox.Text = heights.Max().ToString("F2");
                    }

                    // Рисуем профиль
                    DrawProportionalProfile(traverseRows);
                }

                // Открываем popup
                TraverseDetailsPopup.IsOpen = true;
            }
        }

        /// <summary>
        /// Рисует профиль хода с пропорциональными расстояниями
        /// </summary>
        private void DrawProportionalProfile(System.Collections.Generic.List<Models.TraverseRow> rows)
        {
            ProfileCanvas.Children.Clear();

            if (rows.Count < 2)
                return;

            var canvasWidth = ProfileCanvas.ActualWidth > 0 ? ProfileCanvas.ActualWidth : 564;
            var canvasHeight = ProfileCanvas.ActualHeight > 0 ? ProfileCanvas.ActualHeight : 300;

            // Собираем высоты и расстояния
            var points = new System.Collections.Generic.List<(double height, double distance)>();
            double cumulativeDistance = 0;

            foreach (var row in rows)
            {
                var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                if (height.HasValue)
                {
                    points.Add((height.Value, cumulativeDistance));
                    // Добавляем длину станции для следующей точки
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

            var margin = 40;
            var plotWidth = canvasWidth - 2 * margin;
            var plotHeight = canvasHeight - 2 * margin;

            // Рисуем оси
            var axisLine = new Line
            {
                X1 = margin,
                Y1 = canvasHeight - margin,
                X2 = canvasWidth - margin,
                Y2 = canvasHeight - margin,
                Stroke = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                StrokeThickness = 1
            };
            ProfileCanvas.Children.Add(axisLine);

            var leftAxisLine = new Line
            {
                X1 = margin,
                Y1 = margin,
                X2 = margin,
                Y2 = canvasHeight - margin,
                Stroke = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                StrokeThickness = 1
            };
            ProfileCanvas.Children.Add(leftAxisLine);

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

                // Точка
                var ellipse = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = profileBrush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(ellipse, x1 - 3.5);
                Canvas.SetTop(ellipse, y1 - 3.5);
                ProfileCanvas.Children.Add(ellipse);

                // Подпись расстояния
                if (i > 0 && rows[i].StationLength_m.HasValue)
                {
                    var distLabel = new TextBlock
                    {
                        Text = $"{rows[i].StationLength_m.Value:F0}м",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
                    };
                    Canvas.SetLeft(distLabel, (x1 + x2) / 2 - 15);
                    Canvas.SetTop(distLabel, canvasHeight - margin + 5);
                    ProfileCanvas.Children.Add(distLabel);
                }
            }

            // Последняя точка
            var lastX = margin + (points[^1].distance / totalDistance) * plotWidth;
            var lastY = canvasHeight - margin - ((points[^1].height - minHeight) / heightRange * plotHeight);
            var lastEllipse = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = profileBrush,
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(lastEllipse, lastX - 3.5);
            Canvas.SetTop(lastEllipse, lastY - 3.5);
            ProfileCanvas.Children.Add(lastEllipse);

            // Подписи высот
            var minLabel = new TextBlock
            {
                Text = $"{minHeight:F3} м",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            Canvas.SetRight(minLabel, canvasWidth - margin + 5);
            Canvas.SetBottom(minLabel, margin - 5);
            ProfileCanvas.Children.Add(minLabel);

            var maxLabel = new TextBlock
            {
                Text = $"{maxHeight:F3} м",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            Canvas.SetRight(maxLabel, canvasWidth - margin + 5);
            Canvas.SetTop(maxLabel, margin - 5);
            ProfileCanvas.Children.Add(maxLabel);

            // Подпись общей длины
            var totalLabel = new TextBlock
            {
                Text = $"Σ {totalDistance:F2} м",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            Canvas.SetLeft(totalLabel, canvasWidth - margin - 60);
            Canvas.SetTop(totalLabel, canvasHeight - margin + 5);
            ProfileCanvas.Children.Add(totalLabel);
        }

        /// <summary>
        /// Изменение цвета профиля
        /// </summary>
        private void ProfileColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileColorComboBox.SelectedItem is ComboBoxItem item && item.Tag is string colorHex)
            {
                _profileColor = (Color)ColorConverter.ConvertFromString(colorHex);

                if (_currentTraverseRows != null)
                    DrawProportionalProfile(_currentTraverseRows);
            }
        }

        /// <summary>
        /// Изменение границ высот
        /// </summary>
        private void HeightLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentTraverseRows == null)
                return;

            // Парсим минимальную высоту
            if (double.TryParse(MinHeightTextBox.Text, out var minH))
                _manualMinHeight = minH;
            else
                _manualMinHeight = null;

            // Парсим максимальную высоту
            if (double.TryParse(MaxHeightTextBox.Text, out var maxH))
                _manualMaxHeight = maxH;
            else
                _manualMaxHeight = null;

            // Перерисовываем
            DrawProportionalProfile(_currentTraverseRows);
        }

        /// <summary>
        /// Закрыть popup по кнопке
        /// </summary>
        private void ClosePopup_Click(object sender, RoutedEventArgs e)
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
