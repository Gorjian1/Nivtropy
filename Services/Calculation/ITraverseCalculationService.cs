using System.Collections.Generic;
using Nivtropy.Models;
using Nivtropy.ViewModels;

namespace Nivtropy.Services.Calculation
{
    /// <summary>
    /// Сервис для выполнения расчётов нивелирного хода.
    /// Извлечён из TraverseCalculationViewModel для соблюдения SRP.
    /// Все методы stateless - принимают данные как параметры.
    /// </summary>
    public interface ITraverseCalculationService
    {
        /// <summary>
        /// Рассчитывает высоты точек на основе известных высот и превышений
        /// </summary>
        /// <param name="rows">Строки нивелирного хода</param>
        /// <param name="knownHeights">Словарь известных высот (код точки -> высота)</param>
        /// <returns>Обновлённые строки с рассчитанными высотами</returns>
        IList<TraverseRow> CalculateHeights(IList<TraverseRow> rows, IDictionary<string, double> knownHeights);

        /// <summary>
        /// Распределяет невязку по превышениям пропорционально длинам станций
        /// </summary>
        /// <param name="rows">Строки нивелирного хода</param>
        /// <param name="closure">Невязка для распределения</param>
        /// <returns>Обновлённые строки с поправками</returns>
        IList<TraverseRow> DistributeClosure(IList<TraverseRow> rows, double closure);

        /// <summary>
        /// Рассчитывает невязку хода
        /// </summary>
        /// <param name="rows">Строки нивелирного хода</param>
        /// <param name="startHeight">Начальная высота</param>
        /// <param name="endHeight">Конечная высота (если известна)</param>
        /// <returns>Невязка в метрах или null если расчёт невозможен</returns>
        double? CalculateClosure(IList<TraverseRow> rows, double? startHeight, double? endHeight);

        /// <summary>
        /// Рассчитывает допустимую невязку по заданному классу нивелирования
        /// </summary>
        /// <param name="totalLength">Общая длина хода в метрах</param>
        /// <param name="stationCount">Количество станций</param>
        /// <param name="toleranceMode">Режим расчёта допуска</param>
        /// <param name="coefficient">Коэффициент допуска</param>
        /// <returns>Допустимая невязка в метрах</returns>
        double CalculateAllowableClosure(double totalLength, int stationCount, ToleranceMode toleranceMode, double coefficient);

        /// <summary>
        /// Применяет поправки к превышениям
        /// </summary>
        /// <param name="rows">Строки нивелирного хода</param>
        /// <returns>Строки с применёнными поправками</returns>
        IList<TraverseRow> ApplyCorrections(IList<TraverseRow> rows);
    }
}
