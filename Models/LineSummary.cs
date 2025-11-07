using System;

namespace Nivtropy.Models
{
    public class LineSummary
    {
        public LineSummary(int index, string? startTarget, string? startStation, string? endTarget, string? endStation, int recordCount, double? deltaHSum)
        {
            Index = index;
            StartTarget = startTarget;
            StartStation = startStation;
            EndTarget = endTarget;
            EndStation = endStation;
            RecordCount = recordCount;
            DeltaHSum = deltaHSum;
        }

        public int Index { get; }
        public string? StartTarget { get; }
        public string? StartStation { get; }
        public string? EndTarget { get; }
        public string? EndStation { get; }
        public int RecordCount { get; }
        public double? DeltaHSum { get; }

        private static string FormatPoint(string? target, string? station)
        {
            if (!string.IsNullOrWhiteSpace(target)) return target.Trim();
            if (!string.IsNullOrWhiteSpace(station)) return station.Trim();
            return "—";
        }

        public string StartLabel => FormatPoint(StartTarget, StartStation);
        public string EndLabel => FormatPoint(EndTarget, EndStation);

        public string DisplayName => $"Ход {Index:D2}";
        public string RangeDisplay => $"{StartLabel} → {EndLabel}";
        public string Header => $"{DisplayName}: {RangeDisplay}";

        public string CombinedStats
        {
            get
            {
                var delta = DeltaHSum.HasValue
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "ΣΔh = {0:+0.0000;-0.0000;0.0000} м", DeltaHSum.Value)
                    : "ΣΔh = —";
                return $"Отсчётов: {RecordCount}, {delta}";
            }
        }

        public string Tooltip => $"{Header}\n{CombinedStats}";
    }
}
