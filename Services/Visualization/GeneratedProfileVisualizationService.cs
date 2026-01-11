using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Models;

namespace Nivtropy.Services.Visualization
{
    /// <summary>
    /// Интерфейс сервиса визуализации профиля сгенерированных данных
    /// </summary>
    public interface IGeneratedProfileVisualizationService
    {
        ProfileRenderResult? DrawProfile(
            Canvas canvas,
            IReadOnlyCollection<GeneratedMeasurement> measurements,
            double? minHeightOverride = null,
            double? maxHeightOverride = null);
    }

    /// <summary>
    /// Визуализация профиля сгенерированных данных в окне "Проектирование".
    /// </summary>
    public class GeneratedProfileVisualizationService : IGeneratedProfileVisualizationService
    {
        private const double Margin = 48;

        public ProfileRenderResult? DrawProfile(
            Canvas canvas,
            IReadOnlyCollection<GeneratedMeasurement> measurements,
            double? minHeightOverride = null,
            double? maxHeightOverride = null)
        {
            canvas.Children.Clear();

            if (measurements.Count == 0 || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            {
                DrawPlaceholder(canvas, "Нет данных для отображения");
                return null;
            }

            var points = CollectPoints(measurements);
            if (points.Count < 2)
            {
                DrawPlaceholder(canvas, "Недостаточно высот для построения профиля");
                return null;
            }

            var minHeight = minHeightOverride ?? points.Min(p => p.Height);
            var maxHeight = maxHeightOverride ?? points.Max(p => p.Height);

            if (!maxHeightOverride.HasValue && !minHeightOverride.HasValue && Math.Abs(maxHeight - minHeight) < 1e-6)
            {
                maxHeight = minHeight + 1;
            }

            if (maxHeight < minHeight)
            {
                (minHeight, maxHeight) = (maxHeight, minHeight);
            }

            var heightRange = Math.Max(maxHeight - minHeight, 1e-3);
            var totalDistance = Math.Max(points.Max(p => p.Distance), 1.0);

            var plotWidth = Math.Max(canvas.ActualWidth - 2 * Margin, 10);
            var plotHeight = Math.Max(canvas.ActualHeight - 2 * Margin, 10);

            DrawGrid(canvas, plotWidth, plotHeight, minHeight, maxHeight, totalDistance);
            DrawPolyline(canvas, points, minHeight, heightRange, plotWidth, plotHeight, totalDistance);
            var visuals = DrawPoints(canvas, points, minHeight, heightRange, plotWidth, plotHeight, totalDistance);

            var transform = new ProfileTransform(minHeight, maxHeight, totalDistance, plotWidth, plotHeight, Margin);
            return new ProfileRenderResult(visuals, transform);
        }

        private List<ProfilePoint> CollectPoints(IEnumerable<GeneratedMeasurement> measurements)
        {
            var points = new List<ProfilePoint>();
            double cumulative = 0;

            // Каждый круг на профиле соответствует измерению из таблицы генератора.
            // Положение точки определяется накопленной суммой HD_Back + HD_Fore,
            // поэтому изменение этих расстояний в измерении передвигает точку вдоль оси X.
            foreach (var m in measurements)
            {
                if (!m.Height_m.HasValue)
                    continue;

                var stationLength = (m.HD_Back_m ?? 0) + (m.HD_Fore_m ?? 0);
                if (stationLength <= 0)
                {
                    stationLength = 1.0; // Фолбэк, чтобы расстояние росло даже без HD
                }

                cumulative += stationLength;
                points.Add(new ProfilePoint(cumulative, m.Height_m.Value, m.PointCode, m));
            }

            return points;
        }

        private void DrawGrid(Canvas canvas, double plotWidth, double plotHeight, double minHeight, double maxHeight, double totalDistance)
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

            int verticalLines = 8;
            for (int i = 0; i <= verticalLines; i++)
            {
                var x = Margin + (i / (double)verticalLines) * plotWidth;
                var line = new Line
                {
                    X1 = x,
                    Y1 = Margin,
                    X2 = x,
                    Y2 = Margin + plotHeight,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                canvas.Children.Add(line);

                var distance = (i / (double)verticalLines) * totalDistance;
                var label = new TextBlock
                {
                    Text = $"{distance:F0} м",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70))
                };
                Canvas.SetLeft(label, x - 12);
                Canvas.SetTop(label, Margin + plotHeight + 6);
                canvas.Children.Add(label);
            }

            int horizontalLines = 6;
            for (int i = 0; i <= horizontalLines; i++)
            {
                var y = Margin + (i / (double)horizontalLines) * plotHeight;
                var line = new Line
                {
                    X1 = Margin,
                    Y1 = y,
                    X2 = Margin + plotWidth,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                canvas.Children.Add(line);

                var height = maxHeight - (i / (double)horizontalLines) * (maxHeight - minHeight);
                var label = new TextBlock
                {
                    Text = $"{height:F2}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70))
                };
                Canvas.SetLeft(label, 8);
                Canvas.SetTop(label, y - 8);
                canvas.Children.Add(label);
            }

            var axisLine = new Line
            {
                X1 = Margin,
                Y1 = Margin + plotHeight,
                X2 = Margin + plotWidth,
                Y2 = Margin + plotHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = 1.5
            };
            canvas.Children.Add(axisLine);
        }

        private void DrawPolyline(Canvas canvas, List<ProfilePoint> points, double minHeight, double heightRange, double plotWidth, double plotHeight, double totalDistance)
        {
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            foreach (var point in points)
            {
                var x = Margin + (point.Distance / totalDistance) * plotWidth;
                var y = Margin + plotHeight - ((point.Height - minHeight) / heightRange) * plotHeight;
                polyline.Points.Add(new System.Windows.Point(x, y));
            }

            canvas.Children.Add(polyline);
        }

        private List<ProfilePointVisual> DrawPoints(
            Canvas canvas,
            List<ProfilePoint> points,
            double minHeight,
            double heightRange,
            double plotWidth,
            double plotHeight,
            double totalDistance)
        {
            var pointFill = new SolidColorBrush(Color.FromRgb(0xFF, 0xB5, 0x00));
            var pointStroke = new SolidColorBrush(Color.FromRgb(0x80, 0x5A, 0x00));

            var visuals = new List<ProfilePointVisual>();

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var x = Margin + (point.Distance / totalDistance) * plotWidth;
                var y = Margin + plotHeight - ((point.Height - minHeight) / heightRange) * plotHeight;

                var ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = pointFill,
                    Stroke = pointStroke,
                    StrokeThickness = 1.2,
                    Cursor = Cursors.Hand,
                    ToolTip = $"{point.Label}: {point.Height:F3} м"
                };

                Canvas.SetLeft(ellipse, x - ellipse.Width / 2);
                Canvas.SetTop(ellipse, y - ellipse.Height / 2);
                canvas.Children.Add(ellipse);

                var label = new TextBlock
                {
                    Text = point.Label,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
                };

                Canvas.SetLeft(label, x + 8);
                Canvas.SetTop(label, y - 10);
                canvas.Children.Add(label);

                visuals.Add(new ProfilePointVisual(i, point, ellipse, label));
            }

            return visuals;
        }

        private void DrawPlaceholder(Canvas canvas, string text)
        {
            var placeholder = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                FontStyle = System.Windows.FontStyles.Italic
            };

            Canvas.SetLeft(placeholder, (canvas.ActualWidth - placeholder.ActualWidth) / 2);
            Canvas.SetTop(placeholder, (canvas.ActualHeight - placeholder.ActualHeight) / 2);
            canvas.Children.Add(placeholder);
        }
    }

    public class ProfilePoint
    {
        public ProfilePoint(double distance, double height, string label, GeneratedMeasurement measurement)
        {
            Distance = distance;
            Height = height;
            Label = label;
            Measurement = measurement;
        }

        public double Distance { get; set; }
        public double Height { get; set; }
        public string Label { get; }
        public GeneratedMeasurement Measurement { get; }
    }

    public class ProfilePointVisual
    {
        public ProfilePointVisual(int index, ProfilePoint point, Ellipse ellipse, TextBlock label)
        {
            Index = index;
            Point = point;
            Ellipse = ellipse;
            Label = label;
        }

        public int Index { get; }
        public ProfilePoint Point { get; }
        public Ellipse Ellipse { get; }
        public TextBlock Label { get; }
    }

    public class ProfileTransform
    {
        private readonly double _margin;

        public ProfileTransform(double minHeight, double maxHeight, double totalDistance, double plotWidth, double plotHeight, double margin)
        {
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            TotalDistance = totalDistance;
            PlotWidth = plotWidth;
            PlotHeight = plotHeight;
            _margin = margin;
        }

        public double MinHeight { get; }
        public double MaxHeight { get; }
        public double TotalDistance { get; }
        public double PlotWidth { get; }
        public double PlotHeight { get; }

        public double CanvasToDistance(double x)
        {
            var normalized = Math.Clamp(x - _margin, 0, PlotWidth) / PlotWidth;
            return normalized * TotalDistance;
        }

        public double CanvasToHeight(double y)
        {
            var clampedY = Math.Clamp(y - _margin, 0, PlotHeight);
            var normalized = 1 - clampedY / PlotHeight;
            return MinHeight + normalized * (MaxHeight - MinHeight);
        }

        public double DistanceToCanvasX(double distance)
        {
            var clamped = Math.Clamp(distance, 0, TotalDistance);
            return _margin + (clamped / Math.Max(TotalDistance, 1e-6)) * PlotWidth;
        }

        public double HeightToCanvasY(double height)
        {
            var range = Math.Max(MaxHeight - MinHeight, 1e-3);
            var clampedHeight = Math.Clamp(height, MinHeight, MaxHeight);
            var normalized = 1 - (clampedHeight - MinHeight) / range;
            return _margin + normalized * PlotHeight;
        }
    }

    public class ProfileRenderResult
    {
        public ProfileRenderResult(IReadOnlyList<ProfilePointVisual> points, ProfileTransform transform)
        {
            Points = points;
            Transform = transform;
        }

        public IReadOnlyList<ProfilePointVisual> Points { get; }
        public ProfileTransform Transform { get; }
    }
}
