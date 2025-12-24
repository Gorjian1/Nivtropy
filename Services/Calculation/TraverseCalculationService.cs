using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;
using Nivtropy.ViewModels;

namespace Nivtropy.Services.Calculation
{
    /// <summary>
    /// Реализация сервиса расчётов нивелирного хода.
    /// Содержит stateless методы для расчётов высот, невязок и поправок.
    /// </summary>
    public class TraverseCalculationService : ITraverseCalculationService
    {
        /// <inheritdoc/>
        public IList<TraverseRow> CalculateHeights(IList<TraverseRow> rows, IDictionary<string, double> knownHeights)
        {
            if (rows == null || rows.Count == 0)
                return rows ?? new List<TraverseRow>();

            // Прямой ход: от начала к концу
            double? runningHeight = null;
            double? runningHeightZ0 = null;

            foreach (var row in rows)
            {
                // Проверяем, есть ли известная высота для задней точки
                if (!string.IsNullOrEmpty(row.BackCode) &&
                    knownHeights.TryGetValue(row.BackCode, out var backKnownHeight))
                {
                    runningHeight = backKnownHeight;
                    runningHeightZ0 = backKnownHeight;
                }

                // Устанавливаем высоту задней точки
                if (runningHeight.HasValue)
                {
                    row.BackHeight = runningHeight;
                    row.BackHeightZ0 = runningHeightZ0;
                }

                // Рассчитываем высоту передней точки
                if (runningHeight.HasValue && row.AdjustedDeltaH.HasValue)
                {
                    row.ForeHeight = runningHeight.Value + row.AdjustedDeltaH.Value;
                    runningHeight = row.ForeHeight;
                }

                if (runningHeightZ0.HasValue && row.DeltaH.HasValue)
                {
                    row.ForeHeightZ0 = runningHeightZ0.Value + row.DeltaH.Value;
                    runningHeightZ0 = row.ForeHeightZ0;
                }

                // Проверяем известную высоту передней точки
                if (!string.IsNullOrEmpty(row.ForeCode) &&
                    knownHeights.TryGetValue(row.ForeCode, out var foreKnownHeight))
                {
                    row.ForeHeight = foreKnownHeight;
                    runningHeight = foreKnownHeight;
                }
            }

            return rows;
        }

        /// <inheritdoc/>
        public IList<TraverseRow> DistributeClosure(IList<TraverseRow> rows, double closure)
        {
            if (rows == null || rows.Count == 0)
                return rows ?? new List<TraverseRow>();

            // Рассчитываем общую длину хода
            var totalLength = rows.Sum(r => r.StationLength_m ?? 0);
            if (totalLength <= 0)
                return rows;

            // Распределяем невязку пропорционально длинам станций
            foreach (var row in rows)
            {
                if (row.StationLength_m.HasValue && row.DeltaH.HasValue)
                {
                    var proportion = row.StationLength_m.Value / totalLength;
                    row.Correction = -closure * proportion;
                    row.AdjustedDeltaH = row.DeltaH.Value + row.Correction.Value;
                }
            }

            return rows;
        }

        /// <inheritdoc/>
        public double? CalculateClosure(IList<TraverseRow> rows, double? startHeight, double? endHeight)
        {
            if (rows == null || rows.Count == 0)
                return null;

            if (!startHeight.HasValue || !endHeight.HasValue)
                return null;

            // Сумма всех превышений
            var sumDeltaH = rows
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value);

            // Теоретическая сумма = конечная высота - начальная высота
            var theoreticalSum = endHeight.Value - startHeight.Value;

            // Невязка = фактическая сумма - теоретическая сумма
            return sumDeltaH - theoreticalSum;
        }

        /// <inheritdoc/>
        public double CalculateAllowableClosure(double totalLength, int stationCount, ToleranceMode toleranceMode, double coefficient)
        {
            return toleranceMode switch
            {
                ToleranceMode.SqrtLength => coefficient * Math.Sqrt(totalLength / 1000.0), // L в км
                ToleranceMode.SqrtStations => coefficient * Math.Sqrt(stationCount),
                _ => coefficient * Math.Sqrt(totalLength / 1000.0)
            };
        }

        /// <inheritdoc/>
        public IList<TraverseRow> ApplyCorrections(IList<TraverseRow> rows)
        {
            if (rows == null || rows.Count == 0)
                return rows ?? new List<TraverseRow>();

            foreach (var row in rows)
            {
                if (row.DeltaH.HasValue && row.Correction.HasValue)
                {
                    row.AdjustedDeltaH = row.DeltaH.Value + row.Correction.Value;
                }
                else if (row.DeltaH.HasValue)
                {
                    row.AdjustedDeltaH = row.DeltaH.Value;
                }
            }

            return rows;
        }
    }
}
