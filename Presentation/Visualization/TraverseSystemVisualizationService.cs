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
    /// Сервис визуализации системы нивелирных ходов как графа.
    /// Узлы: ходы и известные точки (реперы).
    /// Рёбра: связи между ходами по общим точкам и привязки к известным точкам.
    /// </summary>
    public class TraverseSystemVisualizationService : ITraverseSystemVisualizationService
    {
        private const double Padding = 24;
        private const double RunNodeSize = 18;
        private const double RunNodeDotSize = 6;
        private const double KnownNodeSize = 12;
        private const double ArrowLength = 10;
        private const double ArrowWidth = 6;
        private const int LayoutIterations = 120;
        private const double LayoutCooling = 0.95;
        private const double RepulsionStrength = 26000;
        private const double AttractionStrength = 0.08;

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
            positions = StretchToCanvas(positions, w, h, Padding);

            DrawEdges(canvas, graph, positions);
            DrawNodes(canvas, graph, positions);
        }

        private Graph BuildGraph(NetworkViewModel calc, List<LineSummary> runs)
        {
            var nodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
            var edges = new List<GraphEdge>();
            var runNodes = new Dictionary<int, GraphNode>();

            var enabledSharedPoints = new HashSet<string>(
                calc.SharedPoints.Where(p => p.IsEnabled).Select(p => NormalizeCode(p.Code)),
                StringComparer.OrdinalIgnoreCase);

            var sharedUsage = BuildSharedPointUsage(calc, enabledSharedPoints);
            var runPairs = BuildRunPairConnections(sharedUsage);

            int colorIndex = 0;
            foreach (var run in runs)
            {
                var runNode = new GraphNode(
                    $"run:{run.Index}",
                    run.DisplayName,
                    NodeKind.Run)
                {
                    Run = run,
                    IsActive = run.IsActive,
                    Color = Colors[colorIndex++ % Colors.Length]
                };

                nodes[runNode.Id] = runNode;
                runNodes[run.Index] = runNode;
            }

            foreach (var pair in runPairs.Values)
            {
                if (!runNodes.TryGetValue(pair.LeftRunIndex, out var left) ||
                    !runNodes.TryGetValue(pair.RightRunIndex, out var right))
                    continue;

                edges.Add(new GraphEdge(
                    left,
                    right,
                    EdgeKind.SharedRunConnection,
                    pair.SharedPointCodes));
            }

            foreach (var run in runs)
            {
                if (!runNodes.TryGetValue(run.Index, out var runNode))
                    continue;

                var knownCodes = GetKnownPointCodes(calc, run);
                foreach (var code in knownCodes)
                {
                    runNode.HasKnownPoint = true;
                    if (!nodes.TryGetValue(code, out var knownNode))
                    {
                        knownNode = new GraphNode(code, code, NodeKind.KnownPoint)
                        {
                            IsActive = true
                        };
                        nodes[code] = knownNode;
                    }

                    edges.Add(new GraphEdge(
                        runNode,
                        knownNode,
                        EdgeKind.KnownPointConnection,
                        new List<string> { code }));
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
            => string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();

        private static Dictionary<string, HashSet<int>> BuildSharedPointUsage(NetworkViewModel calc, HashSet<string> enabledSharedPoints)
        {
            var usage = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            void AddUsage(string? code, int runIndex)
            {
                if (runIndex == 0 || string.IsNullOrWhiteSpace(code))
                    return;

                var trimmed = NormalizeCode(code);
                if (!enabledSharedPoints.Contains(trimmed))
                    return;

                if (!usage.TryGetValue(trimmed, out var set))
                {
                    set = new HashSet<int>();
                    usage[trimmed] = set;
                }

                set.Add(runIndex);
            }

            foreach (var row in calc.Rows)
            {
                var runIndex = row.LineSummary?.Index ?? 0;
                AddUsage(row.BackCode, runIndex);
                AddUsage(row.ForeCode, runIndex);
            }

            return usage;
        }

        private static Dictionary<string, RunPairConnection> BuildRunPairConnections(Dictionary<string, HashSet<int>> sharedUsage)
        {
            var connections = new Dictionary<string, RunPairConnection>(StringComparer.OrdinalIgnoreCase);

            foreach (var (code, runIndexes) in sharedUsage)
            {
                var runs = runIndexes.OrderBy(index => index).ToList();
                if (runs.Count < 2)
                    continue;

                for (int i = 0; i < runs.Count - 1; i++)
                {
                    for (int j = i + 1; j < runs.Count; j++)
                    {
                        var key = $"{runs[i]}:{runs[j]}";
                        if (!connections.TryGetValue(key, out var connection))
                        {
                            connection = new RunPairConnection(runs[i], runs[j]);
                            connections[key] = connection;
                        }

                        connection.SharedPointCodes.Add(code);
                    }
                }
            }

            return connections;
        }

        private static HashSet<string> GetKnownPointCodes(NetworkViewModel calc, LineSummary run)
        {
            var knownCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in calc.Rows.Where(r => r.LineSummary?.Index == run.Index))
            {
                if (row.IsBackHeightKnown && !string.IsNullOrWhiteSpace(row.BackCode))
                    knownCodes.Add(NormalizeCode(row.BackCode));
                if (row.IsForeHeightKnown && !string.IsNullOrWhiteSpace(row.ForeCode))
                    knownCodes.Add(NormalizeCode(row.ForeCode));
            }

            return knownCodes;
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
                var radius = Math.Min(w, h) * 0.4;
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

        private static Dictionary<string, Point> StretchToCanvas(
            Dictionary<string, Point> positions,
            double width,
            double height,
            double padding)
        {
            if (positions.Count == 0)
                return positions;

            var minX = positions.Values.Min(p => p.X);
            var maxX = positions.Values.Max(p => p.X);
            var minY = positions.Values.Min(p => p.Y);
            var maxY = positions.Values.Max(p => p.Y);

            var spanX = Math.Max(1.0, maxX - minX);
            var spanY = Math.Max(1.0, maxY - minY);

            var scaleX = (width - padding * 2) / spanX;
            var scaleY = (height - padding * 2) / spanY;
            var scale = Math.Min(scaleX, scaleY);

            var offsetX = padding - minX * scale + (width - padding * 2 - spanX * scale) / 2;
            var offsetY = padding - minY * scale + (height - padding * 2 - spanY * scale) / 2;

            return positions.ToDictionary(
                kvp => kvp.Key,
                kvp => new Point(kvp.Value.X * scale + offsetX, kvp.Value.Y * scale + offsetY),
                StringComparer.OrdinalIgnoreCase);
        }

        private void DrawEdges(Canvas canvas, Graph graph, Dictionary<string, Point> positions)
        {
            foreach (var edge in graph.Edges)
            {
                if (!positions.TryGetValue(edge.From.Id, out var from) ||
                    !positions.TryGetValue(edge.To.Id, out var to))
                    continue;

                switch (edge.Kind)
                {
                    case EdgeKind.SharedRunConnection:
                        DrawRunConnection(canvas, edge, from, to);
                        break;
                    case EdgeKind.KnownPointConnection:
                        DrawKnownPointConnection(canvas, edge, from, to);
                        break;
                }
            }
        }

        private void DrawRunConnection(Canvas canvas, GraphEdge edge, Point from, Point to)
        {
            var isActive = edge.From.IsActive && edge.To.IsActive;
            var strokeColor = isActive ? Color.FromRgb(70, 70, 70) : Color.FromRgb(150, 150, 150);
            var thickness = isActive ? 1.6 : 1.2;
            var line = new Line
            {
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = thickness,
                StrokeDashArray = isActive ? null : new DoubleCollection { 4, 2 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                ToolTip = BuildSharedEdgeTooltip(edge)
            };
            canvas.Children.Add(line);

            if (edge.From.HasKnownPoint ^ edge.To.HasKnownPoint)
            {
                var arrowFrom = edge.From.HasKnownPoint ? from : to;
                var arrowTo = edge.From.HasKnownPoint ? to : from;
                DrawArrowAtMidpoint(canvas, arrowFrom, arrowTo, strokeColor, thickness, line.ToolTip?.ToString() ?? string.Empty);
            }
        }

        private void DrawKnownPointConnection(Canvas canvas, GraphEdge edge, Point from, Point to)
        {
            DrawDoubleLine(canvas, from, to, Color.FromRgb(40, 40, 40), 1.4, BuildKnownEdgeTooltip(edge));
        }

        private void DrawNodes(Canvas canvas, Graph graph, Dictionary<string, Point> positions)
        {
            foreach (var node in graph.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var pos))
                    continue;

                if (node.Kind == NodeKind.KnownPoint)
                {
                    DrawTriangle(canvas, pos, KnownNodeSize, System.Windows.Media.Colors.Black, $"{node.Label}\nИзвестная высота");
                    DrawLabel(canvas, node.Label, pos.X + 10, pos.Y - 8);
                    continue;
                }

                DrawRunNode(canvas, pos, RunNodeSize, RunNodeDotSize, node.Color, $"Ход: {node.Label}");
                DrawLabel(canvas, node.Label, pos.X + 10, pos.Y - 8, node.Color);
            }
        }

        private static string BuildSharedEdgeTooltip(GraphEdge edge)
        {
            var sharedPoints = edge.SharedPointCodes.Count > 0
                ? string.Join(", ", edge.SharedPointCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase))
                : "—";

            return $"Связь ходов: {edge.From.Label} ↔ {edge.To.Label}\nОбщие точки: {sharedPoints}";
        }

        private static string BuildKnownEdgeTooltip(GraphEdge edge)
        {
            var point = edge.SharedPointCodes.FirstOrDefault() ?? "—";
            return $"Привязка к известной точке: {point}\nХод: {edge.From.Label}";
        }

        private void DrawDoubleLine(Canvas canvas, Point from, Point to, Color stroke, double thickness, string toolTip)
        {
            var vector = to - from;
            if (vector.Length < 1)
                return;

            var offset = new Vector(-vector.Y, vector.X);
            offset.Normalize();
            offset *= 2.0;

            var line1 = new Line
            {
                X1 = from.X + offset.X,
                Y1 = from.Y + offset.Y,
                X2 = to.X + offset.X,
                Y2 = to.Y + offset.Y,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                ToolTip = toolTip
            };

            var line2 = new Line
            {
                X1 = from.X - offset.X,
                Y1 = from.Y - offset.Y,
                X2 = to.X - offset.X,
                Y2 = to.Y - offset.Y,
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                ToolTip = toolTip
            };

            canvas.Children.Add(line1);
            canvas.Children.Add(line2);
        }

        private void DrawArrowAtMidpoint(Canvas canvas, Point from, Point to, Color stroke, double thickness, string toolTip)
        {
            var direction = to - from;
            if (direction.Length < 1)
                return;

            direction.Normalize();
            var perpendicular = new Vector(-direction.Y, direction.X);

            var center = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
            var tip = center + direction * (ArrowLength / 2);
            var basePoint = center - direction * (ArrowLength / 2);
            var left = basePoint + perpendicular * (ArrowWidth / 2);
            var right = basePoint - perpendicular * (ArrowWidth / 2);

            var arrow = new Polygon
            {
                Points = new PointCollection { tip, left, right },
                Fill = new SolidColorBrush(stroke),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = thickness,
                ToolTip = toolTip
            };

            canvas.Children.Add(arrow);
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

        private void DrawRunNode(Canvas canvas, Point center, double size, double dotSize, Color color, string tip)
        {
            canvas.Children.Add(new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                ToolTip = tip
            }.At(center.X - size / 2, center.Y - size / 2));

            canvas.Children.Add(new Ellipse
            {
                Width = dotSize,
                Height = dotSize,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                ToolTip = tip
            }.At(center.X - dotSize / 2, center.Y - dotSize / 2));
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
            public GraphNode(string id, string label, NodeKind kind)
            {
                Id = id;
                Label = label;
                Kind = kind;
            }

            public string Id { get; }
            public string Label { get; }
            public NodeKind Kind { get; }
            public int Degree { get; set; }
            public bool IsActive { get; set; }
            public bool HasKnownPoint { get; set; }
            public LineSummary? Run { get; set; }
            public Color Color { get; set; } = Colors[0];
        }

        private sealed class GraphEdge
        {
            public GraphEdge(GraphNode from, GraphNode to, EdgeKind kind, List<string> sharedPointCodes)
            {
                From = from;
                To = to;
                Kind = kind;
                SharedPointCodes = sharedPointCodes;
            }

            public GraphNode From { get; }
            public GraphNode To { get; }
            public EdgeKind Kind { get; }
            public List<string> SharedPointCodes { get; }
        }

        private sealed class RunPairConnection
        {
            public RunPairConnection(int leftRunIndex, int rightRunIndex)
            {
                LeftRunIndex = leftRunIndex;
                RightRunIndex = rightRunIndex;
            }

            public int LeftRunIndex { get; }
            public int RightRunIndex { get; }
            public List<string> SharedPointCodes { get; } = new();
        }

        private enum NodeKind
        {
            Run,
            KnownPoint
        }

        private enum EdgeKind
        {
            SharedRunConnection,
            KnownPointConnection
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
