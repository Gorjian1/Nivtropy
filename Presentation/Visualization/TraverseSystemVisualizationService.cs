using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.ViewModels;

namespace Nivtropy.Presentation.Visualization
{
    /// <summary>
    /// Сервис визуализации нивелирной сети как графа.
    /// Узлы: реперы (▲) и связующие точки (●)
    /// Рёбра: разомкнутые ходы (линии) и замкнутые ходы (петли)
    /// </summary>
    public class TraverseSystemVisualizationService : ITraverseSystemVisualizationService
    {
        private const double Padding = 20;
        private const double NodeRadius = 6;
        private const double KnownNodeSize = 12;
        private const double SharedNodeSize = 12;
        private const int LayoutIterations = 120;
        private const double LayoutCooling = 0.95;
        private const double RepulsionStrength = 22000;
        private const double AttractionStrength = 0.05;

        private static readonly Color[] Colors =
        {
            Color.FromRgb(25, 118, 210),  // Синий
            Color.FromRgb(56, 142, 60),   // Зелёный
            Color.FromRgb(251, 140, 0),   // Оранжевый
            Color.FromRgb(142, 36, 170),  // Фиолетовый
            Color.FromRgb(0, 150, 136),   // Бирюзовый
            Color.FromRgb(211, 47, 47)    // Красный
        };

        public void DrawSystemVisualization(Canvas canvas, NetworkJournalViewModel viewModel)
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

        private void Draw(Canvas canvas, NetworkViewModel calc, List<LineSummary> runs, double w, double h)
        {
            var graph = BuildGraph(calc, runs);
            if (graph.Nodes.Count == 0)
                return;

            var positions = LayoutGraph(graph, w, h);

            DrawEdges(canvas, graph, positions);
            DrawNodes(canvas, graph, positions);
        }

        private Graph BuildGraph(NetworkViewModel calc, List<LineSummary> runs)
        {
            var nodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
            var edges = new List<GraphEdge>();

            var shared = new HashSet<string>(
                calc.SharedPoints.Where(p => p.IsEnabled).Select(p => p.Code),
                StringComparer.OrdinalIgnoreCase);

            foreach (var run in runs)
            {
                var runRows = calc.Rows
                    .Where(r => r.LineSummary?.Index == run.Index ||
                                string.Equals(r.LineName, run.DisplayName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.Index)
                    .ToList();

                foreach (var row in runRows)
                {
                    if (string.IsNullOrWhiteSpace(row.BackCode) || string.IsNullOrWhiteSpace(row.ForeCode))
                        continue;

                    var fromId = NormalizeCode(row.BackCode);
                    var toId = NormalizeCode(row.ForeCode);
                    if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                        continue;

                    var fromNode = GetOrCreateNode(nodes, fromId, calc, shared);
                    var toNode = GetOrCreateNode(nodes, toId, calc, shared);

                    edges.Add(new GraphEdge(
                        fromNode,
                        toNode,
                        run,
                        row.Index,
                        row.DeltaH,
                        row.StationLength_m));
                }
            }

            foreach (var edge in edges)
            {
                edge.From.Degree++;
                edge.To.Degree++;
            }

            return new Graph(nodes.Values.ToList(), edges);
        }

        private static string NormalizeCode(string? code)
        {
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
        }

        private static GraphNode GetOrCreateNode(
            Dictionary<string, GraphNode> nodes,
            string code,
            NetworkViewModel calc,
            HashSet<string> shared)
        {
            if (nodes.TryGetValue(code, out var existing))
                return existing;

            bool isKnown = calc.HasKnownHeight(code);
            bool isShared = shared.Contains(code);

            var node = new GraphNode(code, code, isKnown, isShared);
            nodes[code] = node;
            return node;
        }

        private Dictionary<string, Point> LayoutGraph(Graph graph, double w, double h)
        {
            var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            var random = new Random(42);
            var nodes = graph.Nodes;
            var centerX = w / 2;
            var centerY = h / 2;

            foreach (var node in nodes)
            {
                var angle = random.NextDouble() * 2 * Math.PI;
                var radius = Math.Min(w, h) * 0.25;
                positions[node.Id] = new Point(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle));
            }

            var temperature = Math.Min(w, h) * 0.1;

            for (int iter = 0; iter < LayoutIterations; iter++)
            {
                var displacements = nodes.ToDictionary(n => n.Id, _ => new Vector());

                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var v = nodes[i];
                        var u = nodes[j];
                        var delta = positions[v.Id] - positions[u.Id];
                        var distance = Math.Max(20, delta.Length);
                        var force = RepulsionStrength / (distance * distance);
                        var direction = delta / distance;
                        var displacement = direction * force;
                        displacements[v.Id] += displacement;
                        displacements[u.Id] -= displacement;
                    }
                }

                foreach (var edge in graph.Edges)
                {
                    var from = edge.From.Id;
                    var to = edge.To.Id;
                    var delta = positions[from] - positions[to];
                    var distance = Math.Max(20, delta.Length);
                    var force = AttractionStrength * (distance * distance);
                    var direction = delta / distance;
                    var displacement = direction * force;
                    displacements[from] -= displacement;
                    displacements[to] += displacement;
                }

                foreach (var node in nodes)
                {
                    var displacement = displacements[node.Id];
                    var distance = Math.Max(0.1, displacement.Length);
                    var limited = displacement / distance * Math.Min(distance, temperature);
                    var newPos = positions[node.Id] + limited;
                    positions[node.Id] = Clamp(newPos, Padding, w, h);
                }

                temperature *= LayoutCooling;
            }

            return positions;
        }

        private void DrawEdges(Canvas canvas, Graph graph, Dictionary<string, Point> positions)
        {
            var runPalette = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            int idx = 0;

            foreach (var edge in graph.Edges)
            {
                if (!positions.TryGetValue(edge.From.Id, out var from) ||
                    !positions.TryGetValue(edge.To.Id, out var to))
                    continue;

                if (!runPalette.TryGetValue(edge.Run.DisplayName, out var color))
                {
                    color = Colors[idx++ % Colors.Length];
                    runPalette[edge.Run.DisplayName] = color;
                }

                var stroke = edge.Run.IsActive ? color : Color.FromRgb(150, 150, 150);
                var line = new Line
                {
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = to.X,
                    Y2 = to.Y,
                    Stroke = new SolidColorBrush(stroke),
                    StrokeThickness = edge.Run.IsActive ? 2.2 : 1.6,
                    StrokeDashArray = edge.Run.IsActive ? null : new DoubleCollection { 4, 2 },
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    ToolTip = BuildEdgeTooltip(edge)
                };
                canvas.Children.Add(line);
            }
        }

        private void DrawNodes(Canvas canvas, Graph graph, Dictionary<string, Point> positions)
        {
            foreach (var node in graph.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var pos))
                    continue;

                if (node.IsKnown)
                {
                    DrawTriangle(canvas, pos, KnownNodeSize, System.Windows.Media.Colors.Black, $"{node.Label}\nИзвестная высота");
                }
                else if (node.IsShared)
                {
                    DrawDoubleCircle(canvas, pos, SharedNodeSize, System.Windows.Media.Colors.OrangeRed, $"{node.Label}\nОбщая точка");
                }
                else
                {
                    DrawCircle(canvas, pos, NodeRadius * 2, System.Windows.Media.Colors.DodgerBlue, $"{node.Label}");
                }

                if (node.IsKnown || node.IsShared || node.Degree > 2)
                {
                    DrawLabel(canvas, node.Label, pos.X + 10, pos.Y - 8);
                }
            }
        }

        private static string BuildEdgeTooltip(GraphEdge edge)
        {
            var delta = edge.DeltaH.HasValue ? edge.DeltaH.Value.ToString("F4") : "—";
            var length = edge.LengthMeters.HasValue ? edge.LengthMeters.Value.ToString("F2") : "—";
            return $"Ход: {edge.Run.DisplayName}\nСтанция: {edge.StationIndex}\nΔh: {delta} м\nL: {length} м";
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
                StrokeThickness = 1.6,
                ToolTip = tip
            }.At(center.X - size / 2, center.Y - size / 2));
        }

        private void DrawDoubleCircle(Canvas canvas, Point center, double size, Color fill, string tip)
        {
            DrawCircle(canvas, center, size + 4, System.Windows.Media.Colors.White, tip);
            DrawCircle(canvas, center, size, fill, tip);
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

        private static Point Clamp(Point p, double margin, double w, double h) =>
            new(Math.Clamp(p.X, margin, w - margin), Math.Clamp(p.Y, margin, h - margin));

        private sealed class Graph
        {
            public Graph(List<GraphNode> nodes, List<GraphEdge> edges)
            {
                Nodes = nodes;
                Edges = edges;
            }

            public List<GraphNode> Nodes { get; }
            public List<GraphEdge> Edges { get; }
        }

        private sealed class GraphNode
        {
            public GraphNode(string id, string label, bool isKnown, bool isShared)
            {
                Id = id;
                Label = label;
                IsKnown = isKnown;
                IsShared = isShared;
            }

            public string Id { get; }
            public string Label { get; }
            public bool IsKnown { get; }
            public bool IsShared { get; }
            public int Degree { get; set; }
        }

        private sealed class GraphEdge
        {
            public GraphEdge(GraphNode from, GraphNode to, LineSummary run, int stationIndex, double? deltaH, double? lengthMeters)
            {
                From = from;
                To = to;
                Run = run;
                StationIndex = stationIndex;
                DeltaH = deltaH;
                LengthMeters = lengthMeters;
            }

            public GraphNode From { get; }
            public GraphNode To { get; }
            public LineSummary Run { get; }
            public int StationIndex { get; }
            public double? DeltaH { get; }
            public double? LengthMeters { get; }
        }
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
