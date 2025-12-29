using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Models;
using Nivtropy.ViewModels;

namespace Nivtropy.Services.Visualization
{
    /// <summary>
    /// Сервис для визуализации графа связей между ходами нивелирной сети
    /// Извлечен из code-behind для соблюдения принципа единственной ответственности (SRP)
    /// </summary>
    public class TraverseSystemVisualizationService : ITraverseSystemVisualizationService
    {
        private const double Padding = 18;

        public void DrawSystemVisualization(Canvas canvas, TraverseJournalViewModel viewModel)
        {
            canvas.Children.Clear();

            var calculation = viewModel.Calculation;
            var runs = calculation.Runs.ToList();

            if (runs.Count == 0)
                return;

            var canvasWidth = canvas.ActualWidth;
            var canvasHeight = canvas.ActualHeight;

            if (canvasWidth < 10 || canvasHeight < 10)
                return;

            try
            {
                DrawSystemVisualizationInternal(canvas, calculation, runs, canvasWidth, canvasHeight);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Traverse visualization failed: {ex}");

                var errorBlock = new TextBlock
                {
                    Text = "Не удалось отрисовать схему ходов. Проверьте корректность данных.",
                    Foreground = Brushes.DarkRed,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8)
                };

                canvas.Children.Add(errorBlock);
            }
        }

        private void DrawSystemVisualizationInternal(Canvas canvas, TraverseCalculationViewModel calculation,
                                                     List<LineSummary> runs, double canvasWidth, double canvasHeight)
        {
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
            var pointsByRun = CollectPointSequences(runs, rows);

            // Вычисляем позиции и радиусы для ходов
            var runShapeRadius = CalculateRunRadii(runs, pointsByRun);
            var runRotationOffsets = CalculateRotationOffsets(runs, pointsByRun);
            var maxShapeRadius = runShapeRadius.Values.Count > 0 ? runShapeRadius.Values.Max() : 30;

            // Позиции общих точек
            var sharedPoints = calculation.SharedPoints.Where(p => p.IsEnabled).ToList();
            var sharedPointPositions = CalculateSharedPointPositions(sharedPoints, canvasWidth, canvasHeight, maxShapeRadius);

            // Позиции центров ходов
            var runCenters = CalculateRunCenters(runs, calculation, sharedPointPositions, runShapeRadius, canvasWidth, canvasHeight, maxShapeRadius);

            // Итерационное разрешение коллизий между фигурами
            var actualRunRadius = ResolveCollisions(runs, runCenters, runShapeRadius, canvasWidth, canvasHeight);

            // Рисуем каждый ход
            var drawnSharedPoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DrawRuns(canvas, runs, runColors, pointsByRun, runCenters, actualRunRadius, runRotationOffsets,
                    sharedPointPositions, calculation, drawnSharedPoints);
        }

        private Dictionary<LineSummary, List<string>> CollectPointSequences(List<LineSummary> runs, List<TraverseRow> rows)
        {
            var pointsByRun = new Dictionary<LineSummary, List<string>>();

            foreach (var run in runs)
            {
                // Сопоставляем ряды по индексу LineSummary
                var runRows = rows
                    .Where(r => r.LineSummary != null && r.LineSummary.Index == run.Index)
                    .OrderBy(r => r.Index)
                    .ToList();

                var sequence = new List<string>();

                if (!string.IsNullOrWhiteSpace(run.StartLabel))
                {
                    sequence.Add(run.StartLabel.Trim());
                }

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

                // Убираем подряд идущие дубликаты
                var uniqueSequence = new List<string>();
                string? lastCode = null;
                foreach (var code in sequence)
                {
                    if (!string.Equals(code, lastCode, StringComparison.OrdinalIgnoreCase))
                    {
                        uniqueSequence.Add(code);
                        lastCode = code;
                    }
                }

                // Fallback: если точек меньше 2, используем StartLabel и EndLabel из хода
                if (uniqueSequence.Count < 2)
                {
                    uniqueSequence.Clear();
                    if (!string.IsNullOrWhiteSpace(run.StartLabel))
                        uniqueSequence.Add(run.StartLabel.Trim());
                    if (!string.IsNullOrWhiteSpace(run.EndLabel) &&
                        !string.Equals(run.EndLabel, run.StartLabel, StringComparison.OrdinalIgnoreCase))
                        uniqueSequence.Add(run.EndLabel.Trim());
                }

                pointsByRun[run] = uniqueSequence;
            }

            return pointsByRun;
        }

        private Dictionary<LineSummary, double> CalculateRunRadii(List<LineSummary> runs, Dictionary<LineSummary, List<string>> pointsByRun)
        {
            return runs.ToDictionary(
                run => run,
                run =>
                {
                    var pointCount = pointsByRun.TryGetValue(run, out var seq) ? seq.Count : 0;
                    var clamped = Math.Max(pointCount, 1);
                    var scaled = 18 + Math.Sqrt(clamped) * 9;
                    return Math.Clamp(scaled, 26, 138);
                });
        }

        private Dictionary<LineSummary, double> CalculateRotationOffsets(List<LineSummary> runs, Dictionary<LineSummary, List<string>> pointsByRun)
        {
            return runs.ToDictionary(
                run => run,  // Ключ - LineSummary
                run =>       // Значение - double (угол в радианах)
                {
                    var key = run.DisplayName ?? run.Index.ToString();
                    long hash = 0;

                    foreach (var ch in key)
                    {
                        hash = (hash * 31 + ch) & 0x7FFFFFFF;
                    }

                    var count = pointsByRun.TryGetValue(run, out var seq) ? seq.Count : 0;
                    hash = (hash + count * 97 + run.Index * 53) & 0x7FFFFFFF;

                    var degrees = 8 + (hash % 344);
                    return degrees * Math.PI / 180.0;
                });
        }

        private Dictionary<string, Point> CalculateSharedPointPositions(List<SharedPointLinkItem> sharedPoints,
                                                                        double canvasWidth, double canvasHeight,
                                                                        double maxShapeRadius)
        {
            var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            var center = new Point(canvasWidth / 2, canvasHeight / 2);
            var drawableRadius = Math.Min(canvasWidth, canvasHeight) / 2 - (Padding + maxShapeRadius);
            var sharedRadius = Math.Max(24, drawableRadius);

            for (int i = 0; i < sharedPoints.Count; i++)
            {
                var angle = 2 * Math.PI * i / Math.Max(1, sharedPoints.Count);
                var point = new Point(
                    center.X + sharedRadius * Math.Cos(angle),
                    center.Y + sharedRadius * Math.Sin(angle));
                positions[sharedPoints[i].Code] = ClampPoint(point, Padding + 6, canvasWidth, canvasHeight);
            }

            return positions;
        }

        private Dictionary<LineSummary, Point> CalculateRunCenters(List<LineSummary> runs,
                                                                   TraverseCalculationViewModel calculation,
                                                                   Dictionary<string, Point> sharedPointPositions,
                                                                   Dictionary<LineSummary, double> runShapeRadius,
                                                                   double canvasWidth, double canvasHeight,
                                                                   double maxShapeRadius)
        {
            var runCenters = new Dictionary<LineSummary, Point>();
            var center = new Point(canvasWidth / 2, canvasHeight / 2);
            var orbitBase = Math.Min(canvasWidth, canvasHeight) / 2 - (Padding + maxShapeRadius);
            var sharedRadius = sharedPointPositions.Any() ? Math.Max(24, orbitBase * 0.82) : Math.Max(24, orbitBase);

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

                    var jitterAngle = ((run.Index * 37) % 360) * Math.PI / 180.0;
                    var jitterDistance = (anchors.Count > 1 ? 10 : 18) + (i % 3) * 3;
                    var offset = new Vector(Math.Cos(jitterAngle) * jitterDistance, Math.Sin(jitterAngle) * jitterDistance);
                    var proposed = basePoint + offset;
                    var margin = Padding + shapeRadius;
                    runCenters[run] = ClampPoint(proposed, margin, canvasWidth, canvasHeight);
                }
                else
                {
                    var angle = 2 * Math.PI * i / Math.Max(1, runs.Count);
                    var radius = sharedRadius + (sharedPointPositions.Any() ? 24 : 0);
                    var proposed = new Point(
                        center.X + radius * Math.Cos(angle),
                        center.Y + radius * Math.Sin(angle));
                    var margin = Padding + shapeRadius;
                    runCenters[run] = ClampPoint(proposed, margin, canvasWidth, canvasHeight);
                }
            }

            return runCenters;
        }

        private Dictionary<LineSummary, double> ResolveCollisions(List<LineSummary> runs,
                                                                  Dictionary<LineSummary, Point> runCenters,
                                                                  Dictionary<LineSummary, double> runShapeRadius,
                                                                  double canvasWidth, double canvasHeight)
        {
            var actualRunRadius = runs.ToDictionary(run => run, _ => 0d);

            double GetActualRadius(LineSummary run)
            {
                var target = runShapeRadius[run];
                var available = GetMaxRadius(runCenters[run], Padding, canvasWidth, canvasHeight);
                return Math.Min(target, available);
            }

            // Итерационное разрешение коллизий
            for (int iteration = 0; iteration < 22; iteration++)
            {
                foreach (var run in runs)
                {
                    var radius = GetActualRadius(run);
                    actualRunRadius[run] = radius;
                    runCenters[run] = ClampPoint(runCenters[run], Padding + radius, canvasWidth, canvasHeight);
                }

                foreach (var a in runs)
                {
                    foreach (var b in runs)
                    {
                        if (a == b) continue;

                        var ca = runCenters[a];
                        var cb = runCenters[b];
                        var delta = cb - ca;
                        var distance = delta.Length;
                        var target = actualRunRadius[a] + actualRunRadius[b] + Padding * 0.6;

                        if (distance <= 0.01 || distance >= target)
                            continue;

                        delta.Normalize();
                        var push = (target - distance) / 2;
                        var shiftA = new Point(ca.X - delta.X * push, ca.Y - delta.Y * push);
                        var shiftB = new Point(cb.X + delta.X * push, cb.Y + delta.Y * push);

                        runCenters[a] = ClampPoint(shiftA, Padding + actualRunRadius[a], canvasWidth, canvasHeight);
                        runCenters[b] = ClampPoint(shiftB, Padding + actualRunRadius[b], canvasWidth, canvasHeight);
                    }
                }
            }

            return actualRunRadius;
        }

        private void DrawRuns(Canvas canvas, List<LineSummary> runs, Dictionary<LineSummary, Color> runColors,
                             Dictionary<LineSummary, List<string>> pointsByRun,
                             Dictionary<LineSummary, Point> runCenters,
                             Dictionary<LineSummary, double> actualRunRadius,
                             Dictionary<LineSummary, double> runRotationOffsets,
                             Dictionary<string, Point> sharedPointPositions,
                             TraverseCalculationViewModel calculation,
                             HashSet<string> drawnSharedPoints)
        {
            foreach (var run in runs)
            {
                pointsByRun.TryGetValue(run, out var pointSequence);
                pointSequence ??= new List<string>();

                var runColor = runColors.TryGetValue(run, out var c) ? c : Colors.SteelBlue;
                var strokeColor = run.IsActive ? runColor : Color.FromRgb(140, 140, 140);
                var fillColor = Color.FromArgb(30, runColor.R, runColor.G, runColor.B);

                var centerPoint = runCenters.TryGetValue(run, out var cp) ? cp : new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2);
                var shapeRadius = actualRunRadius.TryGetValue(run, out var radius) ? radius : 32;
                var rotationOffset = runRotationOffsets.TryGetValue(run, out var offset) ? offset : 0.0;

                // Если точек меньше 2, рисуем круг с единственной точкой (или пустой круг)
                if (pointSequence.Count < 2)
                {
                    DrawSinglePointRun(canvas, run, pointSequence, centerPoint, shapeRadius, strokeColor, fillColor, calculation, drawnSharedPoints, runColor);
                    continue;
                }

                var pointCount = pointSequence.Count;

                // Все точки размещаются последовательно по кругу вокруг центра хода
                // Общие точки визуально выделяются, но не используются как фиксированные якоря
                // Это предотвращает "перекруты" при неправильном порядке общих точек
                var vertices = new List<Point>();

                for (int i = 0; i < pointSequence.Count; i++)
                {
                    var angle = rotationOffset + 2 * Math.PI * i / pointCount;
                    var vertex = new Point(
                        centerPoint.X + shapeRadius * Math.Cos(angle),
                        centerPoint.Y + shapeRadius * Math.Sin(angle));
                    vertices.Add(ClampPoint(vertex, Padding, canvas.ActualWidth, canvas.ActualHeight));
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

                canvas.Children.Add(path);

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
                canvas.Children.Add(label);

                // Отрисовка точек фигуры
                DrawRunPoints(canvas, run, pointSequence, vertices, centerPoint, calculation, drawnSharedPoints, runColor);
            }
        }

        private void DrawSinglePointRun(Canvas canvas, LineSummary run, List<string> pointSequence,
                                        Point centerPoint, double shapeRadius, Color strokeColor, Color fillColor,
                                        TraverseCalculationViewModel calculation,
                                        HashSet<string> drawnSharedPoints, Color runColor)
        {
            // Рисуем простой круг для хода с 0-1 точками
            var ellipse = new Ellipse
            {
                Width = shapeRadius * 2,
                Height = shapeRadius * 2,
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = run.IsActive ? 2.2 : 1.5,
                Fill = new SolidColorBrush(fillColor),
                StrokeDashArray = run.IsActive ? null : new DoubleCollection { 3, 3 },
                ToolTip = run.Tooltip
            };
            Canvas.SetLeft(ellipse, centerPoint.X - shapeRadius);
            Canvas.SetTop(ellipse, centerPoint.Y - shapeRadius);
            canvas.Children.Add(ellipse);

            // Подпись хода
            var label = new TextBlock
            {
                Text = run.DisplayName,
                FontSize = 10,
                FontWeight = run.IsActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(strokeColor),
                Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
            };
            Canvas.SetLeft(label, centerPoint.X - 18);
            Canvas.SetTop(label, centerPoint.Y - 10);
            canvas.Children.Add(label);

            // Если есть хотя бы одна точка, рисуем её
            if (pointSequence.Count == 1)
            {
                var code = pointSequence[0];
                var pointPos = new Point(centerPoint.X + shapeRadius, centerPoint.Y);
                pointPos = ClampPoint(pointPos, Padding, canvas.ActualWidth, canvas.ActualHeight);

                var sharedForRun = new HashSet<string>(calculation.GetSharedPointsForRun(run)
                    .Where(p => p.IsEnabled)
                    .Select(p => p.Code), StringComparer.OrdinalIgnoreCase);

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
                        ToolTip = $"{code}\n{(hasKnownHeight ? "Известная" : isShared ? "Общая" : "Точка хода")}"
                    };

                    Canvas.SetLeft(node, pointPos.X - node.Width / 2);
                    Canvas.SetTop(node, pointPos.Y - node.Height / 2);
                    canvas.Children.Add(node);

                    var pointLabel = new TextBlock
                    {
                        Text = code,
                        FontSize = 8,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.Black,
                        Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
                    };
                    Canvas.SetLeft(pointLabel, pointPos.X + 8);
                    Canvas.SetTop(pointLabel, pointPos.Y - 8);
                    canvas.Children.Add(pointLabel);
                }
            }
        }

        private void DrawRunPoints(Canvas canvas, LineSummary run, List<string> pointSequence,
                                  List<Point> vertices, Point centerPoint,
                                  TraverseCalculationViewModel calculation,
                                  HashSet<string> drawnSharedPoints, Color runColor)
        {
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
                        ToolTip = $"{code}\n{(hasKnownHeight ? "Известная" : isShared ? "Общая" : "Точка хода")}"
                    };

                    Canvas.SetLeft(node, pointPos.X - node.Width / 2);
                    Canvas.SetTop(node, pointPos.Y - node.Height / 2);
                    canvas.Children.Add(node);

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
                    canvas.Children.Add(pointLabel);
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

        private static double NormalizeAngle(double angle)
        {
            var twoPi = 2 * Math.PI;
            angle %= twoPi;
            if (angle < 0)
                angle += twoPi;
            return angle;
        }

        private Point ClampPoint(Point p, double margin, double canvasWidth, double canvasHeight)
        {
            double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
            return new Point(
                Clamp(p.X, margin, canvasWidth - margin),
                Clamp(p.Y, margin, canvasHeight - margin));
        }

        private double GetMaxRadius(Point c, double margin, double canvasWidth, double canvasHeight)
        {
            var dx = Math.Min(c.X - margin, canvasWidth - margin - c.X);
            var dy = Math.Min(c.Y - margin, canvasHeight - margin - c.Y);
            return Math.Max(0, Math.Min(dx, dy));
        }
    }
}
