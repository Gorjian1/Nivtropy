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
        private readonly IDataParser _parser;

        public ObservableCollection<MeasurementRecord> Records { get; } = new();
        public ObservableCollection<LineSummary> Runs { get; } = new();

        // Служебные версии для отслеживания изменений данных
        public int RecordsVersion { get; private set; }
        public int KnownHeightsVersion { get; private set; }

        // Словарь известных высот точек: ключ - код точки, значение - высота
        private readonly Dictionary<string, double> _knownHeights = new(StringComparer.OrdinalIgnoreCase);

        // Состояние общих точек: включен/выключен обмен
        private readonly Dictionary<string, bool> _sharedPointStates = new(StringComparer.OrdinalIgnoreCase);

        // События для оптимизации массовых обновлений
        public event EventHandler? BeginBatchUpdate;
        public event EventHandler? EndBatchUpdate;

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

        public DataViewModel(IDataParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            Records.CollectionChanged += (_, __) => IncrementRecordsVersion();
        }

        private void IncrementRecordsVersion()
        {
            RecordsVersion++;
            OnPropertyChanged(nameof(RecordsVersion));
        }

        private void IncrementKnownHeightsVersion()
        {
            KnownHeightsVersion++;
            OnPropertyChanged(nameof(KnownHeightsVersion));
        }

        public void LoadFromFile(string path)
        {
            SourcePath = path;

            // Уведомляем о начале массового обновления
            BeginBatchUpdate?.Invoke(this, EventArgs.Empty);

            try
            {
                Records.Clear();
                _knownHeights.Clear();
                IncrementKnownHeightsVersion();

                var parsed = _parser.Parse(path).ToList();

                // Фильтрация теперь выполняется в парсере
                AnnotateRuns(parsed);

                foreach (var rec in parsed)
                {
                    Records.Add(rec);
                }
            }
            finally
            {
                // Уведомляем о завершении массового обновления
                EndBatchUpdate?.Invoke(this, EventArgs.Empty);
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
            IncrementKnownHeightsVersion();
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
            IncrementKnownHeightsVersion();
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

        /// <summary>
        /// Состояние общих точек (для управления обменом высот между ходами)
        /// </summary>
        public IReadOnlyDictionary<string, bool> SharedPointStates => _sharedPointStates;

        public bool IsSharedPointEnabled(string? pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return true;

            return !_sharedPointStates.TryGetValue(pointCode.Trim(), out var enabled) || enabled;
        }

        public void SetSharedPointEnabled(string? pointCode, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            var code = pointCode.Trim();
            var wasEnabled = IsSharedPointEnabled(code);

            _sharedPointStates[code] = isEnabled;

            // Если точка отвязывается (была связана, стала отвязанной)
            if (wasEnabled && !isEnabled)
            {
                CreateDisconnectedPointCopies(code);
            }

            OnPropertyChanged(nameof(SharedPointStates));
        }

        /// <summary>
        /// Создаёт копии точки для каждого хода при отвязке
        /// </summary>
        private void CreateDisconnectedPointCopies(string pointCode)
        {
            // Находим все ходы где используется эта точка через LineSummary
            var runsWithPoint = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in Records)
            {
                // Проверяем Target (целевая точка) и StationCode (код станции)
                var recordPointCode = record.Target ?? record.StationCode;

                if (string.Equals(recordPointCode, pointCode, StringComparison.OrdinalIgnoreCase))
                {
                    var runName = record.LineSummary?.DisplayName;
                    if (!string.IsNullOrWhiteSpace(runName))
                        runsWithPoint.Add(runName);
                }
            }

            // Получаем текущую высоту точки (если есть)
            var existingHeight = GetKnownHeight(pointCode);

            // Создаём копию для каждого хода
            bool isFirst = true;
            foreach (var runName in runsWithPoint.OrderBy(r => r))
            {
                var pointCodeWithRun = GetPointCodeForRun(pointCode, runName);

                // Если для первого хода есть высота - копируем её
                // Для остальных создаём без высоты (они появятся в списке для установки)
                if (existingHeight.HasValue && isFirst && !HasKnownHeight(pointCodeWithRun))
                {
                    SetKnownHeight(pointCodeWithRun, existingHeight.Value);
                    isFirst = false;
                }
            }
        }

        /// <summary>
        /// Получает код точки с суффиксом хода
        /// </summary>
        public string GetPointCodeForRun(string pointCode, string runName)
        {
            return $"{pointCode} ({runName})";
        }

        /// <summary>
        /// Получает известную высоту точки с учётом отвязки
        /// Если точка отвязана - ищет версию с суффиксом хода
        /// </summary>
        public double? GetKnownHeightForRun(string pointCode, string? runName)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return null;

            var code = pointCode.Trim();

            // Если точка отвязана и указан ход - ищем версию с суффиксом
            if (!IsSharedPointEnabled(code) && !string.IsNullOrWhiteSpace(runName))
            {
                var codeWithRun = GetPointCodeForRun(code, runName);
                var heightWithRun = GetKnownHeight(codeWithRun);
                if (heightWithRun.HasValue)
                    return heightWithRun;
            }

            // Иначе возвращаем обычную высоту
            return GetKnownHeight(code);
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

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                int index = g + 1;

                var summary = BuildSummary(index, group);
                Runs.Add(summary);

                var start = group.FirstOrDefault(gr => gr.Rb_m.HasValue) ?? group.First();
                var end = group.LastOrDefault(gr => gr.Rf_m.HasValue) ?? group.Last();

                for (int i = 0; i < group.Count; i++)
                {
                    var rec = group[i];
                    rec.LineSummary = summary;
                    rec.ShotIndexWithinLine = i + 1;
                    rec.IsLineStart = ReferenceEquals(rec, start);
                    rec.IsLineEnd = ReferenceEquals(rec, end);
                }
            }
        }

        /// <summary>
        /// Определяет, нужно ли начать новый ход на основе маркеров Start-Line
        /// </summary>
        private static bool ShouldStartNewLine(MeasurementRecord previous, MeasurementRecord current)
        {
            // Start-Line всегда начинает новый ход
            if (current.LineMarker == "Start-Line")
                return true;

            // Cont-Line НЕ начинает новый ход - это продолжение текущего хода
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

            // Ищем оригинальный номер хода из Start-Line записи
            var startLineRecord = group.FirstOrDefault(r => r.LineMarker == "Start-Line");
            var originalLineNumber = startLineRecord?.OriginalLineNumber;

            double? deltaSum = null;
            double? totalDistanceBack = null;
            double? totalDistanceFore = null;
            double? armDiffAccumulation = null;

            foreach (var rec in group)
            {
                if (rec.DeltaH.HasValue)
                {
                    deltaSum = (deltaSum ?? 0d) + rec.DeltaH.Value;
                }

                if (rec.HdBack_m.HasValue)
                {
                    totalDistanceBack = (totalDistanceBack ?? 0d) + rec.HdBack_m.Value;
                }

                if (rec.HdFore_m.HasValue)
                {
                    totalDistanceFore = (totalDistanceFore ?? 0d) + rec.HdFore_m.Value;
                }

                // Накопление разности плеч (относительное значение с учетом знака)
                if (rec.HdBack_m.HasValue && rec.HdFore_m.HasValue)
                {
                    var armDiff = rec.HdBack_m.Value - rec.HdFore_m.Value;
                    armDiffAccumulation = (armDiffAccumulation ?? 0d) + armDiff;
                }
            }

            return new LineSummary(
                index,
                start.Target,
                start.StationCode,
                end.Target,
                end.StationCode,
                group.Count,
                deltaSum,
                totalDistanceBack,
                totalDistanceFore,
                armDiffAccumulation,
                originalLineNumber: originalLineNumber);
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
