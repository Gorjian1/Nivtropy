namespace Nivtropy.Presentation.Models
{
    /// <summary>
    /// Представляет точку с информацией о принадлежности к ходу.
    /// UI-модель для отображения при выборе точки для установки высоты.
    /// </summary>
    public class PointItem
    {
        public PointItem(string code, string? lineName, int lineIndex)
        {
            Code = code;
            LineName = lineName;
            LineIndex = lineIndex;
        }

        public string Code { get; }
        public string? LineName { get; }
        public int LineIndex { get; }
        public string DisplayName => $"{Code} ({LineName})";

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Представляет репер (точку с известной высотой).
    /// UI-модель для отображения в списке реперов.
    /// </summary>
    public class BenchmarkItem
    {
        public BenchmarkItem(string code, double height, string? systemId = null)
        {
            Code = code;
            Height = height;
            SystemId = systemId;
        }

        public string Code { get; }
        public double Height { get; }
        public string? SystemId { get; }
        public string DisplayName => $"{Code}: {Height:F4} м";

        public override string ToString() => DisplayName;
    }
}
