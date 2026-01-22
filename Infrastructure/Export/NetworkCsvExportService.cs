using System.Linq;
using System.Text;
using Nivtropy.Application.Export;
using Nivtropy.Constants;
using Nivtropy.Domain.Model;

namespace Nivtropy.Infrastructure.Export
{
    public class NetworkCsvExportService : INetworkCsvExportService
    {
        public string BuildCsv(LevelingNetwork network)
        {
            var csv = new StringBuilder();

            foreach (var run in network.Runs.Where(r => r.IsActive))
            {
                csv.AppendLine($"===== НАЧАЛО ХОДА: {run.Name} =====");

                var lengthBack = run.Observations.Sum(o => o.BackDistance.Meters);
                var lengthFore = run.Observations.Sum(o => o.ForeDistance.Meters);
                var totalLength = run.TotalLength.Meters;
                var armAccumulation = run.Observations.Sum(o => o.ArmDifference);
                var closureText = run.Closure != null
                    ? (run.Closure.Value.ValueMm / 1000.0).ToString(DisplayFormats.DeltaH)
                    : DisplayFormats.EmptyValue;

                csv.AppendLine(
                    $"Станций: {run.StationCount}; " +
                    $"Длина назад: {lengthBack:F2} м; " +
                    $"Длина вперёд: {lengthFore:F2} м; " +
                    $"Общая длина: {totalLength:F2} м; " +
                    $"Накопление плеч: {armAccumulation:F3} м; " +
                    $"Невязка: {closureText} м");

                csv.AppendLine("Номер;Ход;Точка;Станция;Длина станции (м);Отсчет назад (м);Отсчет вперед (м);Превышение (м);Поправка (мм);Превышение испр. (м);Высота непров. (м);Высота (м);Точка");

                var rawHeights = BuildRawHeights(run);

                if (run.StartPoint?.Height.IsKnown == true)
                {
                    var startHeight = run.StartPoint.Height.Value;
                    csv.AppendLine(string.Join(";",
                        0,
                        run.Name,
                        run.StartPoint.Code.Value,
                        "StartPoint",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        startHeight.ToString("F4"),
                        startHeight.ToString("F4"),
                        run.StartPoint.Code.Value));
                }

                foreach (var obs in run.Observations)
                {
                    rawHeights.TryGetValue(obs.Id, out var raw);
                    var heightZ0 = raw.foreHeight ?? raw.backHeight;
                    var height = obs.To.Height.IsKnown
                        ? obs.To.Height.Value
                        : (obs.From.Height.IsKnown ? obs.From.Height.Value : (double?)null);

                    csv.AppendLine(string.Join(";",
                        obs.StationIndex,
                        run.Name,
                        obs.To.Code.Value,
                        $"{obs.From.Code.Value} → {obs.To.Code.Value}",
                        obs.StationLength.Meters.ToString("F2"),
                        obs.BackReading.Meters.ToString("F4"),
                        obs.ForeReading.Meters.ToString("F4"),
                        obs.DeltaH.ToString("F4"),
                        (obs.Correction * 1000).ToString("F2"),
                        obs.AdjustedDeltaH.ToString("F4"),
                        heightZ0?.ToString("F4") ?? "",
                        height?.ToString("F4") ?? "",
                        obs.To.Code.Value));
                }

                csv.AppendLine($"===== КОНЕЦ ХОДА: {run.Name} =====");
                csv.AppendLine();
            }

            return csv.ToString();
        }

        private static Dictionary<Guid, (double? backHeight, double? foreHeight)> BuildRawHeights(Run run)
        {
            var result = new Dictionary<Guid, (double? backHeight, double? foreHeight)>();
            double? running = null;

            foreach (var obs in run.Observations)
            {
                if (obs.From.Type == PointType.Benchmark && obs.From.Height.IsKnown)
                {
                    running = obs.From.Height.Value;
                }

                double? back = running;
                double? fore = null;

                if (back.HasValue)
                {
                    fore = back + obs.DeltaH;
                    running = fore;
                }

                if (obs.To.Type == PointType.Benchmark && obs.To.Height.IsKnown)
                {
                    fore ??= obs.To.Height.Value;
                    running = obs.To.Height.Value;
                }

                result[obs.Id] = (back, fore);
            }

            return result;
        }
    }
}
