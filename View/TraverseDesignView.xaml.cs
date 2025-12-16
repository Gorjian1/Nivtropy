using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Models;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseDesignView : UserControl
    {
        private DataGeneratorViewModel? _viewModel;

        public TraverseDesignView()
        {
            InitializeComponent();

            DataContextChanged += TraverseDesignView_DataContextChanged;
            Loaded += TraverseDesignView_Loaded;
        }

        private void TraverseDesignView_Loaded(object sender, RoutedEventArgs e)
        {
            AttachViewModel();
        }

        private void TraverseDesignView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachViewModel();
        }

        private void AttachViewModel()
        {
            if (_viewModel != null)
            {
                _viewModel.Measurements.CollectionChanged -= Measurements_CollectionChanged;
                foreach (var measurement in _viewModel.Measurements)
                {
                    measurement.PropertyChanged -= Measurement_PropertyChanged;
                }
            }

            _viewModel = DataContext as DataGeneratorViewModel;

            if (_viewModel != null)
            {
                _viewModel.Measurements.CollectionChanged += Measurements_CollectionChanged;
                foreach (var measurement in _viewModel.Measurements)
                {
                    measurement.PropertyChanged += Measurement_PropertyChanged;
                }
            }

            RedrawProfile();
        }

        private void Measurements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<GeneratedMeasurement>())
                {
                    item.PropertyChanged -= Measurement_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<GeneratedMeasurement>())
                {
                    item.PropertyChanged += Measurement_PropertyChanged;
                }
            }

            RedrawProfile();
        }

        private void Measurement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GeneratedMeasurement.Height_m))
            {
                RedrawProfile();
            }
        }

        private void GeneratedProfileCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawProfile();
        }

        private void RedrawProfile()
        {
            if (GeneratedProfileCanvas == null)
            {
                return;
            }

            GeneratedProfileCanvas.Children.Clear();

            if (_viewModel == null)
            {
                return;
            }

            var pointsWithHeight = _viewModel.Measurements
                .Where(m => m.Height_m.HasValue)
                .OrderBy(m => m.Index)
                .ToList();

            if (pointsWithHeight.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "Нет точек с высотой",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic
                };

                double centerX = GeneratedProfileCanvas.ActualWidth / 2 - 60;
                double centerY = GeneratedProfileCanvas.ActualHeight / 2 - 10;
                Canvas.SetLeft(emptyText, Math.Max(8, centerX));
                Canvas.SetTop(emptyText, Math.Max(8, centerY));
                GeneratedProfileCanvas.Children.Add(emptyText);
                return;
            }

            double width = GeneratedProfileCanvas.ActualWidth;
            double height = GeneratedProfileCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            const double padding = 24;
            double plotWidth = Math.Max(0, width - padding * 2);
            double plotHeight = Math.Max(0, height - padding * 2);

            double minZ = pointsWithHeight.Min(p => p.Height_m!.Value);
            double maxZ = pointsWithHeight.Max(p => p.Height_m!.Value);
            double deltaZ = Math.Max(maxZ - minZ, 0.0001);

            double stepX = pointsWithHeight.Count > 1
                ? plotWidth / (pointsWithHeight.Count - 1)
                : plotWidth;

            var polyline = new Polyline
            {
                Stroke = (Brush)new BrushConverter().ConvertFromString("#005A9E")!,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            for (int i = 0; i < pointsWithHeight.Count; i++)
            {
                double x = padding + stepX * i;
                double normalized = (pointsWithHeight[i].Height_m!.Value - minZ) / deltaZ;
                double y = padding + plotHeight * (1 - normalized);

                polyline.Points.Add(new Point(x, y));

                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = Brushes.White,
                    Stroke = Brushes.DimGray,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(ellipse, x - ellipse.Width / 2);
                Canvas.SetTop(ellipse, y - ellipse.Height / 2);
                GeneratedProfileCanvas.Children.Add(ellipse);

                var label = new TextBlock
                {
                    Text = pointsWithHeight[i].PointCode,
                    Foreground = Brushes.DimGray,
                    FontSize = 10
                };

                Canvas.SetLeft(label, x - 12);
                Canvas.SetTop(label, y - 18);
                GeneratedProfileCanvas.Children.Add(label);
            }

            GeneratedProfileCanvas.Children.Insert(0, polyline);

            // Оси
            var axisBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
            var xAxis = new Line
            {
                X1 = padding,
                X2 = padding + plotWidth,
                Y1 = padding + plotHeight,
                Y2 = padding + plotHeight,
                Stroke = axisBrush,
                StrokeThickness = 1
            };

            var yAxis = new Line
            {
                X1 = padding,
                X2 = padding,
                Y1 = padding,
                Y2 = padding + plotHeight,
                Stroke = axisBrush,
                StrokeThickness = 1
            };

            GeneratedProfileCanvas.Children.Insert(0, yAxis);
            GeneratedProfileCanvas.Children.Insert(0, xAxis);

            // Подписи по высоте
            var minText = new TextBlock
            {
                Text = $"Мин: {minZ:F3}",
                Foreground = Brushes.Gray,
                FontSize = 11
            };

            var maxText = new TextBlock
            {
                Text = $"Макс: {maxZ:F3}",
                Foreground = Brushes.Gray,
                FontSize = 11
            };

            Canvas.SetLeft(minText, padding + 4);
            Canvas.SetTop(minText, padding + plotHeight - 18);

            Canvas.SetLeft(maxText, padding + 4);
            Canvas.SetTop(maxText, padding - 6);

            GeneratedProfileCanvas.Children.Add(minText);
            GeneratedProfileCanvas.Children.Add(maxText);
        }
    }
}
