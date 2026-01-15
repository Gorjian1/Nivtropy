namespace Nivtropy.Legacy.Adapters;

using Nivtropy.Domain.Model;
using Nivtropy.Models;

/// <summary>
/// Адаптер для получения данных в старом формате из LevelingNetwork.
/// Используется ViewModels для обратной совместимости с UI.
/// </summary>
public class NetworkAdapter
{
    private readonly LevelingNetwork _network;

    public NetworkAdapter(LevelingNetwork network)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
    }

    /// <summary>Получить все TraverseRows для отображения</summary>
    public List<TraverseRow> GetAllTraverseRows()
    {
        return _network.Runs
            .SelectMany(TraverseRowAdapter.ToTraverseRows)
            .ToList();
    }

    /// <summary>Получить TraverseRows для конкретного хода</summary>
    public List<TraverseRow> GetTraverseRowsForRun(Guid runId)
    {
        var run = _network.Runs.FirstOrDefault(r => r.Id == runId);
        return run != null ? TraverseRowAdapter.ToTraverseRows(run) : new List<TraverseRow>();
    }

    /// <summary>Получить все LineSummaries</summary>
    public List<LineSummary> GetLineSummaries()
    {
        return LineSummaryAdapter.ToLineSummaries(_network);
    }

    /// <summary>Получить словарь известных высот</summary>
    public Dictionary<string, double> GetKnownHeights()
    {
        return _network.Benchmarks
            .Where(p => p.Height.IsKnown)
            .ToDictionary(p => p.Code.Value, p => p.Height.Value);
    }

    /// <summary>Получить коды общих точек</summary>
    public List<string> GetSharedPointCodes()
    {
        return _network.SharedPoints
            .Select(p => p.Code.Value)
            .ToList();
    }

    /// <summary>Получить все точки с информацией о ходах</summary>
    public Dictionary<string, List<string>> GetPointToRunsMapping()
    {
        return _network.Points.Values
            .Where(p => p.Degree > 0)
            .ToDictionary(
                p => p.Code.Value,
                p => p.ConnectedRuns.Select(r => r.Name).ToList()
            );
    }

    /// <summary>Получить ход по ID</summary>
    public Run? GetRun(Guid runId)
    {
        return _network.Runs.FirstOrDefault(r => r.Id == runId);
    }

    /// <summary>Получить точку по коду</summary>
    public Point? GetPoint(string code)
    {
        return _network.GetPoint(new Domain.ValueObjects.PointCode(code));
    }
}
