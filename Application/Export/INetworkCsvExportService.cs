using Nivtropy.Domain.Model;

namespace Nivtropy.Application.Export;

public interface INetworkCsvExportService
{
    string BuildCsv(LevelingNetwork network);
}
