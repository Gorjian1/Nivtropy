using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Export;

public interface ITraverseExportService
{
    string BuildCsv(IReadOnlyList<StationDto> rows);
}
