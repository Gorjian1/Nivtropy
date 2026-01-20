using System.Collections.Generic;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Export;

/// <summary>
/// Порт для генерации экспортного контента по ходам.
/// </summary>
public interface ITraverseExportService
{
    /// <summary>
    /// Генерирует CSV содержимое для списка станций.
    /// </summary>
    string BuildCsv(IEnumerable<StationDto> rows);
}
