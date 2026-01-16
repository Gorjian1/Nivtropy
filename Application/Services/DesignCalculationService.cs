using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Реализация сервиса для расчёта проектных высот и распределения невязки
    /// </summary>
    public class DesignCalculationService : IDesignCalculationService
    {
        /// <summary>
        /// Строит строки проектирования из данных нивелирного хода
        /// </summary>
        public DesignCalculationResult BuildDesignRows(
            IEnumerable<StationDto> traverseRows,
            double startHeight,
            double targetClosure)
        {
            var items = traverseRows.ToList();
            var result = new DesignCalculationResult();

            if (items.Count == 0)
            {
                return result;
            }

            // Расчет фактической невязки
            var originalClosure = items
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value);
            result.ActualClosure = originalClosure;

            // Расчет общей длины хода (в метрах)
            var totalDistance = 0.0;
            foreach (var row in items)
            {
                var avgDist = ((row.BackDistance ?? 0) + (row.ForeDistance ?? 0)) / 2.0;
                totalDistance += avgDist;
            }
            result.TotalDistance = totalDistance;

            // Расчет допустимой невязки по формуле для IV класса: 20 мм × √L (L в км)
            var lengthKm = totalDistance / 1000.0;
            result.AllowableClosure = 0.020 * Math.Sqrt(Math.Max(lengthKm, 1e-6)); // в метрах

            // Проверка допуска
            var absActualClosure = Math.Abs(originalClosure);
            if (absActualClosure <= result.AllowableClosure)
            {
                result.ClosureStatus = "✓ В пределах допуска";
            }
            else
            {
                result.ClosureStatus = $"✗ ПРЕВЫШЕНИЕ допуска! ({absActualClosure:F4} м > {result.AllowableClosure:F4} м)";
            }

            // Расчет невязки для распределения
            var closureToDistribute = targetClosure - originalClosure;

            // Распределение поправок ПРОПОРЦИОНАЛЬНО ДЛИНАМ секций
            double correctionFactor = totalDistance > 0 ? closureToDistribute / totalDistance : 0;

            double runningHeight = startHeight;
            double adjustedSum = 0;

            foreach (var row in items)
            {
                // Средняя длина для данного хода
                var avgDistance = ((row.BackDistance ?? 0) + (row.ForeDistance ?? 0)) / 2.0;

                // Поправка пропорционально длине данного хода
                double correction = row.DeltaH.HasValue
                    ? correctionFactor * avgDistance
                    : 0;

                double? adjustedDelta = row.DeltaH.HasValue
                    ? row.DeltaH + correction
                    : null;

                if (adjustedDelta.HasValue)
                {
                    runningHeight += adjustedDelta.Value;
                    adjustedSum += adjustedDelta.Value;
                }

                var designRow = new DesignPointDto
                {
                    Index = row.Index,
                    Station = string.IsNullOrWhiteSpace(row.BackCode) && string.IsNullOrWhiteSpace(row.ForeCode)
                        ? row.LineName
                        : $"{row.BackCode ?? "?"} → {row.ForeCode ?? "?"}",
                    Distance = avgDistance > 0 ? avgDistance : null,
                    OriginalDeltaH = row.DeltaH,
                    Correction = correction,
                    AdjustedDeltaH = adjustedDelta,
                    DesignedHeight = runningHeight,
                    OriginalHeight = runningHeight,
                    OriginalDistance = avgDistance > 0 ? avgDistance : null,
                    IsEdited = false
                };

                result.Rows.Add(designRow);
            }

            result.DesignedClosure = adjustedSum;

            // Средняя поправка на станцию (для информации)
            var adjustableCount = items.Count(r => r.DeltaH.HasValue);
            result.CorrectionPerStation = adjustableCount > 0 ? closureToDistribute / adjustableCount : 0;

            return result;
        }

        /// <summary>
        /// Вычисляет статистику невязки для хода
        /// </summary>
        public ClosureStatistics CalculateClosureStatistics(IEnumerable<StationDto> traverseRows)
        {
            var items = traverseRows.ToList();
            var stats = new ClosureStatistics();

            if (items.Count == 0)
            {
                return stats;
            }

            // Расчет фактической невязки
            stats.ActualClosure = items
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value);

            // Расчет общей длины хода (в метрах)
            var totalDistance = 0.0;
            foreach (var row in items)
            {
                var avgDist = ((row.BackDistance ?? 0) + (row.ForeDistance ?? 0)) / 2.0;
                totalDistance += avgDist;
            }
            stats.TotalDistance = totalDistance;

            // Расчет допустимой невязки по формуле для IV класса: 20 мм × √L (L в км)
            var lengthKm = totalDistance / 1000.0;
            stats.AllowableClosure = 0.020 * Math.Sqrt(Math.Max(lengthKm, 1e-6)); // в метрах

            // Проверка допуска
            var absActualClosure = Math.Abs(stats.ActualClosure);
            stats.IsWithinTolerance = absActualClosure <= stats.AllowableClosure;

            if (stats.IsWithinTolerance)
            {
                stats.Status = "✓ В пределах допуска";
            }
            else
            {
                stats.Status = $"✗ ПРЕВЫШЕНИЕ допуска! ({absActualClosure:F4} м > {stats.AllowableClosure:F4} м)";
            }

            return stats;
        }

        /// <summary>
        /// Пересчитывает высоты всех точек начиная с указанного индекса + 1
        /// </summary>
        public void RecalculateHeightsFrom(IList<DesignPointDto> rows, int changedIndex)
        {
            if (rows == null || changedIndex < 0 || changedIndex >= rows.Count)
                return;

            // Начинаем со следующей строки
            for (int i = changedIndex + 1; i < rows.Count; i++)
            {
                var prevRow = rows[i - 1];
                var currentRow = rows[i];

                // Пересчитываем высоту: H_current = H_prev + ΔH_adjusted
                if (currentRow.AdjustedDeltaH.HasValue)
                {
                    currentRow.DesignedHeight = prevRow.DesignedHeight + currentRow.AdjustedDeltaH.Value;
                }
            }
        }

        /// <summary>
        /// Пересчитывает поправки для всех строк и высоты
        /// Вызывается при изменении дистанции (Distance)
        /// </summary>
        public double RecalculateCorrectionsAndHeights(
            IList<DesignPointDto> rows,
            double startHeight,
            double targetClosure)
        {
            if (rows == null || rows.Count == 0)
                return 0;

            // Рассчитываем фактическую невязку (сумма исходных превышений)
            var originalClosure = rows
                .Where(r => r.OriginalDeltaH.HasValue)
                .Sum(r => r.OriginalDeltaH!.Value);

            // Рассчитываем общую длину хода с учетом отредактированных дистанций
            var totalDistance = rows.Sum(r => r.Distance ?? 0);

            // Расчет невязки для распределения
            var closureToDistribute = targetClosure - originalClosure;

            // Распределение поправок ПРОПОРЦИОНАЛЬНО ДЛИНАМ
            double correctionFactor = totalDistance > 0 ? closureToDistribute / totalDistance : 0;

            double runningHeight = startHeight;
            double adjustedSum = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                // Поправка пропорционально длине данного хода
                double correction = row.OriginalDeltaH.HasValue
                    ? correctionFactor * (row.Distance ?? 0)
                    : 0;

                double? adjustedDelta = row.OriginalDeltaH.HasValue
                    ? row.OriginalDeltaH + correction
                    : null;

                row.Correction = correction;
                row.AdjustedDeltaH = adjustedDelta;

                if (adjustedDelta.HasValue)
                {
                    runningHeight += adjustedDelta.Value;
                    adjustedSum += adjustedDelta.Value;
                }

                // Обновляем высоту только если строка не была отредактирована вручную
                if (!row.IsEdited || i == 0)
                {
                    row.DesignedHeight = runningHeight;
                }
            }

            return adjustedSum;
        }
    }
}
