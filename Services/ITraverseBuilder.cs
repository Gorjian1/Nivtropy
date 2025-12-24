using System.Collections.Generic;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Интерфейс построителя структуры хода из записей измерений
    /// </summary>
    public interface ITraverseBuilder
    {
        /// <summary>
        /// Строит структуру хода из записей измерений
        /// </summary>
        /// <param name="records">Записи измерений</param>
        /// <param name="run">Опциональная информация о ходе</param>
        /// <returns>Список строк хода</returns>
        List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null);

        /// <summary>
        /// Очищает кэш результатов построения.
        /// Вызывайте при изменении исходных данных.
        /// </summary>
        void InvalidateCache();
    }
}
