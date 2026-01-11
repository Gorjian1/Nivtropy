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
    /// Сервис визуализации нивелирной сети как графа.
    /// Узлы: реперы (▲) и связующие точки (●)
    /// Рёбра: разомкнутые ходы (линии) и замкнутые ходы (петли)
    /// </summary>
    public class TraverseSystemVisualizationService : ITraverseSystemVisualizationService
    {
        private const double Padding = 20;

        private static readonly Color[] Colors =
        {
            Color.FromRgb(25, 118, 210),  // Синий
            Color.FromRgb(56, 142, 60),   // Зелёный
            Color.FromRgb(251, 140, 0),   // Оранжевый
            Color.FromRgb(142, 36, 170),  // Фиолетовый
            Color.FromRgb(0, 150, 136),   // Бирюзовый
            Color.FromRgb(211, 47, 47)    // Красный
        };

        public void DrawSystemVisualization(Canvas canvas, TraverseJournalViewModel viewModel)
        {
            canvas.Children.Clear();

            var runs = viewModel.Calculation.Runs.ToList();
            if (runs.Count == 0) return;

            var w = canvas.ActualWidth;
            var h = canvas.ActualHeight;
            if (w < 10 || h < 10) return;

            try
            {
                Draw(canvas, viewModel.Calculation, runs, w, h);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Visualization error: {ex.Message}");
                canvas.Children.Add(new TextBlock
                {
                    Text = "Ошибка отрисовки схемы",
                    Foreground = Brushes.DarkRed,
                    Margin = new Thickness(8)
                });
            }
        }

        private void Draw(Canvas canvas, TraverseCalculationViewModel calc, List<LineSummary> runs, double w, double h)
        {
            var pointsByRun = GetPointSequences(runs, calc.Rows.ToList());

            // Собираем важные точки
            var knownHeights = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shared = new HashSet<string>(
                calc.SharedPoints.Where(p => p.IsEnabled).Select(p => p.Code),
                StringComparer.OrdinalIgnoreCase);

            foreach (var run in runs)
            {
                if (!pointsByRun.TryGetValue(run, out var seq)) continue;
                foreach (var code in seq.Where(c => calc.HasKnownHeight(c) && !IsTechnical(c)))
                    knownHeights.Add(code);
            }

            var important = new HashSet<string>(knownHeights, StringComparer.OrdinalIgnoreCase);
            foreach (var code in shared.Where(c => !IsTechnical(c)))
                important.Add(code);

            // Раскладка узлов
            var positions = LayoutNodes(important.ToList(), w, h);

            // Рисуем рёбра
            int idx = 0;
            foreach (var run in runs)
            {
                if (!pointsByRun.TryGetValue(run, out var seq) || seq.Count < 2) continue;

                var color = Colors[idx++ % Colors.Length];
                var stroke = run.IsActive ? color : Color.FromRgb(140, 140, 140);

                var start = seq.First();
                var end = seq.Last();
                bool closed = string.Equals(start, end, StringComparison.OrdinalIgnoreCase);

                if (closed)
                {
                    if (positions.TryGetValue(start, out var pos))
                        DrawLoop(canvas, pos, stroke, run.IsActive, run.DisplayName, seq.Count, run.Tooltip);
                }
                else
                {
                    var p1 = GetOrCreatePosition(positions, start, end, idx, w, h);
                    var p2 = GetOrCreatePosition(positions, end, start, idx + 1, w, h);
                    DrawEdge(canvas, p1, p2, stroke, run.IsActive, run.DisplayName, seq.Count, run.Tooltip);
                }
            }

            // Рисуем узлы поверх
            foreach (var (code, pos) in positions)
            {
                bool isKnown = knownHeights.Contains(code);
                bool isShared = shared.Contains(code);
                if (!isKnown && !isShared) continue;

                if (isKnown)
                    DrawTriangle(canvas, pos, 14, System.Windows.Media.Colors.Black, $"{code}\nИзвестная высота");
                else
                    DrawCircle(canvas, pos, 10, System.Windows.Media.Colors.OrangeRed, $"{code}\nОбщая точка");

                DrawLabel(canvas, code, pos.X + 10, pos.Y - 8);
            }
        }

        private Point GetOrCreatePosition(Dictionary<string, Point> positions, string code, string other, int idx, double w, double h)
        {
            if (positions.TryGetValue(code, out var pos)) return pos;

            if (positions.TryGetValue(other, out var otherPos))
            {
                var angle = idx * 0.5;
                pos = new Point(otherPos.X + 60 * Math.Cos(angle), otherPos.Y + 60 * Math.Sin(angle));
            }
            else
            {
                var angle = idx * 0.7;
                var r = Math.Min(w, h) / 4;
                pos = new Point(w / 2 + r * Math.Cos(angle), h / 2 + r * Math.Sin(angle));
            }

            pos = Clamp(pos, Padding, w, h);
            positions[code] = pos;
            return pos;
        }

        private Dictionary<string, Point> LayoutNodes(List<string> nodes, double w, double h)
        {
            var result = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            if (nodes.Count == 0) return result;

            var cx = w / 2;
            var cy = h / 2;
            var r = Math.Min(w, h) / 2 - Padding - 50;

            for (int i = 0; i < nodes.Count; i++)
            {
                var angle = -Math.PI / 2 + 2 * Math.PI * i / nodes.Count;
                result[nodes[i]] = new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
            }

            return result;
        }

        private void DrawLoop(Canvas canvas, Point pos, Color stroke, bool active, string name, int count, string? tip)
        {
            const int r = 20;
            var center = new Point(pos.X, pos.Y - r - 8);

            canvas.Children.Add(new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = active ? 2.5 : 1.8,
                StrokeDashArray = active ? null : new DoubleCollection { 4, 2 },
                ToolTip = tip
            }.At(center.X - r, center.Y - r));

            DrawLabel(canvas, $"{name} ({count})", center.X - 25, center.Y - 8, stroke);
        }

        private void DrawEdge(Canvas canvas, Point from, Point to, Color stroke, bool active, string name, int count, string? tip)
        {
            canvas.Children.Add(new Line
            {
                X1 = from.X, Y1 = from.Y,
                X2 = to.X, Y2 = to.Y,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = active ? 2.5 : 1.8,
                StrokeDashArray = active ? null : new DoubleCollection { 4, 2 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                ToolTip = tip
            });

            var mid = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
            DrawLabel(canvas, $"{name} ({count})", mid.X - 30, mid.Y - 10, stroke);
        }

        private void DrawTriangle(Canvas canvas, Point center, double size, Color fill, string tip)
        {
            var h = size * 0.866;
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection
                {
                    new Point(center.X, center.Y - h * 0.6),
                    new Point(center.X - size / 2, center.Y + h * 0.4),
                    new Point(center.X + size / 2, center.Y + h * 0.4)
                },
                Fill = new SolidColorBrush(fill),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                ToolTip = tip
            });
        }

        private void DrawCircle(Canvas canvas, Point center, double size, Color fill, string tip)
        {
            canvas.Children.Add(new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(fill),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                ToolTip = tip
            }.At(center.X - size / 2, center.Y - size / 2));
        }

        private void DrawLabel(Canvas canvas, string text, double x, double y, Color? color = null)
        {
            canvas.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = color.HasValue ? new SolidColorBrush(color.Value) : Brushes.Black,
                Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Padding = new Thickness(2, 0, 2, 0)
            }.At(x, y));
        }

        private Dictionary<LineSummary, List<string>> GetPointSequences(List<LineSummary> runs, List<TraverseRow> rows)
        {
            var result = new Dictionary<LineSummary, List<string>>();

            foreach (var run in runs)
            {
                var runRows = rows
                    .Where(r => r.LineSummary?.Index == run.Index ||
                                string.Equals(r.LineName, run.DisplayName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.Index);

                var seq = new List<string>();

                if (!string.IsNullOrWhiteSpace(run.StartTarget))
                    seq.Add(run.StartTarget.Trim());

                foreach (var row in runRows)
                {
                    AddIfNew(seq, row.BackCode);
                    AddIfNew(seq, row.ForeCode);
                }

                if (seq.Count < 2)
                {
                    seq.Clear();
                    if (!string.IsNullOrWhiteSpace(run.StartTarget))
                        seq.Add(run.StartTarget.Trim());
                    if (!string.IsNullOrWhiteSpace(run.EndTarget) &&
                        !string.Equals(run.EndTarget, run.StartTarget, StringComparison.OrdinalIgnoreCase))
                        seq.Add(run.EndTarget.Trim());
                }

                result[run] = seq;
            }

            return result;
        }

        private static void AddIfNew(List<string> list, string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            var trimmed = code.Trim();
            if (list.Count == 0 || !string.Equals(list[^1], trimmed, StringComparison.OrdinalIgnoreCase))
                list.Add(trimmed);
        }

        private static bool IsTechnical(string code) =>
            string.IsNullOrWhiteSpace(code) ||
            code.Contains(':') ||
            code.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } parts &&
            parts.All(p => p.All(c => char.IsDigit(c) || c == '.' || c == ':'));

        private static Point Clamp(Point p, double margin, double w, double h) =>
            new(Math.Clamp(p.X, margin, w - margin), Math.Clamp(p.Y, margin, h - margin));
    }

    internal static class CanvasExtensions
    {
        public static T At<T>(this T element, double left, double top) where T : UIElement
        {
            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            return element;
        }
    }
}
