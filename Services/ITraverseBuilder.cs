using System.Collections.Generic;
using Nivtropy.Domain.DTOs;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Интерфейс построителя структуры хода из записей измерений.
    /// Legacy сервис - будет заменён на Domain сервисы.
    /// </summary>
    public interface ITraverseBuilder
    {
        List<StationDto> Build(IEnumerable<MeasurementRecord> records, RunSummaryDto? run = null);
        void InvalidateCache();
    }
}
