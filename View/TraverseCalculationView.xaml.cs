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
                PopupTitle.Text = $"Ход: {traverseName}";

                // Находим данные хода
                var traverseRows = viewModel.Rows.Where(r => r.LineName == traverseName).ToList();
                if (traverseRows.Any())
                {
                    var firstRow = traverseRows.First();
                    PopupStations.Text = $"Станций: {traverseRows.Count}";
                    PopupClosure.Text = $"ΣΔh: {traverseRows.Sum(r => r.DeltaH ?? 0):+0.0000;-0.0000;0.0000} м";
                    PopupLength.Text = $"Длина: {firstRow.LineSummary.TotalAverageLength:0.00} м";

                    // Рисуем простой профиль
                    DrawSimpleProfile(traverseRows);
                }

                // Открываем popup
                TraverseDetailsPopup.IsOpen = true;
            }
        }

        /// <summary>
        /// Рисует простой профиль хода
        /// </summary>
        private void DrawSimpleProfile(System.Collections.Generic.List<Models.TraverseRow> rows)
        {
            ProfileCanvas.Children.Clear();

            if (rows.Count < 2)
                return;

            var canvasWidth = ProfileCanvas.ActualWidth > 0 ? ProfileCanvas.ActualWidth : 460;
            var canvasHeight = ProfileCanvas.ActualHeight > 0 ? ProfileCanvas.ActualHeight : 220;

            // Собираем высоты
            var heights = new System.Collections.Generic.List<double>();
            foreach (var row in rows)
            {
                var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                if (height.HasValue)
                    heights.Add(height.Value);
            }

            if (heights.Count < 2)
                return;

            var minHeight = heights.Min();
            var maxHeight = heights.Max();
            var heightRange = maxHeight - minHeight;
            if (heightRange < 0.001)
                heightRange = 1.0; // Избегаем деления на ноль

            var margin = 30;
            var plotWidth = canvasWidth - 2 * margin;
            var plotHeight = canvasHeight - 2 * margin;

            // Рисуем оси
            var axisLine = new Line
            {
                X1 = margin,
                Y1 = canvasHeight - margin,
                X2 = canvasWidth - margin,
                Y2 = canvasHeight - margin,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            ProfileCanvas.Children.Add(axisLine);

            // Рисуем профиль
            var stepX = plotWidth / (heights.Count - 1);

            for (int i = 0; i < heights.Count - 1; i++)
            {
                var x1 = margin + i * stepX;
                var y1 = canvasHeight - margin - ((heights[i] - minHeight) / heightRange * plotHeight);
                var x2 = margin + (i + 1) * stepX;
                var y2 = canvasHeight - margin - ((heights[i + 1] - minHeight) / heightRange * plotHeight);

                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)),
                    StrokeThickness = 2
                };
                ProfileCanvas.Children.Add(line);

                // Точка
                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2))
                };
                Canvas.SetLeft(ellipse, x1 - 3);
                Canvas.SetTop(ellipse, y1 - 3);
                ProfileCanvas.Children.Add(ellipse);
            }

            // Последняя точка
            var lastX = margin + (heights.Count - 1) * stepX;
            var lastY = canvasHeight - margin - ((heights[^1] - minHeight) / heightRange * plotHeight);
            var lastEllipse = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2))
            };
            Canvas.SetLeft(lastEllipse, lastX - 3);
            Canvas.SetTop(lastEllipse, lastY - 3);
            ProfileCanvas.Children.Add(lastEllipse);

            // Подписи высот
            var minLabel = new TextBlock
            {
                Text = $"{minHeight:F2} м",
                FontSize = 10,
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(minLabel, 5);
            Canvas.SetBottom(minLabel, margin - 5);
            ProfileCanvas.Children.Add(minLabel);

            var maxLabel = new TextBlock
            {
                Text = $"{maxHeight:F2} м",
                FontSize = 10,
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(maxLabel, 5);
            Canvas.SetTop(maxLabel, margin - 15);
            ProfileCanvas.Children.Add(maxLabel);
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
