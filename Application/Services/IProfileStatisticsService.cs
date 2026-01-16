using System.Collections.Generic;
using Nivtropy.Application.DTOs;

using Nivtropy.Presentation.Models; // TODO: Remove after migrating to Domain models
namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Интерфейс сервиса для вычисления статистики профилей и обнаружения аномалий
    /// </summary>
    public interface IProfileStatisticsService
    {
        /// <summary>
        /// Вычисляет статистику и обнаруживает аномалии в данных хода
        /// </summary>
        /// <param name="rows">Строки хода</param>
        /// <param name="sensitivitySigma">Чувствительность обнаружения аномалий (в сигмах)</param>
        /// <returns>Статистика профиля с обнаруженными аномалиями</returns>
        ProfileStatistics CalculateStatistics(List<TraverseRow> rows, double sensitivitySigma = 2.5);

        /// <summary>
        /// Вычисляет расширенный диапазон высот для лучшей визуализации
        /// Добавляет ±50% от диапазона данных
        /// </summary>
        (double min, double max) CalculateExtendedRange(List<double> heights);
    }
}
