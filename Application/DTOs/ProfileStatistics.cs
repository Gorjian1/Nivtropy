using System;
using System.Collections.Generic;

namespace Nivtropy.Application.DTOs
{
    /// <summary>
    /// Статистика профиля хода с анализом аномалий
    /// </summary>
    public class ProfileStatistics
    {
        /// <summary>
        /// Список обнаруженных аномалий
        /// </summary>
        public List<OutlierDto> Outliers { get; set; } = new();

        /// <summary>
        /// Резкие перепады высот
        /// </summary>
        public List<OutlierDto> HeightJumps => Outliers.FindAll(o => o.Type == OutlierType.HeightJump);

        /// <summary>
        /// Аномальные длины станций
        /// </summary>
        public List<OutlierDto> StationLengthAnomalies => Outliers.FindAll(o => o.Type == OutlierType.StationLength);

        /// <summary>
        /// Превышения разности плеч
        /// </summary>
        public List<OutlierDto> ArmDifferenceIssues => Outliers.FindAll(o => o.Type == OutlierType.ArmDifference);

        /// <summary>
        /// Общее количество аномалий
        /// </summary>
        public int TotalOutliers => Outliers.Count;

        #region Статистика высот

        /// <summary>
        /// Минимальная высота
        /// </summary>
        public double MinHeight { get; set; }

        /// <summary>
        /// Максимальная высота
        /// </summary>
        public double MaxHeight { get; set; }

        /// <summary>
        /// Средняя высота
        /// </summary>
        public double MeanHeight { get; set; }

        /// <summary>
        /// Стандартное отклонение высот
        /// </summary>
        public double StdDevHeight { get; set; }

        /// <summary>
        /// Диапазон высот
        /// </summary>
        public double HeightRange => MaxHeight - MinHeight;

        #endregion

        #region Статистика превышений

        /// <summary>
        /// Максимальное превышение
        /// </summary>
        public double MaxDeltaH { get; set; }

        /// <summary>
        /// Минимальное превышение
        /// </summary>
        public double MinDeltaH { get; set; }

        /// <summary>
        /// Среднее превышение
        /// </summary>
        public double MeanDeltaH { get; set; }

        /// <summary>
        /// Стандартное отклонение превышений
        /// </summary>
        public double StdDevDeltaH { get; set; }

        /// <summary>
        /// Максимальное абсолютное превышение
        /// </summary>
        public double MaxAbsDeltaH { get; set; }

        #endregion

        #region Статистика длин станций

        /// <summary>
        /// Минимальная длина станции
        /// </summary>
        public double MinStationLength { get; set; }

        /// <summary>
        /// Максимальная длина станции
        /// </summary>
        public double MaxStationLength { get; set; }

        /// <summary>
        /// Средняя длина станции
        /// </summary>
        public double MeanStationLength { get; set; }

        /// <summary>
        /// Стандартное отклонение длин станций
        /// </summary>
        public double StdDevStationLength { get; set; }

        /// <summary>
        /// Общая длина хода
        /// </summary>
        public double TotalLength { get; set; }

        #endregion

        #region Статистика разности плеч

        /// <summary>
        /// Максимальная разность плеч
        /// </summary>
        public double MaxArmDifference { get; set; }

        /// <summary>
        /// Минимальная разность плеч
        /// </summary>
        public double MinArmDifference { get; set; }

        /// <summary>
        /// Средняя разность плеч
        /// </summary>
        public double MeanArmDifference { get; set; }

        /// <summary>
        /// Стандартное отклонение разности плеч
        /// </summary>
        public double StdDevArmDifference { get; set; }

        #endregion

        /// <summary>
        /// Количество станций
        /// </summary>
        public int StationCount { get; set; }

        /// <summary>
        /// Есть ли аномалии
        /// </summary>
        public bool HasOutliers => TotalOutliers > 0;

        /// <summary>
        /// Есть ли критичные аномалии
        /// </summary>
        public bool HasCriticalOutliers => Outliers.Exists(o => o.Severity >= 3);

        /// <summary>
        /// Получить краткое описание статистики
        /// </summary>
        public string GetSummary()
        {
            return $"Станций: {StationCount}, Длина: {TotalLength:F2} м, Аномалий: {TotalOutliers}";
        }
    }
}
