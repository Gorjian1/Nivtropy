namespace Nivtropy.Models
{
    /// <summary>
    /// Представляет точку с информацией о принадлежности к ходу
    /// Используется для отображения в UI при выборе точки для установки высоты
    /// </summary>
    public class PointItem
    {
        public PointItem(string code, string lineName, int lineIndex)
        {
            Code = code;
            LineName = lineName;
            LineIndex = lineIndex;
        }

        /// <summary>
        /// Код точки (например, "21", "1000", "12")
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Название хода, в котором встречается точка (например, "Ход 01")
        /// </summary>
        public string LineName { get; }

        /// <summary>
        /// Индекс хода (для группировки и сортировки)
        /// </summary>
        public int LineIndex { get; }

        /// <summary>
        /// Отображаемое имя точки в формате "21 (Ход 01)"
        /// </summary>
        public string DisplayName => $"{Code} ({LineName})";

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Представляет репер (точку с известной высотой)
    /// </summary>
    public class BenchmarkItem
    {
        public BenchmarkItem(KnownHeightEntry entry)
        {
            Entry = entry;
        }

        public KnownHeightEntry Entry { get; }

        public string Code => Entry.PointCode;

        public double Height => Entry.Height;

        public string DisplayName => Entry.DisplayName;

        public override string ToString() => DisplayName;
    }
}
