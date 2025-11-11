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

        // Словарь известных высот точек: ключ - код точки, значение - высота
        private readonly Dictionary<string, double> _knownHeights = new(StringComparer.OrdinalIgnoreCase);

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
            _knownHeights.Clear();

            var parser = new DatParser();
            var parsed = parser.Parse(path).ToList();

            // Фильтрация теперь выполняется в парсере
            AnnotateRuns(parsed);

            foreach (var rec in parsed)
            {
                Records.Add(rec);
            }
        }

        /// <summary>
        /// Устанавливает известную высоту для точки
        /// </summary>
        public void SetKnownHeight(string pointCode, double height)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            _knownHeights[pointCode.Trim()] = height;
            OnPropertyChanged(nameof(KnownHeights));
        }

        /// <summary>
        /// Получает известную высоту точки, если она установлена
        /// </summary>
        public double? GetKnownHeight(string pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return null;

            return _knownHeights.TryGetValue(pointCode.Trim(), out var height) ? height : null;
        }

        /// <summary>
        /// Удаляет известную высоту точки
        /// </summary>
        public void ClearKnownHeight(string pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            _knownHeights.Remove(pointCode.Trim());
            OnPropertyChanged(nameof(KnownHeights));
        }

        /// <summary>
        /// Проверяет, установлена ли известная высота для точки
        /// </summary>
        public bool HasKnownHeight(string pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return false;

            return _knownHeights.ContainsKey(pointCode.Trim());
        }

        /// <summary>
        /// Словарь всех известных высот (только для чтения)
        /// </summary>
        public IReadOnlyDictionary<string, double> KnownHeights => _knownHeights;

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

        /// <summary>
        /// Определяет, нужно ли начать новый ход на основе маркеров Start-Line/Cont-Line
        /// </summary>
        private static bool ShouldStartNewLine(MeasurementRecord previous, MeasurementRecord current)
        {
            // Start-Line всегда начинает новый ход
            if (current.LineMarker == "Start-Line")
                return true;

            // Cont-Line НИКОГДА не начинает новый ход (продолжение текущего)
            if (current.LineMarker == "Cont-Line")
                return false;

            // End-Line сам по себе не начинает новый ход
            // (следующая запись после End-Line может быть Start-Line или Cont-Line)
            if (current.LineMarker == "End-Line")
                return false;

            // Для записей без маркеров используем эвристики (обратная совместимость)
            // Это нужно для старых файлов без явных маркеров
            if (current.LineMarker == null && previous.LineMarker == null)
            {
                // Большой разрыв в последовательности номеров
                if (current.Seq.HasValue && previous.Seq.HasValue)
                {
                    if (current.Seq.Value - previous.Seq.Value > 50)
                        return true;
                }

                // Если Mode содержит "line" - возможно начало хода
                if (current.Mode != null && current.Mode.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!string.Equals(previous.Mode, current.Mode, StringComparison.OrdinalIgnoreCase))
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
