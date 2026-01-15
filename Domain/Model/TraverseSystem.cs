namespace Nivtropy.Domain.Model;

/// <summary>
/// Система ходов - логическая группировка ходов.
/// </summary>
public class TraverseSystem
{
    private readonly List<Run> _runs = new();

    public Guid Id { get; }
    public string Name { get; private set; }
    public int Order { get; set; }

    public IReadOnlyList<Run> Runs => _runs;
    public int RunCount => _runs.Count;

    public string DisplayName => $"{Name} ({RunCount})";

    public TraverseSystem(string name, int order = 0)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Order = order;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        Name = newName;
    }

    internal void AddRun(Run run)
    {
        if (!_runs.Contains(run))
        {
            _runs.Add(run);
            run.System = this;
        }
    }

    internal void RemoveRun(Run run)
    {
        if (_runs.Remove(run))
        {
            run.System = null;
        }
    }

    public override string ToString() => DisplayName;
}
