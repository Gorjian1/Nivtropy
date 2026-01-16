using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Presentation.Models; // TODO: Remove after migrating to Domain models

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Сервис для вычисления статистики профилей и обнаружения аномалий
    /// Извлечен из code-behind для соблюдения принципа единственной ответственности
    /// </summary>
    public class ProfileStatisticsService : IProfileStatisticsService
    {
        public ProfileStatistics CalculateStatistics(List<TraverseRow> rows, double sensitivitySigma = 2.5)
        {
            var stats = new ProfileStatistics
            {
                StationCount = rows.Count
            };

            if (rows.Count < 2)
                return stats;

            // Собираем данные
            var heights = new List<double>();
            var deltaHs = new List<double>();
            var stationLengths = new List<double>();
            var armDifferences = new List<double>();

            foreach (var row in rows)
            {
                var height = row.IsVirtualStation ? row.BackHeight : row.ForeHeight;
                if (height.HasValue) heights.Add(height.Value);

                if (row.DeltaH.HasValue) deltaHs.Add(row.DeltaH.Value);
                if (row.StationLength_m.HasValue) stationLengths.Add(row.StationLength_m.Value);
                if (row.ArmDifference_m.HasValue) armDifferences.Add(row.ArmDifference_m.Value);
            }

            // Статистика высот
            if (heights.Count > 0)
            {
                stats.MinHeight = heights.Min();
                stats.MaxHeight = heights.Max();
                stats.MeanHeight = heights.Average();
                stats.StdDevHeight = CalculateStdDev(heights, stats.MeanHeight);
            }

            // Статистика превышений
            if (deltaHs.Count > 0)
            {
                stats.MinDeltaH = deltaHs.Min();
                stats.MaxDeltaH = deltaHs.Max();
                stats.MeanDeltaH = deltaHs.Average();
                stats.StdDevDeltaH = CalculateStdDev(deltaHs, stats.MeanDeltaH);
                stats.MaxAbsDeltaH = deltaHs.Max(Math.Abs);
            }

            // Статистика длин станций
            if (stationLengths.Count > 0)
            {
                stats.MinStationLength = stationLengths.Min();
                stats.MaxStationLength = stationLengths.Max();
                stats.MeanStationLength = stationLengths.Average();
                stats.StdDevStationLength = CalculateStdDev(stationLengths, stats.MeanStationLength);
                stats.TotalLength = stationLengths.Sum();
            }

            // Статистика разности плеч
            if (armDifferences.Count > 0)
            {
                stats.MinArmDifference = armDifferences.Min();
                stats.MaxArmDifference = armDifferences.Max();
                stats.MeanArmDifference = armDifferences.Average();
                stats.StdDevArmDifference = CalculateStdDev(armDifferences, stats.MeanArmDifference);
            }

            // Поиск аномалий
            DetectOutliers(rows, stats, sensitivitySigma);

            return stats;
        }

        public (double min, double max) CalculateExtendedRange(List<double> heights)
        {
            if (!heights.Any()) return (0, 0);

            var actualMin = heights.Min();
            var actualMax = heights.Max();
            var range = actualMax - actualMin;

            // Если диапазон очень маленький (менее 1см), расширяем минимум на ±0.5м
            if (range < 0.01)
            {
                return (actualMin - 0.5, actualMax + 0.5);
            }

            // Добавляем половину диапазона сверху и снизу
            var expansion = range * 0.5;
            var minHeight = actualMin - expansion;
            var maxHeight = actualMax + expansion;

            return (minHeight, maxHeight);
        }

        /// <summary>
        /// Вычисляет стандартное отклонение
        /// </summary>
        private double CalculateStdDev(List<double> values, double mean)
        {
            if (values.Count < 2) return 0;
            var sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / (values.Count - 1));
        }

        /// <summary>
        /// Обнаруживает аномалии (выбросы) в данных
        /// </summary>
        private void DetectOutliers(List<TraverseRow> rows, ProfileStatistics stats, double sensitivitySigma)
        {
            // 1. Резкие перепады превышений (анализ последовательных разностей)
            for (int i = 1; i < rows.Count; i++)
            {
                var prevDeltaH = rows[i - 1].DeltaH;
                var currDeltaH = rows[i].DeltaH;

                if (prevDeltaH.HasValue && currDeltaH.HasValue)
                {
                    var diff = Math.Abs(currDeltaH.Value - prevDeltaH.Value);
                    var threshold = sensitivitySigma * stats.StdDevDeltaH;

                    if (threshold > 0.001 && diff > threshold)
                    {
                        var deviation = diff / stats.StdDevDeltaH;
                        stats.Outliers.Add(new OutlierPoint
                        {
                            StationIndex = rows[i].Index,
                            PointCode = rows[i].PointCode ?? "—",
                            Value = currDeltaH.Value,
                            ExpectedValue = prevDeltaH.Value,
                            DeviationInSigma = deviation,
                            Type = OutlierType.HeightJump,
                            Description = $"Резкий перепад: Δh = {diff:F4} м ({deviation:F1}σ)",
                            Severity = deviation > 4 ? 3 : (deviation > 3 ? 2 : 1)
                        });
                    }
                }
            }

            // 2. Аномальные длины станций
            if (stats.StdDevStationLength > 0.001)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var length = rows[i].StationLength_m;
                    if (length.HasValue)
                    {
                        var diff = Math.Abs(length.Value - stats.MeanStationLength);
                        var deviation = diff / stats.StdDevStationLength;

                        if (deviation > sensitivitySigma)
                        {
                            stats.Outliers.Add(new OutlierPoint
                            {
                                StationIndex = rows[i].Index,
                                PointCode = rows[i].PointCode ?? "—",
                                Value = length.Value,
                                ExpectedValue = stats.MeanStationLength,
                                DeviationInSigma = deviation,
                                Type = OutlierType.StationLength,
                                Description = $"Аномальная длина: {length.Value:F2} м ({deviation:F1}σ)",
                                Severity = deviation > 4 ? 2 : 1
                            });
                        }
                    }
                }
            }

            // 3. Превышение разности плеч (если есть допуск из класса нивелирования)
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsArmDifferenceExceeded)
                {
                    var armDiff = rows[i].ArmDifference_m;
                    if (armDiff.HasValue)
                    {
                        stats.Outliers.Add(new OutlierPoint
                        {
                            StationIndex = rows[i].Index,
                            PointCode = rows[i].PointCode ?? "—",
                            Value = Math.Abs(armDiff.Value),
                            ExpectedValue = 0,
                            DeviationInSigma = 0,
                            Type = OutlierType.ArmDifference,
                            Description = $"Превышена разность плеч: {Math.Abs(armDiff.Value):F2} м",
                            Severity = 2
                        });
                    }
                }
            }
        }
    }
}
