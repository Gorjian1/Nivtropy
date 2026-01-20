using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Export;
using Nivtropy.Constants;

namespace Nivtropy.Infrastructure.Export
{
    /// <summary>
    /// Сервис для формирования CSV экспорта по данным ходов.
    /// </summary>
    public class TraverseExportService : ITraverseExportService
    {
        public string BuildCsv(IEnumerable<StationDto> rows)
        {
            return BuildCsvContent(rows.ToList());
        }

        private static string BuildCsvContent(List<StationDto> rows)
        {
            var csv = new StringBuilder();

            var groupedRows = rows.GroupBy(r => r.LineName).ToList();

            foreach (var group in groupedRows)
            {
                var lineName = group.Key;
                var rowsList = group.ToList();
                var lineSummary = rowsList.FirstOrDefault()?.RunSummary;

                csv.AppendLine($"===== НАЧАЛО ХОДА: {lineName} =====");

                if (lineSummary != null)
                {
                    var lengthBack = lineSummary.TotalDistanceBack ?? 0;
                    var lengthFore = lineSummary.TotalDistanceFore ?? 0;
                    var totalLength = lineSummary.TotalLength ?? 0;
                    var armAccumulation = lineSummary.ArmDifferenceAccumulation ?? 0;
                    var stationCount = lineSummary.StationCount;
                    var closureText = lineSummary.Closures.Count > 0
                        ? string.Join(", ", lineSummary.Closures.Select(c => c.ToString(DisplayFormats.DeltaH)))
                        : DisplayFormats.EmptyValue;

                    csv.AppendLine($"Станций: {stationCount}; Длина назад: {lengthBack:F2} м; Длина вперёд: {lengthFore:F2} м; Общая длина: {totalLength:F2} м; Накопление плеч: {armAccumulation:F3} м; Невязка: {closureText} м");
                }

                csv.AppendLine("Номер;Ход;Точка;Станция;Длина станции (м);Отсчет назад (м);Отсчет вперед (м);Превышение (м);Поправка (мм);Превышение испр. (м);Высота непров. (м);Высота (м);Точка");

                foreach (var dataRow in rowsList)
                {
                    var heightZ0 = dataRow.IsVirtualStation ? dataRow.BackHeightRaw : dataRow.ForeHeightRaw;
                    var height = dataRow.IsVirtualStation ? dataRow.BackHeight : dataRow.ForeHeight;
                    var pointCode = string.IsNullOrWhiteSpace(dataRow.ForeCode) ? (dataRow.BackCode ?? "—") : dataRow.ForeCode;
                    var station = string.IsNullOrWhiteSpace(dataRow.BackCode) && string.IsNullOrWhiteSpace(dataRow.ForeCode)
                        ? dataRow.LineName
                        : $"{dataRow.BackCode ?? "?"} → {dataRow.ForeCode ?? "?"}";

                    csv.AppendLine(string.Join(";",
                        dataRow.Index,
                        dataRow.LineName,
                        pointCode,
                        station,
                        dataRow.StationLength?.ToString("F2") ?? "",
                        dataRow.BackReading?.ToString("F4") ?? "",
                        dataRow.ForeReading?.ToString("F4") ?? "",
                        dataRow.DeltaH?.ToString("F4") ?? "",
                        dataRow.Correction.HasValue ? (dataRow.Correction.Value * 1000).ToString("F2") : "",
                        dataRow.AdjustedDeltaH?.ToString("F4") ?? "",
                        heightZ0?.ToString("F4") ?? "",
                        height?.ToString("F4") ?? "",
                        pointCode
                    ));
                }

                csv.AppendLine($"===== КОНЕЦ ХОДА: {lineName} =====");
                csv.AppendLine();
            }

            return csv.ToString();
        }
    }
}
