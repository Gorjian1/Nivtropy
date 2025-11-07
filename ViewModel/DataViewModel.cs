using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

        public DataViewModel()
        {
            Records.CollectionChanged += OnCollectionChanged;
            Runs.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotifications)
            {
                RefreshComputedProperties();
            }
        }

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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void LoadFromFile(string path)
        {
            SourcePath = path;
            _suppressNotifications = true;
            Records.Clear();

            var parser = new DatParser();
            var parsed = parser.Parse(path).ToList();
            AnnotateRuns(parsed);

            foreach (var rec in parsed)
            {
                Records.Add(rec);
            }

            _suppressNotifications = false;
            RefreshComputedProperties();
        }

        private void AnnotateRuns(IList<MeasurementRecord> records)
        {
            Runs.Clear();
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

        public int TotalRuns => Runs.Count;
        public int TotalRecords => Records.Count;
        public int ValidRecords => Records.Count(r => r.IsValid);
        public int InvalidRecords => TotalRecords - ValidRecords;
        public int PairShotsCount => Records.Count(r => r.Rb_m.HasValue && r.Rf_m.HasValue);
        public int SingleShotsCount => Records.Count(r => r.Rb_m.HasValue ^ r.Rf_m.HasValue);

        public double? DeltaHSum
        {
            get
            {
                var values = Records.Where(r => r.DeltaH.HasValue).Select(r => r.DeltaH!.Value).ToList();
                if (values.Count == 0)
                    return null;
                return values.Sum();
            }
        }

        public double? DeltaHMin
        {
            get
            {
                var values = Records.Where(r => r.DeltaH.HasValue).Select(r => r.DeltaH!.Value).ToList();
                return values.Count > 0 ? values.Min() : null;
            }
        }

        public double? DeltaHMax
        {
            get
            {
                var values = Records.Where(r => r.DeltaH.HasValue).Select(r => r.DeltaH!.Value).ToList();
                return values.Count > 0 ? values.Max() : null;
            }
        }

        public double? DeltaHAbsMax
        {
            get
            {
                var values = Records.Where(r => r.DeltaH.HasValue).Select(r => Math.Abs(r.DeltaH!.Value)).ToList();
                return values.Count > 0 ? values.Max() : null;
            }
        }

        public string? LongestRunHeader => GetLongestRun()?.Header;
        public string? LongestRunRange => GetLongestRun()?.RangeDisplay;

        private string? _requestedLineHeader;
        public string? RequestedLineHeader
        {
            get => _requestedLineHeader;
            set
            {
                _requestedLineHeader = value;
                OnPropertyChanged();
            }
        }

        public string ExpressAnalysis
        {
            get
            {
                if (TotalRecords == 0)
                    return "Загрузите файл, чтобы увидеть экспресс-анализ.";

                var sb = new StringBuilder();
                if (DeltaHAbsMax.HasValue)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "Макс |Δh| {0:0.0000} м", DeltaHAbsMax.Value);
                }
                else
                {
                    sb.Append("Невязки отсутствуют");
                }

                if (InvalidRecords > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ", пустых отсчётов: {0}", InvalidRecords);
                }
                else
                {
                    sb.Append(", все отсчёты заполнены");
                }

                var dominantRun = GetLongestRun();
                if (dominantRun != null)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ", {0} ({1} отсчётов)", dominantRun.Header, dominantRun.RecordCount);
                }

                if (sb.Length > 0 && sb[sb.Length - 1] != '.')
                {
                    sb.Append('.');
                }

                return sb.ToString();
            }
        }

        private void RefreshComputedProperties()
        {
            OnPropertyChanged(nameof(TotalRuns));
            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(ValidRecords));
            OnPropertyChanged(nameof(InvalidRecords));
            OnPropertyChanged(nameof(PairShotsCount));
            OnPropertyChanged(nameof(SingleShotsCount));
            OnPropertyChanged(nameof(DeltaHSum));
            OnPropertyChanged(nameof(DeltaHMin));
            OnPropertyChanged(nameof(DeltaHMax));
            OnPropertyChanged(nameof(DeltaHAbsMax));
            OnPropertyChanged(nameof(LongestRunHeader));
            OnPropertyChanged(nameof(LongestRunRange));
            OnPropertyChanged(nameof(ExpressAnalysis));
        }

        private bool _suppressNotifications;

        private LineSummary? GetLongestRun() => Runs.OrderByDescending(r => r.RecordCount).FirstOrDefault();
    }
}
