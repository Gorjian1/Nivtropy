using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseDesignView : UserControl
    {
        public TraverseDesignView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (DataContext is INotifyPropertyChanged newViewModel)
            {
                newViewModel.PropertyChanged += OnViewModelPropertyChanged;
                DrawProfile();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawProfile();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ProfileDataChanged")
            {
                DrawProfile();
            }
        }

        private void DrawProfile()
        {
            if (!(DataContext is DataGeneratorViewModel viewModel))
                return;

            ProfileCanvas.Children.Clear();

            var measurements = viewModel.GetCurrentTraverseMeasurements();
            if (measurements.Count == 0)
                return;

            var heights = measurements.Where(m => m.Height_m.HasValue).Select(m => m.Height_m!.Value).ToList();
            if (heights.Count < 2)
                return;

            double minHeight = heights.Min();
            double maxHeight = heights.Max();
            double heightRange = maxHeight - minHeight;
            if (heightRange < 0.001) heightRange = 1.0; // Предотвращаем деление на 0

            double canvasWidth = ProfileCanvas.ActualWidth;
            double canvasHeight = ProfileCanvas.ActualHeight;
            if (canvasWidth < 10 || canvasHeight < 10)
                return;

            // Отступы
            double marginLeft = 40;
            double marginRight = 20;
            double marginTop = 20;
            double marginBottom = 30;

            double plotWidth = canvasWidth - marginLeft - marginRight;
            double plotHeight = canvasHeight - marginTop - marginBottom;

            // Рисуем оси
            var axisLine = new Line
            {
                X1 = marginLeft,
                Y1 = marginTop,
                X2 = marginLeft,
                Y2 = marginTop + plotHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            ProfileCanvas.Children.Add(axisLine);

            var baseLinе = new Line
            {
                X1 = marginLeft,
                Y1 = marginTop + plotHeight,
                X2 = marginLeft + plotWidth,
                Y2 = marginTop + plotHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            ProfileCanvas.Children.Add(baseLinе);

            // Рисуем профиль
            var polyline = new Polyline
            {
                Stroke = Brushes.Blue,
                StrokeThickness = 2,
                Fill = Brushes.LightBlue,
                Opacity = 0.5
            };

            double stepX = plotWidth / (heights.Count - 1);

            for (int i = 0; i < heights.Count; i++)
            {
                double x = marginLeft + i * stepX;
                double normalizedHeight = (heights[i] - minHeight) / heightRange;
                double y = marginTop + plotHeight - (normalizedHeight * plotHeight);

                polyline.Points.Add(new Point(x, y));

                // Добавляем точку
                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = Brushes.DarkBlue
                };
                Canvas.SetLeft(ellipse, x - 3);
                Canvas.SetTop(ellipse, y - 3);
                ProfileCanvas.Children.Add(ellipse);
            }

            // Замыкаем полигон для заливки
            polyline.Points.Add(new Point(marginLeft + plotWidth, marginTop + plotHeight));
            polyline.Points.Add(new Point(marginLeft, marginTop + plotHeight));

            ProfileCanvas.Children.Add(polyline);

            // Добавляем подписи высот (макс и мин)
            var maxLabel = new TextBlock
            {
                Text = $"{maxHeight:F2} м",
                FontSize = 10,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(maxLabel, 5);
            Canvas.SetTop(maxLabel, marginTop);
            ProfileCanvas.Children.Add(maxLabel);

            var minLabel = new TextBlock
            {
                Text = $"{minHeight:F2} м",
                FontSize = 10,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(minLabel, 5);
            Canvas.SetTop(minLabel, marginTop + plotHeight - 15);
            ProfileCanvas.Children.Add(minLabel);
        }
    }
}
