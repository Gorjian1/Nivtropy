using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Nivtropy.Models;
using Nivtropy.Services;

namespace Nivtropy.ViewModels
{
    public class DataViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MeasurementRecord> Records { get; } = new();
        public ObservableCollection<LineSummary> Runs { get; } = new();
        public ObservableCollection<QuickStatCard> QuickStats { get; } = new();

        private string? _sourcePath;
        public string? SourcePath
        {
            get => _sourcePath;
            private set
            {
                if (_sourcePath != value)
                {
                    _sourcePath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        public string? FileName => string.IsNullOrWhiteSpace(SourcePath) ? null : Path.GetFileName(SourcePath);

        private int _recordsCount;
        public int RecordsCount
        {
            get => _recordsCount;
            private set => SetField(ref _recordsCount, value);
        }

        private int _validRecordsCount;
        public int ValidRecordsCount
        {
            get => _validRecordsCount;
            private set
            {
                if (SetField(ref _validRecordsCount, value))
                {
                    OnPropertyChanged(nameof(InvalidRecordsCount));
                }
            }
        }

        public int InvalidRecordsCount => RecordsCount - ValidRecordsCount;

        private double? _deltaHSum;
        public double? DeltaHSum
        {
            get => _deltaHSum;
            private set => SetField(ref _deltaHSum, value);
        }

        private double? _averageDeltaH;
        public double? AverageDeltaH
        {
            get => _averageDeltaH;
            private set => SetField(ref _averageDeltaH, value);
        }

        private double? _minDeltaH;
        public double? MinDeltaH
        {
            get => _minDeltaH;
            private set => SetField(ref _minDeltaH, value);
        }

        private double? _maxDeltaH;
        public double? MaxDeltaH
        {
            get => _maxDeltaH;
            private set => SetField(ref _maxDeltaH, value);
        }

        private double? _maxAbsDeltaH;
        public double? MaxAbsDeltaH
        {
            get => _maxAbsDeltaH;
            private set => SetField(ref _maxAbsDeltaH, value);
        }

        private string _expressAnalysis = "Загрузите файл для анализа.";
        public string ExpressAnalysis
        {
            get => _expressAnalysis;
            private set => SetField(ref _expressAnalysis, value);
        }

        private LineSummary? _selectedRun;
        public LineSummary? SelectedRun
        {
            get => _selectedRun;
            set => SetField(ref _selectedRun, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void LoadFromFile(string path)
        {
            SourcePath = path;
            Records.Clear();
            Runs.Clear();
            QuickStats.Clear();
            SelectedRun = null;

            var parser = new DatParser();
            var parsed = parser.Parse(path).ToList();
            AnnotateRuns(parsed);

            foreach (var rec in parsed)
            {
                Records.Add(rec);
            }

            UpdateAnalytics();
        }

        private void AnnotateRuns(IList<MeasurementRecord> records)
        {
            if (records.Count == 0)
                return;

            var groups = new List<List<MeasurementRecord>>();
            var current = new List<MeasurementRecord>();
            MeasurementRecord? previous = null;
            foreach (var record in records)
            {
                if (previous != null && ShouldStartNewLine(previous, record))
                {
                    if (current.Count > 0)
                    {
                        groups.Add(current);
                        current = new List<MeasurementRecord>();
                    }
                }

                current.Add(record);
                previous = record;
            }

            if (current.Count > 0)
            {
                groups.Add(current);
            }

            int index = 1;
            foreach (var group in groups)
            {
                var summary = BuildSummary(index, group);
                Runs.Add(summary);

                var start = group.FirstOrDefault(g => g.Rb_m.HasValue) ?? group.First();
                var end = group.LastOrDefault(g => g.Rf_m.HasValue) ?? group.Last();

                for (int i = 0; i < group.Count; i++)
                {
                    var rec = group[i];
                    rec.LineSummary = summary;
                    rec.ShotIndexWithinLine = i + 1;
                    rec.IsLineStart = ReferenceEquals(rec, start);
                    rec.IsLineEnd = ReferenceEquals(rec, end);
                }

                index++;
            }
        }

        private void UpdateAnalytics()
        {
            RecordsCount = Records.Count;
            ValidRecordsCount = Records.Count(r => r.IsValid);

            var deltas = Records.Select(r => r.DeltaH)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (deltas.Count > 0)
            {
                DeltaHSum = deltas.Sum();
                AverageDeltaH = deltas.Average();
                MinDeltaH = deltas.Min();
                MaxDeltaH = deltas.Max();
                MaxAbsDeltaH = deltas.Max(v => Math.Abs(v));
            }
            else
            {
                DeltaHSum = null;
                AverageDeltaH = null;
                MinDeltaH = null;
                MaxDeltaH = null;
                MaxAbsDeltaH = null;
            }

            BuildQuickStats();
            ExpressAnalysis = BuildExpressAnalysis();
        }

        private void BuildQuickStats()
        {
            QuickStats.Clear();

            if (RecordsCount == 0)
            {
                QuickStats.Add(new QuickStatCard("Данных нет", "—", "Загрузите файл, чтобы увидеть показатели.", "#FF94A3B8"));
                return;
            }

            var runsCount = Runs.Count;
            var avgShotsPerRun = runsCount > 0 ? (double)RecordsCount / runsCount : 0d;
            QuickStats.Add(new QuickStatCard(
                "Ходы",
                runsCount.ToString(CultureInfo.InvariantCulture),
                runsCount > 0
                    ? string.Format(CultureInfo.InvariantCulture, "Средний размер: {0:0.0} отсч.", avgShotsPerRun)
                    : "Ходы не определены",
                "#FF6366F1"));

            var validPercent = RecordsCount > 0 ? (double)ValidRecordsCount / RecordsCount : 0d;
            QuickStats.Add(new QuickStatCard(
                "Отсчёты",
                RecordsCount.ToString(CultureInfo.InvariantCulture),
                string.Format(CultureInfo.InvariantCulture, "Валидных: {0} ({1:P0})", ValidRecordsCount, validPercent),
                "#FF0EA5E9"));

            var rangeText = (MinDeltaH.HasValue && MaxDeltaH.HasValue)
                ? string.Format(CultureInfo.InvariantCulture, "Диапазон: {0:+0.0000;-0.0000;0.0000}…{1:+0.0000;-0.0000;0.0000} м", MinDeltaH.Value, MaxDeltaH.Value)
                : "Диапазон: —";
            var deltaValue = DeltaHSum.HasValue ? $"{FormatSigned(DeltaHSum)} м" : "—";
            QuickStats.Add(new QuickStatCard(
                "ΣΔh",
                deltaValue,
                string.Format(CultureInfo.InvariantCulture, "Средняя Δh: {0} м. {1}", FormatSigned(AverageDeltaH), rangeText),
                "#FFF97316"));

            var maxDeltaValue = MaxAbsDeltaH.HasValue ? $"{FormatAbs(MaxAbsDeltaH)} м" : "—";
            QuickStats.Add(new QuickStatCard(
                "|Δh|max",
                maxDeltaValue,
                DescribeMaxAbsDelta(),
                "#FF22C55E"));
        }

        private string BuildExpressAnalysis()
        {
            if (RecordsCount == 0)
                return "Данных для анализа пока нет.";

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Файл содержит {0} ход(ов) и {1} отсчётов.", Runs.Count, RecordsCount);

            if (RecordsCount > 0)
            {
                var validPercent = (double)ValidRecordsCount / RecordsCount;
                sb.AppendFormat(CultureInfo.InvariantCulture, " Валидность измерений: {0} ({1:P0}).", ValidRecordsCount, validPercent);
            }

            if (DeltaHSum.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " ΣΔh = {0} м", FormatSigned(DeltaHSum));
                if (AverageDeltaH.HasValue)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ", средняя Δh = {0} м", FormatSigned(AverageDeltaH));
                }
                sb.Append('.');
            }

            if (MaxAbsDeltaH.HasValue)
            {
                sb.Append(' ');
                sb.Append(DescribeMaxAbsDelta());
            }

            var longestRun = Runs.OrderByDescending(r => r.RecordCount).FirstOrDefault();
            if (longestRun != null)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " Самый длинный ход: {0} ({1} отсч.).", longestRun.DisplayName, longestRun.RecordCount);
            }

            return sb.ToString();
        }

        private string DescribeMaxAbsDelta()
        {
            if (!MaxAbsDeltaH.HasValue)
                return "Разброс Δh пока не определён.";

            var abs = MaxAbsDeltaH.Value;
            var verdict = abs switch
            {
                < 0.003 => "Очень стабильные измерения.",
                < 0.007 => "Хорошая согласованность отсчётов.",
                < 0.012 => "Разброс в пределах нормы.",
                _ => "Разброс превышает норму — проверьте исходные данные."
            };

            return string.Format(CultureInfo.InvariantCulture, "Максимальная |Δh| = {0} м. {1}", FormatAbs(MaxAbsDeltaH), verdict);
        }

        private static string FormatSigned(double? value)
        {
            return value.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:+0.0000;-0.0000;0.0000}", value.Value)
                : "—";
        }

        private static string FormatAbs(double? value)
        {
            return value.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0000}", Math.Abs(value.Value))
                : "—";
        }

        private static bool ShouldStartNewLine(MeasurementRecord previous, MeasurementRecord current)
        {
            if (current.Seq.HasValue && previous.Seq.HasValue)
            {
                if (current.Seq.Value <= previous.Seq.Value)
                    return true;

                if (current.Seq.Value - previous.Seq.Value > 50)
                    return true;
            }

            if (!string.Equals(previous.Mode, current.Mode, StringComparison.OrdinalIgnoreCase))
            {
                if (current.Mode != null && current.Mode.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (int.TryParse(previous.StationCode, out var prevStation) && int.TryParse(current.StationCode, out var curStation))
            {
                if (curStation < prevStation)
                    return true;
            }

            if (!string.IsNullOrWhiteSpace(previous.Target) && !string.IsNullOrWhiteSpace(current.Target))
            {
                if (string.Equals(previous.Target.Trim(), current.Target.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (previous.Rf_m.HasValue && !current.Rb_m.HasValue)
                        return true;
                }
            }

            return false;
        }

        private static LineSummary BuildSummary(int index, IReadOnlyList<MeasurementRecord> group)
        {
            var start = group.FirstOrDefault(r => r.Rb_m.HasValue) ?? group.First();
            var end = group.LastOrDefault(r => r.Rf_m.HasValue) ?? group.Last();

            double? deltaSum = null;
            foreach (var rec in group)
            {
                if (rec.DeltaH.HasValue)
                {
                    deltaSum = (deltaSum ?? 0d) + rec.DeltaH.Value;
                }
            }

            return new LineSummary(index, start.Target, start.StationCode, end.Target, end.StationCode, group.Count, deltaSum);
        }

        public void ExportCsv(string path)
        {
            using var w = new StreamWriter(path, false, new UTF8Encoding(true));
            w.WriteLine("Seq,Mode,Target,StationCode,Rb_m,Rf_m,HD_m,Z_m,DeltaH,IsValid,Line,ShotIndex");
            foreach (var r in Records)
            {
                string Line(object? v) => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
                w.WriteLine(string.Join(',', new[]
                {
                    Line(r.Seq), r.Mode, r.Target, r.StationCode,
                    Line(r.Rb_m), Line(r.Rf_m), Line(r.HD_m), Line(r.Z_m),
                    Line(r.DeltaH), Line(r.IsValid),
                    r.LineSummary?.DisplayName, Line(r.ShotIndexWithinLine)
                }));
            }
        }
    }
}
