using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Export;
using Nivtropy.Constants;

namespace Nivtropy.Infrastructure.Export
{
    /// <summary>
    /// Сервис для экспорта данных нивелирования в различные форматы
    /// Извлечён из TraverseCalculationViewModel для соблюдения SRP
    /// </summary>
    public class TraverseExportService : ITraverseExportService
    {
        public string BuildCsv(IReadOnlyList<StationDto> rows)
        {
            var csv = new StringBuilder();

            // Группировка по ходам
            var groupedRows = rows.GroupBy(r => r.LineName).ToList();

            foreach (var group in groupedRows)
            {
                var lineName = group.Key;
                var rowsList = group.ToList();
                var lineSummary = rowsList.FirstOrDefault()?.RunSummary;

                // 1. Start-line - начало хода
                csv.AppendLine($"===== НАЧАЛО ХОДА: {lineName} =====");

                // 2. Info line - информация о ходе
                if (lineSummary != null)
                {
                    var lengthBack = lineSummary.TotalDistanceBack ?? 0;
                    var lengthFore = lineSummary.TotalDistanceFore ?? 0;
                    var totalLength = lineSummary.TotalLength ?? 0;
                    var armAccumulation = lineSummary.ArmDifferenceAccumulation ?? 0;
                    var stationCount = lineSummary.StationCount;
                    var closureText = FormatClosures(lineSummary.Closures);

                    csv.AppendLine($"Станций: {stationCount}; Длина назад: {lengthBack:F2} м; Длина вперёд: {lengthFore:F2} м; Общая длина: {totalLength:F2} м; Накопление плеч: {armAccumulation:F3} м; Невязка: {closureText} м");
                }

                // 3. Header row + data table
                csv.AppendLine("Номер;Ход;Точка;Станция;Длина станции (м);Отсчет назад (м);Отсчет вперед (м);Превышение (м);Поправка (мм);Превышение испр. (м);Высота непров. (м);Высота (м);Точка");

                foreach (var dataRow in rowsList)
                {
                    var heightZ0 = dataRow.IsVirtualStation ? dataRow.BackHeightRaw : dataRow.ForeHeightRaw;
                    var height = dataRow.IsVirtualStation ? dataRow.BackHeight : dataRow.ForeHeight;

                    csv.AppendLine(string.Join(";",
                        dataRow.Index,
                        dataRow.LineName,
                        GetPointCode(dataRow),
                        GetStationLabel(dataRow),
                        GetStationLength(dataRow)?.ToString("F2") ?? "",
                        dataRow.BackReading?.ToString("F4") ?? "",
                        dataRow.ForeReading?.ToString("F4") ?? "",
                        dataRow.DeltaH?.ToString("F4") ?? "",
                        dataRow.Correction.HasValue ? (dataRow.Correction.Value * 1000).ToString("F2") : "",
                        dataRow.AdjustedDeltaH?.ToString("F4") ?? "",
                        heightZ0?.ToString("F4") ?? "",
                        height?.ToString("F4") ?? "",
                        GetPointCode(dataRow)
                    ));
                }

                // 4. End-line - конец хода
                csv.AppendLine($"===== КОНЕЦ ХОДА: {lineName} =====");
                csv.AppendLine(); // Пустая строка между ходами
            }

            return csv.ToString();
        }

        private static string FormatClosures(IReadOnlyList<double> closures)
        {
            if (closures.Count == 0)
                return DisplayFormats.EmptyValue;

            return string.Join(", ", closures.Select(c => c.ToString(DisplayFormats.DeltaH)));
        }

        private static string GetPointCode(StationDto station)
        {
            return string.IsNullOrWhiteSpace(station.ForeCode)
                ? station.BackCode ?? "—"
                : station.ForeCode;
        }

        private static string GetStationLabel(StationDto station)
        {
            if (string.IsNullOrWhiteSpace(station.BackCode) && string.IsNullOrWhiteSpace(station.ForeCode))
                return station.LineName;

            return $"{station.BackCode ?? "?"} → {station.ForeCode ?? "?"}";
        }

        private static double? GetStationLength(StationDto station)
        {
            return (station.BackDistance.HasValue && station.ForeDistance.HasValue)
                ? station.BackDistance.Value + station.ForeDistance.Value
                : null;
        }
    }
}
