using System.Collections.Generic;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Сервис для расчёта проектных высот и распределения невязки.
    /// Используется в проектировании нивелирных ходов.
    /// </summary>
    public interface IDesignCalculationService
    {
        /// <summary>
        /// Строит строки проектирования из данных нивелирного хода
        /// </summary>
        /// <param name="traverseRows">Строки нивелирного хода</param>
        /// <param name="startHeight">Начальная высота</param>
        /// <param name="targetClosure">Целевая невязка (обычно 0)</param>
        /// <returns>Список строк проектирования с рассчитанными высотами</returns>
        DesignCalculationResult BuildDesignRows(
            IEnumerable<StationDto> traverseRows,
            double startHeight,
            double targetClosure);

        /// <summary>
        /// Вычисляет статистику невязки для хода
        /// </summary>
        /// <param name="traverseRows">Строки нивелирного хода</param>
        /// <returns>Статистика невязки</returns>
        ClosureStatistics CalculateClosureStatistics(IEnumerable<StationDto> traverseRows);

        /// <summary>
        /// Пересчитывает высоты после редактирования одной из строк
        /// </summary>
        /// <param name="rows">Список строк проектирования</param>
        /// <param name="changedIndex">Индекс измененной строки</param>
        void RecalculateHeightsFrom(IList<DesignPointDto> rows, int changedIndex);

        /// <summary>
        /// Пересчитывает поправки и высоты при изменении дистанций
        /// </summary>
        /// <param name="rows">Список строк проектирования</param>
        /// <param name="startHeight">Начальная высота</param>
        /// <param name="targetClosure">Целевая невязка</param>
        /// <returns>Новая невязка после распределения</returns>
        double RecalculateCorrectionsAndHeights(
            IList<DesignPointDto> rows,
            double startHeight,
            double targetClosure);
    }

    /// <summary>
    /// Результат расчёта проектных данных
    /// </summary>
    public class DesignCalculationResult
    {
        /// <summary>Строки проектирования</summary>
        public List<DesignPointDto> Rows { get; set; } = new();

        /// <summary>Фактическая невязка хода (сумма исходных превышений)</summary>
        public double ActualClosure { get; set; }

        /// <summary>Проектная невязка после распределения поправок</summary>
        public double DesignedClosure { get; set; }

        /// <summary>Средняя поправка на станцию</summary>
        public double CorrectionPerStation { get; set; }

        /// <summary>Общая длина хода в метрах</summary>
        public double TotalDistance { get; set; }

        /// <summary>Допустимая невязка по формуле для IV класса</summary>
        public double AllowableClosure { get; set; }

        /// <summary>Статус невязки (в пределах допуска или нет)</summary>
        public string ClosureStatus { get; set; } = "Нет данных";
    }

    /// <summary>
    /// Статистика невязки хода
    /// </summary>
    public class ClosureStatistics
    {
        /// <summary>Фактическая невязка (сумма превышений)</summary>
        public double ActualClosure { get; set; }

        /// <summary>Общая длина хода в метрах</summary>
        public double TotalDistance { get; set; }

        /// <summary>Допустимая невязка по формуле для IV класса: 20 мм × √L (L в км)</summary>
        public double AllowableClosure { get; set; }

        /// <summary>В пределах допуска или нет</summary>
        public bool IsWithinTolerance { get; set; }

        /// <summary>Текстовое описание статуса</summary>
        public string Status { get; set; } = "Нет данных";
    }
}
