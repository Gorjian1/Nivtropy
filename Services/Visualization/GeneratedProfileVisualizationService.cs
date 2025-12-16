using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Models;

namespace Nivtropy.Services.Visualization
{
    /// <summary>
    /// Визуализация профиля сгенерированных данных в окне "Проектирование".
    /// </summary>
    public class GeneratedProfileVisualizationService
    {
        private const double Margin = 48;

        public void DrawProfile(Canvas canvas, IReadOnlyCollection<GeneratedMeasurement> measurements)
        {
            canvas.Children.Clear();

            if (measurements.Count == 0 || canvas.ActualWidth < 10 || canvas.ActualHeight < 10)
            {
                DrawPlaceholder(canvas, "Нет данных для отображения");
                return;
            }

            var points = CollectPoints(measurements);
            if (points.Count < 2)
            {
                DrawPlaceholder(canvas, "Недостаточно высот для построения профиля");
                return;
            }

            var minHeight = points.Min(p => p.height);
            var maxHeight = points.Max(p => p.height);
            var heightRange = Math.Max(maxHeight - minHeight, 1e-3);
            var totalDistance = Math.Max(points.Max(p => p.distance), 1.0);

            var plotWidth = Math.Max(canvas.ActualWidth - 2 * Margin, 10);
            var plotHeight = Math.Max(canvas.ActualHeight - 2 * Margin, 10);

            DrawGrid(canvas, plotWidth, plotHeight, minHeight, maxHeight, totalDistance);
            DrawPolyline(canvas, points, minHeight, heightRange, plotWidth, plotHeight, totalDistance);
            DrawPoints(canvas, points, minHeight, heightRange, plotWidth, plotHeight, totalDistance);
        }

        private List<(double distance, double height, string label)> CollectPoints(IEnumerable<GeneratedMeasurement> measurements)
        {
            var points = new List<(double distance, double height, string label)>();
            double cumulative = 0;

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
                points.Add((cumulative, m.Height_m.Value, m.PointCode));
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

        private void DrawPolyline(Canvas canvas, List<(double distance, double height, string label)> points, double minHeight, double heightRange, double plotWidth, double plotHeight, double totalDistance)
        {
            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            foreach (var point in points)
            {
                var x = Margin + (point.distance / totalDistance) * plotWidth;
                var y = Margin + plotHeight - ((point.height - minHeight) / heightRange) * plotHeight;
                polyline.Points.Add(new System.Windows.Point(x, y));
            }

            canvas.Children.Add(polyline);
        }

        private void DrawPoints(Canvas canvas, List<(double distance, double height, string label)> points, double minHeight, double heightRange, double plotWidth, double plotHeight, double totalDistance)
        {
            var pointFill = new SolidColorBrush(Color.FromRgb(0xFF, 0xB5, 0x00));
            var pointStroke = new SolidColorBrush(Color.FromRgb(0x80, 0x5A, 0x00));

            foreach (var point in points)
            {
                var x = Margin + (point.distance / totalDistance) * plotWidth;
                var y = Margin + plotHeight - ((point.height - minHeight) / heightRange) * plotHeight;

                var ellipse = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = pointFill,
                    Stroke = pointStroke,
                    StrokeThickness = 1,
                    ToolTip = $"{point.label}: {point.height:F3} м"
                };

                Canvas.SetLeft(ellipse, x - 4);
                Canvas.SetTop(ellipse, y - 4);
                canvas.Children.Add(ellipse);

                var label = new TextBlock
                {
                    Text = point.label,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
                };

                Canvas.SetLeft(label, x + 6);
                Canvas.SetTop(label, y - 10);
                canvas.Children.Add(label);
            }
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
}
