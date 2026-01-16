using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Constants;
using Nivtropy.Domain.DTOs;
using Nivtropy.Application.DTOs;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Services.Visualization
{
    /// <summary>
    /// Сервис для визуализации профилей нивелирного хода
    /// Извлечен из code-behind для соблюдения SOLID принципов
    /// </summary>
    public class ProfileVisualizationService : IProfileVisualizationService
    {
        private const double Margin = VisualizationDefaults.Margin;

        public void DrawProfile(
            Canvas canvas,
            List<TraverseRow> rows,
            ProfileVisualizationOptions options,
            ProfileStatistics? statistics,
            HashSet<string> knownHeightPoints)
        {
            canvas.Children.Clear();

            if (rows.Count < 2)
                return;

            var canvasWidth = canvas.ActualWidth;
            var canvasHeight = canvas.ActualHeight;

            if (canvasWidth < 10 || canvasHeight < 10)
                return;

            // Собираем точки для отображения
            var (pointsZ, pointsZ0, pointDistances) = CollectPoints(rows, options);

            if (pointsZ.Count < 2 && pointsZ0.Count < 2)
                return;

            // Определяем диапазон высот
            var (minHeight, maxHeight, heightRange) = CalculateHeightRange(pointsZ, pointsZ0, options);

            var totalDistance = pointDistances.LastOrDefault();
            if (totalDistance < 0.001)
                totalDistance = 1.0;

            var plotWidth = canvasWidth - 2 * Margin;
            var plotHeight = canvasHeight - 2 * Margin;

            // Рисуем элементы по порядку
            DrawGrid(canvas, canvasWidth, canvasHeight, plotWidth, plotHeight, minHeight, maxHeight, totalDistance);
            DrawArmDifferenceIndicators(canvas, rows, pointDistances, plotWidth, plotHeight, totalDistance, Margin);
            DrawProfileLine(canvas, pointsZ0, canvasWidth, canvasHeight, plotWidth, plotHeight, minHeight, heightRange, totalDistance, options.ProfileZ0Color, isDashed: true, options.ShowZ0);
            DrawProfileLine(canvas, pointsZ, canvasWidth, canvasHeight, plotWidth, plotHeight, minHeight, heightRange, totalDistance, options.ProfileColor, isDashed: false, options.ShowZ);
            DrawPoints(canvas, pointsZ, pointsZ0, pointDistances, minHeight, heightRange, plotWidth, plotHeight, canvasWidth, canvasHeight, totalDistance, knownHeightPoints, options);
            DrawAnomalies(canvas, rows, statistics, pointDistances, minHeight, heightRange, plotWidth, plotHeight, canvasWidth, canvasHeight, totalDistance, options.ShowAnomalies);
        }

        private (List<(double height, double distance, string pointCode, int index)> pointsZ,
                 List<(double height, double distance, string pointCode, int index)> pointsZ0,
                 List<double> pointDistances) CollectPoints(List<TraverseRow> rows, ProfileVisualizationOptions options)
        {
            var pointsZ = new List<(double height, double distance, string pointCode, int index)>();
            var pointsZ0 = new List<(double height, double distance, string pointCode, int index)>();
            var pointDistances = new List<double>(rows.Count);
            double cumulativeDistance = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var stationLength = row.StationLength_m ?? 0;
                var heightZ = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                var heightZ0 = row.IsVirtualStation ? row.BackHeightZ0 : row.ForeHeightZ0;
                var pointCode = row.PointCode ?? "";

                if (!row.IsVirtualStation)
                {
                    cumulativeDistance += stationLength;
                }

                pointDistances.Add(cumulativeDistance);

                if (heightZ.HasValue && options.ShowZ)
                {
                    pointsZ.Add((heightZ.Value, cumulativeDistance, pointCode, i + 1));
                }

                if (heightZ0.HasValue && options.ShowZ0)
                {
                    pointsZ0.Add((heightZ0.Value, cumulativeDistance, pointCode, i + 1));
                }
            }

            return (pointsZ, pointsZ0, pointDistances);
        }

        private (double minHeight, double maxHeight, double heightRange) CalculateHeightRange(
            List<(double height, double distance, string pointCode, int index)> pointsZ,
            List<(double height, double distance, string pointCode, int index)> pointsZ0,
            ProfileVisualizationOptions options)
        {
            var allHeights = new List<double>();
            if (pointsZ.Any())
                allHeights.AddRange(pointsZ.Select(p => p.height));
            if (pointsZ0.Any())
                allHeights.AddRange(pointsZ0.Select(p => p.height));

            var minHeight = options.ManualMinHeight ?? allHeights.Min();
            var maxHeight = options.ManualMaxHeight ?? allHeights.Max();
            var heightRange = maxHeight - minHeight;
            if (heightRange < 0.001)
                heightRange = 1.0;

            return (minHeight, maxHeight, heightRange);
        }

        private void DrawGrid(Canvas canvas, double canvasWidth, double canvasHeight, double plotWidth, double plotHeight,
                             double minHeight, double maxHeight, double totalDistance)
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

            // Вертикальные линии
            int verticalLines = VisualizationDefaults.VerticalGridLines;
            for (int i = 0; i <= verticalLines; i++)
            {
                var x = Margin + (i / (double)verticalLines) * plotWidth;
                var line = new Line
                {
                    X1 = x,
                    Y1 = Margin,
                    X2 = x,
                    Y2 = canvasHeight - Margin,
                    Stroke = gridBrush,
                    StrokeThickness = VisualizationDefaults.GridStrokeThickness
                };
                canvas.Children.Add(line);

                if (i % 2 == 0)
                {
                    var distance = (i / (double)verticalLines) * totalDistance;
                    var label = new TextBlock
                    {
                        Text = $"{distance:F0}",
                        FontSize = VisualizationDefaults.GridFontSize,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
                    };
                    Canvas.SetLeft(label, x - 10);
                    Canvas.SetTop(label, canvasHeight - Margin + 5);
                    canvas.Children.Add(label);
                }
            }

            // Горизонтальные линии
            int horizontalLines = VisualizationDefaults.HorizontalGridLines;
            for (int i = 0; i <= horizontalLines; i++)
            {
                var y = Margin + (i / (double)horizontalLines) * plotHeight;
                var line = new Line
                {
                    X1 = Margin,
                    Y1 = y,
                    X2 = canvasWidth - Margin,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = VisualizationDefaults.GridStrokeThickness
                };
                canvas.Children.Add(line);

                if (i % 2 == 0)
                {
                    var height = maxHeight - (i / (double)horizontalLines) * (maxHeight - minHeight);
                    var label = new TextBlock
                    {
                        Text = $"{height:F2}",
                        FontSize = VisualizationDefaults.GridFontSize,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
                    };
                    Canvas.SetRight(label, canvasWidth - Margin + 5);
                    Canvas.SetTop(label, y - 7);
                    canvas.Children.Add(label);
                }
            }

            // Оси
            var axisLine = new Line
            {
                X1 = Margin,
                Y1 = canvasHeight - Margin,
                X2 = canvasWidth - Margin,
                Y2 = canvasHeight - Margin,
                Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = 1.5
            };
            canvas.Children.Add(axisLine);

            var leftAxisLine = new Line
            {
                X1 = Margin,
                Y1 = Margin,
                X2 = Margin,
                Y2 = canvasHeight - Margin,
                Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = 1.5
            };
            canvas.Children.Add(leftAxisLine);

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
            canvas.Children.Add(xAxisLabel);

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
            canvas.Children.Add(yAxisLabel);
        }

        private void DrawArmDifferenceIndicators(Canvas canvas, List<TraverseRow> rows, List<double> pointDistances,
                                                 double plotWidth, double plotHeight, double totalDistance, double margin)
        {
            for (int i = 0; i < rows.Count - 1; i++)
            {
                var row = rows[i];
                if (row.IsVirtualStation || row.StationLength_m == null)
                    continue;

                var armDiff = Math.Abs(row.ArmDifference_m ?? 0);
                var stationLength = row.StationLength_m ?? 0;

                // Определяем цвет индикатора
                Color segmentColor;
                if (row.IsArmDifferenceExceeded)
                {
                    segmentColor = Color.FromRgb(255, 69, 58); // Красный
                }
                else if (armDiff > VisualizationDefaults.ArmDiffErrorThreshold)
                {
                    segmentColor = Color.FromRgb(255, 159, 10); // Оранжевый
                }
                else if (armDiff > VisualizationDefaults.ArmDiffWarningThreshold)
                {
                    segmentColor = Color.FromRgb(255, 204, 0); // Жёлтый
                }
                else
                {
                    segmentColor = Color.FromRgb(52, 199, 89); // Зелёный
                }

                var x1 = margin + (pointDistances[i] / totalDistance) * plotWidth;
                var x2 = i + 1 < pointDistances.Count
                    ? margin + (pointDistances[i + 1] / totalDistance) * plotWidth
                    : x1;

                var rect = new Rectangle
                {
                    Width = Math.Max(x2 - x1, 2),
                    Height = plotHeight,
                    Fill = new SolidColorBrush(Color.FromArgb(30, segmentColor.R, segmentColor.G, segmentColor.B)),
                    ToolTip = $"Ст. {row.Index}: разность плеч = {armDiff:F3} м"
                };
                Canvas.SetLeft(rect, x1);
                Canvas.SetTop(rect, margin);
                canvas.Children.Add(rect);
            }
        }

        private void DrawProfileLine(Canvas canvas, List<(double height, double distance, string pointCode, int index)> points,
                                    double canvasWidth, double canvasHeight, double plotWidth, double plotHeight,
                                    double minHeight, double heightRange, double totalDistance, Color color, bool isDashed, bool show)
        {
            if (!show || points.Count < 2)
                return;

            var brush = new SolidColorBrush(color);

            for (int i = 0; i < points.Count - 1; i++)
            {
                var x1 = Margin + (points[i].distance / totalDistance) * plotWidth;
                var y1 = canvasHeight - Margin - ((points[i].height - minHeight) / heightRange * plotHeight);
                var x2 = Margin + (points[i + 1].distance / totalDistance) * plotWidth;
                var y2 = canvasHeight - Margin - ((points[i + 1].height - minHeight) / heightRange * plotHeight);

                if (isDashed)
                {
                    // Пунктирная линия для Z0
                    var line = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = brush,
                        StrokeThickness = 2.0,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    canvas.Children.Add(line);
                }
                else
                {
                    // Сплошная линия с обводкой для Z
                    var outlineLine = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = Brushes.Black,
                        StrokeThickness = 3.5
                    };
                    canvas.Children.Add(outlineLine);

                    var whiteLine = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = Brushes.White,
                        StrokeThickness = 2.0
                    };
                    canvas.Children.Add(whiteLine);
                }
            }
        }

        private void DrawPoints(Canvas canvas,
                               List<(double height, double distance, string pointCode, int index)> pointsZ,
                               List<(double height, double distance, string pointCode, int index)> pointsZ0,
                               List<double> pointDistances,
                               double minHeight, double heightRange, double plotWidth, double plotHeight,
                               double canvasWidth, double canvasHeight, double totalDistance,
                               HashSet<string> knownHeightPoints, ProfileVisualizationOptions options)
        {
            var displayPoints = options.ShowZ && pointsZ.Any() ? pointsZ : pointsZ0;
            if (!displayPoints.Any())
                return;

            var allPointCodes = displayPoints.Select(p => p.pointCode).ToList();
            var sharedPoints = allPointCodes.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            var profileBrush = new SolidColorBrush(options.ShowZ ? options.ProfileColor : options.ProfileZ0Color);

            foreach (var point in displayPoints)
            {
                var x = Margin + (point.distance / totalDistance) * plotWidth;
                var y = canvasHeight - Margin - ((point.height - minHeight) / heightRange * plotHeight);

                DrawPoint(canvas, x, y, point.pointCode, point.index, point.height,
                         sharedPoints.Contains(point.pointCode),
                         knownHeightPoints.Contains(point.pointCode),
                         profileBrush);
            }
        }

        private void DrawPoint(Canvas canvas, double x, double y, string pointCode, int index, double height,
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
                canvas.Children.Add(outerCircle);

                var cross1 = new Line { X1 = x - 3, Y1 = y - 3, X2 = x + 3, Y2 = y + 3, Stroke = brush, StrokeThickness = 1.5 };
                canvas.Children.Add(cross1);

                var cross2 = new Line { X1 = x - 3, Y1 = y + 3, X2 = x + 3, Y2 = y - 3, Stroke = brush, StrokeThickness = 1.5 };
                canvas.Children.Add(cross2);

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
                canvas.Children.Add(outerCircle);
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
                canvas.Children.Add(circle);

                var cross1 = new Line { X1 = x - 2.5, Y1 = y - 2.5, X2 = x + 2.5, Y2 = y + 2.5, Stroke = Brushes.White, StrokeThickness = 1.2 };
                canvas.Children.Add(cross1);

                var cross2 = new Line { X1 = x - 2.5, Y1 = y + 2.5, X2 = x + 2.5, Y2 = y - 2.5, Stroke = Brushes.White, StrokeThickness = 1.2 };
                canvas.Children.Add(cross2);
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
                canvas.Children.Add(ellipse);
            }
        }

        private void DrawAnomalies(Canvas canvas, List<TraverseRow> rows, ProfileStatistics? statistics,
                                  List<double> pointDistances, double minHeight, double heightRange,
                                  double plotWidth, double plotHeight, double canvasWidth, double canvasHeight,
                                  double totalDistance, bool showAnomalies)
        {
            if (!showAnomalies || statistics == null || !statistics.HasOutliers)
                return;

            var outlierStations = statistics.Outliers.Select(o => o.StationIndex).ToHashSet();

            for (int i = 0; i < rows.Count; i++)
            {
                if (outlierStations.Contains(rows[i].Index))
                {
                    var height = rows[i].IsVirtualStation ? rows[i].BackHeight : rows[i].ForeHeight;
                    if (height.HasValue && i < pointDistances.Count)
                    {
                        var x = Margin + (pointDistances[i] / totalDistance) * plotWidth;
                        var y = canvasHeight - Margin - ((height.Value - minHeight) / heightRange * plotHeight);

                        var stationOutliers = statistics.Outliers.Where(o => o.StationIndex == rows[i].Index).ToList();
                        var maxSeverity = stationOutliers.Max(o => o.Severity);
                        var outlierDescriptions = string.Join("\n", stationOutliers.Select(o => o.Description));

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
                        canvas.Children.Add(outerCircle);
                    }
                }
            }
        }
    }
}
