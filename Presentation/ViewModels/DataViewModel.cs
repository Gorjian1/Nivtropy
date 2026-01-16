using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Nivtropy.Presentation.Models;
using Nivtropy.Models;
using Nivtropy.Infrastructure.Parsers;
using Nivtropy.Application.Services;
using Nivtropy.Domain.DTOs;
using Nivtropy.Application.DTOs;
using Nivtropy.Presentation.Mappers;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels
{
    public class DataViewModel : ViewModelBase
    {
        private readonly IDataParser _parser;
        private readonly IRunAnnotationService _annotationService;
        private readonly IImportValidationService? _validationService;

        public ObservableCollection<MeasurementRecord> Records { get; } = new();
        public ObservableCollection<LineSummary> Runs { get; } = new();

        public int RecordsVersion { get; private set; }
        public int KnownHeightsVersion { get; private set; }

        private readonly Dictionary<string, double> _knownHeights = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _sharedPointStates = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler? BeginBatchUpdate;
        public event EventHandler? EndBatchUpdate;

        private string? _sourcePath;
        public string? SourcePath
        {
            get => _sourcePath;
            private set
            {
                if (SetField(ref _sourcePath, value))
                    OnPropertyChanged(nameof(FileName));
            }
        }

        public string? FileName => string.IsNullOrWhiteSpace(SourcePath) ? null : Path.GetFileName(SourcePath);

        private LineSummary? _selectedRun;
        public LineSummary? SelectedRun
        {
            get => _selectedRun;
            set => SetField(ref _selectedRun, value);
        }

        /// <summary>
        /// Результат последней валидации импортированных данных
        /// </summary>
        public ValidationResult? LastValidationResult { get; private set; }

        public DataViewModel(IDataParser parser, IRunAnnotationService annotationService)
            : this(parser, annotationService, null)
        {
        }

        public DataViewModel(
            IDataParser parser,
            IRunAnnotationService annotationService,
            IImportValidationService? validationService)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _annotationService = annotationService ?? throw new ArgumentNullException(nameof(annotationService));
            _validationService = validationService;
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
            LastValidationResult = null;

            // Уведомляем о начале массового обновления
            BeginBatchUpdate?.Invoke(this, EventArgs.Empty);

            try
            {
                Records.Clear();
                _knownHeights.Clear();
                IncrementKnownHeightsVersion();

                var parsed = _parser.Parse(path).ToList();

                // Валидируем данные если сервис доступен
                if (_validationService != null)
                {
                    LastValidationResult = _validationService.Validate(parsed);
                    OnPropertyChanged(nameof(LastValidationResult));
                }

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

            var groups = _annotationService.AnnotateRuns(records);
            foreach (var group in groups)
            {
                var summary = group.Summary.ToModel();
                Runs.Add(summary);
                foreach (var record in group.Records)
                {
                    record.LineSummary = summary;
                }
            }
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
