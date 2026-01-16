using System.Collections.Generic;
using Nivtropy.Models;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Интерфейс построителя структуры хода из записей измерений.
    /// Legacy сервис - будет заменён на Domain сервисы.
    /// </summary>
    public interface ITraverseBuilder
    {
        List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null);
        void InvalidateCache();
    }
}
