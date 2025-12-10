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
        /// <returns>Список строк хода</returns>
        List<TraverseRow> Build(IEnumerable<MeasurementRecord> records);
    }
}
