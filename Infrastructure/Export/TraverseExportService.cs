using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Nivtropy.Models;
using Nivtropy.Services.Dialog;

namespace Nivtropy.Infrastructure.Export
{
    /// <summary>
    /// Сервис для экспорта данных нивелирования в различные форматы
    /// Извлечён из TraverseCalculationViewModel для соблюдения SRP
    /// </summary>
    public class TraverseExportService : IExportService
    {
        private readonly IDialogService _dialogService;

        public TraverseExportService(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public bool ExportToCsv(IEnumerable<TraverseRow> rows, string? filePath = null)
        {
            if (filePath == null)
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    DefaultExt = "csv",
                    FileName = $"Нивелирование_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return false;

                filePath = saveFileDialog.FileName;
            }

            try
            {
                var csv = BuildCsvContent(rows.ToList());
                System.IO.File.WriteAllText(filePath, csv, Encoding.UTF8);

                _dialogService.ShowInfo($"Данные успешно экспортированы в:\n{filePath}", "Экспорт завершён");

                return true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка при экспорте:\n{ex.Message}", "Ошибка экспорта");

                return false;
            }
        }

        private string BuildCsvContent(List<TraverseRow> rows)
        {
            var csv = new StringBuilder();

            // Группировка по ходам
            var groupedRows = rows.GroupBy(r => r.LineName).ToList();

            foreach (var group in groupedRows)
            {
                var lineName = group.Key;
                var rowsList = group.ToList();
                var lineSummary = rowsList.FirstOrDefault()?.LineSummary;

                // 1. Start-line - начало хода
                csv.AppendLine($"===== НАЧАЛО ХОДА: {lineName} =====");

                // 2. Info line - информация о ходе
                if (lineSummary != null)
                {
                    var lengthBack = lineSummary.TotalDistanceBack ?? 0;
                    var lengthFore = lineSummary.TotalDistanceFore ?? 0;
                    var totalLength = lineSummary.TotalAverageLength ?? 0;
                    var armAccumulation = lineSummary.ArmDifferenceAccumulation ?? 0;
                    var stationCount = lineSummary.RecordCount;
                    var closureText = lineSummary.ClosuresDisplay;

                    csv.AppendLine($"Станций: {stationCount}; Длина назад: {lengthBack:F2} м; Длина вперёд: {lengthFore:F2} м; Общая длина: {totalLength:F2} м; Накопление плеч: {armAccumulation:F3} м; Невязка: {closureText} м");
                }

                // 3. Header row + data table
                csv.AppendLine("Номер;Ход;Точка;Станция;Длина станции (м);Отсчет назад (м);Отсчет вперед (м);Превышение (м);Поправка (мм);Превышение испр. (м);Высота непров. (м);Высота (м);Точка");

                foreach (var dataRow in rowsList)
                {
                    var heightZ0 = dataRow.IsVirtualStation ? dataRow.BackHeightZ0 : dataRow.ForeHeightZ0;
                    var height = dataRow.IsVirtualStation ? dataRow.BackHeight : dataRow.ForeHeight;

                    csv.AppendLine(string.Join(";",
                        dataRow.Index,
                        dataRow.LineName,
                        dataRow.PointCode,
                        dataRow.Station,
                        dataRow.StationLength_m?.ToString("F2") ?? "",
                        dataRow.Rb_m?.ToString("F4") ?? "",
                        dataRow.Rf_m?.ToString("F4") ?? "",
                        dataRow.DeltaH?.ToString("F4") ?? "",
                        dataRow.Correction.HasValue ? (dataRow.Correction.Value * 1000).ToString("F2") : "",
                        dataRow.AdjustedDeltaH?.ToString("F4") ?? "",
                        heightZ0?.ToString("F4") ?? "",
                        height?.ToString("F4") ?? "",
                        dataRow.PointCode
                    ));
                }

                // 4. End-line - конец хода
                csv.AppendLine($"===== КОНЕЦ ХОДА: {lineName} =====");
                csv.AppendLine(); // Пустая строка между ходами
            }

            return csv.ToString();
        }
    }
}
