using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using Nivtropy.Application.Commands;
using Nivtropy.Application.Commands.Handlers;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
using Nivtropy.Application.Export;
using Nivtropy.Application.Queries;
using Nivtropy.Application.Persistence;
using Nivtropy.Application.Services;
using Nivtropy.Constants;
using Nivtropy.Domain.Model;
using Nivtropy.Domain.Services;
using Nivtropy.Domain.ValueObjects;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.Services;
using Nivtropy.Presentation.ViewModels.Base;
using Nivtropy.Presentation.ViewModels.Managers;
using Nivtropy.Utilities;
using TraverseSystem = Nivtropy.Presentation.Models.TraverseSystem;

namespace Nivtropy.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel для работы с нивелирной сетью через архитектуру DDD.
    /// </summary>
    public class NetworkViewModel : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly BuildNetworkHandler _buildNetworkHandler;
        private readonly CalculateHeightsHandler _calculateHandler;
        private readonly GetNetworkSummaryHandler _summaryHandler;
        private readonly INetworkRepository _repository;
        private readonly INetworkCsvExportService _exportService;
        private readonly IDialogService _dialogService;
        private readonly ObservableCollection<TraverseRow> _rows = new();
        private readonly ObservableCollection<PointItem> _availablePoints = new();
        private readonly ObservableCollection<BenchmarkItem> _benchmarks = new();
        private readonly ObservableCollection<SharedPointLinkItem> _sharedPoints = new();
        private readonly ObservableCollection<TraverseSystem> _systems = new();
        private readonly Dictionary<string, SharedPointLinkItem> _sharedPointLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _benchmarkSystems = new(StringComparer.OrdinalIgnoreCase);

        private LevelingNetwork? _network;
        private Guid _networkId;
        private NetworkSummaryDto? _summary;
        private AdjustmentMode _adjustmentMode = AdjustmentMode.Local;
        private double? _closure;
        private double? _allowableClosure;
        private string _closureVerdict = "Нет данных для расчёта.";
        private double _totalAverageDistance;
        private int _stationsCount;
        private PointItem? _selectedPoint;
        private string? _newBenchmarkHeight;
        private TraverseSystem? _selectedSystem;
        private CancellationTokenSource? _calculationCts;

        private readonly LevelingMethodOption[] _methods =
        {
            new("BF", "Двойной ход (Back → Forward)", ToleranceMode.SqrtStations, ToleranceCoefficients.DoubleRun, 1.0),
            new("FB", "Двойной ход (Forward → Back)", ToleranceMode.SqrtStations, ToleranceCoefficients.DoubleRun, -1.0)
        };

        private readonly LevelingClassOption[] _classes =
        {
            new("I", "Класс I: 4 мм · √L", ToleranceMode.SqrtLength, ToleranceCoefficients.ClassI, ArmDiffStation: ArmDifferenceLimits.PerStation.ClassI, ArmDiffAccumulation: ArmDifferenceLimits.Accumulation.ClassI),
            new("II", "Класс II: 8 мм · √L", ToleranceMode.SqrtLength, ToleranceCoefficients.ClassII, ArmDiffStation: ArmDifferenceLimits.PerStation.ClassII, ArmDiffAccumulation: ArmDifferenceLimits.Accumulation.ClassII),
            new("III", "Класс III: 10 мм · √L", ToleranceMode.SqrtLength, ToleranceCoefficients.ClassIII, ArmDiffStation: ArmDifferenceLimits.PerStation.ClassIII, ArmDiffAccumulation: ArmDifferenceLimits.Accumulation.ClassIII),
            new("IV", "Класс IV: 20 мм · √L", ToleranceMode.SqrtLength, ToleranceCoefficients.ClassIV, ArmDiffStation: ArmDifferenceLimits.PerStation.ClassIV, ArmDiffAccumulation: ArmDifferenceLimits.Accumulation.ClassIV),
            new("Техническое", "Техническое: 50 мм · √L", ToleranceMode.SqrtLength, ToleranceCoefficients.Technical, ArmDiffStation: ArmDifferenceLimits.PerStation.Technical, ArmDiffAccumulation: ArmDifferenceLimits.Accumulation.Technical)
        };

        private LevelingMethodOption? _selectedMethod;
        private LevelingClassOption? _selectedClass;

        public NetworkViewModel(
            DataViewModel dataViewModel,
            SettingsViewModel settingsViewModel,
            BuildNetworkHandler buildNetworkHandler,
            CalculateHeightsHandler calculateHandler,
            GetNetworkSummaryHandler summaryHandler,
            INetworkRepository repository,
            INetworkCsvExportService exportService,
            IDialogService dialogService)
        {
            _dataViewModel = dataViewModel ?? throw new ArgumentNullException(nameof(dataViewModel));
            _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
            _buildNetworkHandler = buildNetworkHandler ?? throw new ArgumentNullException(nameof(buildNetworkHandler));
            _calculateHandler = calculateHandler ?? throw new ArgumentNullException(nameof(calculateHandler));
            _summaryHandler = summaryHandler ?? throw new ArgumentNullException(nameof(summaryHandler));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += OnRecordsCollectionChanged;
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            SubscribeToBatchUpdates(_dataViewModel);

            _selectedMethod = _methods.FirstOrDefault();
            _selectedClass = _classes.FirstOrDefault();

            var defaultSystem = new TraverseSystem(ITraverseSystemsManager.DEFAULT_SYSTEM_ID, ITraverseSystemsManager.DEFAULT_SYSTEM_NAME, order: 0);
            _systems.Add(defaultSystem);
            _selectedSystem = defaultSystem;

            UpdateNetwork();
        }

        public ObservableCollection<TraverseRow> Rows => _rows;
        public ObservableCollection<LineSummary> Runs => _dataViewModel.Runs;
        public ObservableCollection<PointItem> AvailablePoints => _availablePoints;
        public ObservableCollection<BenchmarkItem> Benchmarks => _benchmarks;
        public ObservableCollection<SharedPointLinkItem> SharedPoints => _sharedPoints;
        public ObservableCollection<TraverseSystem> Systems => _systems;
        public SettingsViewModel Settings => _settingsViewModel;

        public LevelingMethodOption[] Methods => _methods;
        public LevelingClassOption[] Classes => _classes;

        public LevelingMethodOption? SelectedMethod
        {
            get => _selectedMethod;
            set => SetField(ref _selectedMethod, value);
        }

        public LevelingClassOption? SelectedClass
        {
            get => _selectedClass;
            set => SetField(ref _selectedClass, value);
        }

        public AdjustmentMode AdjustmentMode
        {
            get => _adjustmentMode;
            set
            {
                if (SetField(ref _adjustmentMode, value))
                {
                    _ = UpdateNetworkAsync();
                }
            }
        }

        public TraverseSystem? SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (SetField(ref _selectedSystem, value))
                {
                    UpdateAvailablePoints();
                    UpdateBenchmarks();
                }
            }
        }

        public PointItem? SelectedPoint
        {
            get => _selectedPoint;
            set => SetField(ref _selectedPoint, value);
        }

        public string? NewBenchmarkHeight
        {
            get => _newBenchmarkHeight;
            set => SetField(ref _newBenchmarkHeight, value);
        }

        public double? Closure
        {
            get => _closure;
            private set => SetField(ref _closure, value);
        }

        public double? AllowableClosure
        {
            get => _allowableClosure;
            private set => SetField(ref _allowableClosure, value);
        }

        public string ClosureVerdict
        {
            get => _closureVerdict;
            private set => SetField(ref _closureVerdict, value);
        }

        public int StationsCount
        {
            get => _stationsCount;
            private set => SetField(ref _stationsCount, value);
        }

        public double TotalAverageLength
        {
            get => _totalAverageDistance;
            private set => SetField(ref _totalAverageDistance, value);
        }

        public bool IsClosureWithinTolerance => Closure.HasValue && AllowableClosure.HasValue && Math.Abs(Closure.Value) <= AllowableClosure.Value;

        public ICommand ExportCommand => new RelayCommand(_ => ExportToCsv(), _ => _network != null);
        public ICommand AddBenchmarkCommand => new RelayCommand(_ => AddBenchmark(), _ => SelectedPoint != null);
        public ICommand RemoveBenchmarkCommand => new RelayCommand(obj => RemoveBenchmark(obj as BenchmarkItem));

        public void RefreshNetwork() => _ = UpdateNetworkAsync();

        public bool HasKnownHeight(string code) => _dataViewModel.KnownHeights.ContainsKey(code);

        protected override void OnBatchUpdateCompleted()
        {
            _ = UpdateNetworkAsync();
        }

        private void OnRecordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsUpdating)
                return;

            _ = UpdateNetworkAsync();
        }

        private void DataViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun))
            {
                OnPropertyChanged(nameof(SelectedRun));
                UpdateSummaryStatistics(_summary);
            }
            else if (e.PropertyName == nameof(DataViewModel.SharedPointStates))
            {
                _ = UpdateNetworkAsync();
            }
        }

        public LineSummary? SelectedRun
        {
            get => _dataViewModel.SelectedRun;
            set
            {
                if (!ReferenceEquals(_dataViewModel.SelectedRun, value))
                {
                    _dataViewModel.SelectedRun = value;
                    OnPropertyChanged();
                }
            }
        }

        private void AddBenchmark()
        {
            if (SelectedPoint == null)
                return;

            if (!double.TryParse(NewBenchmarkHeight, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                _dialogService.ShowWarning("Введите корректную высоту (число).", "Некорректное значение");
                return;
            }

            _dataViewModel.SetKnownHeight(SelectedPoint.Code, height);
            _benchmarkSystems[SelectedPoint.Code] = SelectedSystem?.Id ?? ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
            UpdateBenchmarks();
            _ = UpdateNetworkAsync();
        }

        private void RemoveBenchmark(BenchmarkItem? benchmark)
        {
            if (benchmark == null)
                return;

            _dataViewModel.ClearKnownHeight(benchmark.Code);
            _benchmarkSystems.Remove(benchmark.Code);
            UpdateBenchmarks();
            _ = UpdateNetworkAsync();
        }

        private async Task UpdateNetworkAsync()
        {
            _calculationCts?.Cancel();
            _calculationCts = new CancellationTokenSource();
            var token = _calculationCts.Token;

            try
            {
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                UpdateNetwork();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        private void UpdateNetwork()
        {
            var records = _dataViewModel.RawRecords;
            if (records.Count == 0)
            {
                ClearCalculationResults();
                return;
            }

            var command = new BuildNetworkCommand(
                records,
                _dataViewModel.KnownHeights,
                _dataViewModel.SharedPointStates,
                _dataViewModel.FileName ?? "Новый проект");
            try
            {
                _networkId = _buildNetworkHandler.HandleAsync(command).GetAwaiter().GetResult();

                _network = _repository.GetByIdAsync(_networkId).GetAwaiter().GetResult();

                if (_network == null)
                {
                    ClearCalculationResults();
                    return;
                }

                ApplyRunActivation(_network);
                _repository.SaveAsync(_network).GetAwaiter().GetResult();

                _calculateHandler.HandleAsync(new CalculateHeightsCommand(_networkId, AdjustmentMode)).GetAwaiter().GetResult();
                _network = _repository.GetByIdAsync(_networkId).GetAwaiter().GetResult();

                if (_network == null)
                {
                    ClearCalculationResults();
                    return;
                }

                UpdateFromNetwork(_network);
            }
            catch (Exception ex)
            {
                ClearCalculationResults();
                _dialogService.ShowError(ex.Message, "Ошибка построения сети");
            }
        }

        private void UpdateFromNetwork(LevelingNetwork network)
        {
            var summary = _summaryHandler.HandleAsync(new GetNetworkSummaryQuery(network.Id)).GetAwaiter().GetResult();
            _summary = summary;

            UpdateRuns(summary);
            InitializeSystemsFromRuns();
            UpdateRows(network, summary);
            UpdateSharedPoints();
            UpdateAvailablePoints();
            UpdateBenchmarks();
            UpdateSummaryStatistics(summary);
        }

        private void UpdateRuns(NetworkSummaryDto? summary)
        {
            if (summary?.Runs == null)
                return;

            var lookup = Runs.ToDictionary(r => r.Index);
            for (int i = 0; i < summary.Runs.Count; i++)
            {
                var runSummary = summary.Runs[i];
                var lineIndex = i + 1;

                if (lookup.TryGetValue(lineIndex, out var existing))
                {
                    ApplyNetworkSummary(existing, runSummary);
                    if (string.IsNullOrWhiteSpace(existing.SystemId))
                    {
                        existing.SystemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
                    }
                }
            }
        }

        private void UpdateRows(LevelingNetwork network, NetworkSummaryDto? summary)
        {
            _rows.Clear();

            var heightCalculator = new HeightSnapshotCalculator();
            var rawHeights = heightCalculator.ComputeHeightsSnapshot(network, useCorrections: false);
            var adjustedHeights = heightCalculator.ComputeHeightsSnapshot(network, useCorrections: true);

            var summaries = Runs.ToList();
            for (int i = 0; i < network.Runs.Count; i++)
            {
                var run = network.Runs[i];
                var lineSummary = summaries.Count > i
                    ? summaries[i]
                    : BuildLineSummary(i + 1, summary?.Runs.ElementAtOrDefault(i));

                foreach (var observation in run.Observations)
                {
                    rawHeights.TryGetValue(observation.From, out var rawBack);
                    rawHeights.TryGetValue(observation.To, out var rawFore);
                    adjustedHeights.TryGetValue(observation.From, out var adjustedBack);
                    adjustedHeights.TryGetValue(observation.To, out var adjustedFore);

                    var row = new TraverseRow
                    {
                        LineName = run.Name,
                        Index = observation.StationIndex,
                        BackCode = observation.From.Code.Value,
                        ForeCode = observation.To.Code.Value,
                        Rb_m = observation.BackReading.Meters,
                        Rf_m = observation.ForeReading.Meters,
                        HdBack_m = observation.BackDistance.Meters,
                        HdFore_m = observation.ForeDistance.Meters,
                        BackHeight = adjustedBack,
                        ForeHeight = adjustedFore,
                        BackHeightZ0 = rawBack,
                        ForeHeightZ0 = rawFore,
                        IsBackHeightKnown = observation.From.Type == PointType.Benchmark,
                        IsForeHeightKnown = observation.To.Type == PointType.Benchmark,
                        Correction = observation.Correction,
                        CorrectionMode = observation.Correction != 0 ? CorrectionDisplayMode.Single : CorrectionDisplayMode.None,
                        LineSummary = lineSummary
                    };

                    _rows.Add(row);
                }
            }
        }

        private void UpdateSummaryStatistics(NetworkSummaryDto? summary)
        {
            StationsCount = summary?.TotalStationCount ?? 0;
            TotalAverageLength = summary?.TotalLengthMeters ?? 0;

            if (summary?.Runs != null && summary.Runs.Count > 0)
            {
                var selectedIndex = SelectedRun?.Index ?? 1;
                var runIndex = Math.Clamp(selectedIndex - 1, 0, summary.Runs.Count - 1);
                var run = summary.Runs[runIndex];
                Closure = run.ClosureValueMm.HasValue ? run.ClosureValueMm.Value / 1000.0 : null;
                AllowableClosure = run.ClosureToleranceMm.HasValue ? run.ClosureToleranceMm.Value / 1000.0 : null;
                ClosureVerdict = Closure.HasValue
                    ? (IsClosureWithinTolerance ? "В пределах допуска." : "Превышение допуска!")
                    : "Нет данных для расчёта.";
            }
            else
            {
                Closure = null;
                AllowableClosure = null;
                ClosureVerdict = "Нет данных для расчёта.";
            }
        }

        private void UpdateSharedPoints()
        {
            _sharedPoints.Clear();
            _sharedPointLookup.Clear();

            var sharedUsage = BuildSharedPointUsage();
            foreach (var (code, runIndexes) in sharedUsage)
            {
                var enabled = _dataViewModel.IsSharedPointEnabled(code);
                var item = new SharedPointLinkItem(code, enabled, (pointCode, state) => _dataViewModel.SetSharedPointEnabled(pointCode, state));
                item.SetRunIndexes(runIndexes);
                _sharedPointLookup[code] = item;
                _sharedPoints.Add(item);
            }
        }

        private Dictionary<string, List<int>> BuildSharedPointUsage()
        {
            if (_network == null)
                return new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            var runIndexLookup = _network.Runs
                .Select((run, index) => new { run, index = index + 1 })
                .ToDictionary(x => x.run, x => x.index);

            return _network.SharedPoints
                .Select(point => new
                {
                    code = point.Code.Value,
                    runIndexes = point.ConnectedRuns
                        .Select(run => runIndexLookup.TryGetValue(run, out var index) ? index : 0)
                        .Where(index => index > 0)
                        .Distinct()
                        .OrderBy(index => index)
                        .ToList()
                })
                .Where(item => item.runIndexes.Count > 1)
                .OrderBy(item => PointCodeHelper.GetSortKey(item.code))
                .ToDictionary(item => item.code, item => item.runIndexes, StringComparer.OrdinalIgnoreCase);
        }

        private void UpdateAvailablePoints()
        {
            _availablePoints.Clear();

            var selectedSystemId = SelectedSystem?.Id;
            var runsInSystem = selectedSystemId != null
                ? Runs.Where(r => r.SystemId == selectedSystemId).Select(r => r.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null;

            var pointsDict = new Dictionary<string, PointItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rows)
            {
                if (runsInSystem != null && !runsInSystem.Contains(row.LineName ?? string.Empty))
                    continue;

                if (!string.IsNullOrWhiteSpace(row.BackCode) && !pointsDict.ContainsKey(row.BackCode))
                {
                    pointsDict[row.BackCode] = new PointItem(row.BackCode, row.LineName, row.LineSummary?.Index ?? 0);
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode) && !pointsDict.ContainsKey(row.ForeCode))
                {
                    pointsDict[row.ForeCode] = new PointItem(row.ForeCode, row.LineName, row.LineSummary?.Index ?? 0);
                }
            }

            foreach (var point in pointsDict.Values
                         .OrderBy(p => p.LineIndex)
                         .ThenBy(p => PointCodeHelper.GetSortKey(p.Code)))
            {
                _availablePoints.Add(point);
            }
        }

        private void UpdateBenchmarks()
        {
            _benchmarks.Clear();

            var selectedSystemId = SelectedSystem?.Id;

            foreach (var kvp in _dataViewModel.KnownHeights.OrderBy(k => PointCodeHelper.GetSortKey(k.Key)))
            {
                if (!_benchmarkSystems.TryGetValue(kvp.Key, out var benchmarkSystemId))
                {
                    benchmarkSystemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
                    _benchmarkSystems[kvp.Key] = benchmarkSystemId;
                }

                if (string.IsNullOrEmpty(selectedSystemId) || benchmarkSystemId == selectedSystemId)
                {
                    _benchmarks.Add(new BenchmarkItem(kvp.Key, kvp.Value, benchmarkSystemId));
                }
            }
        }

        private void ExportToCsv()
        {
            if (_network == null)
                return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"Нивелирование_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            try
            {
                var csv = _exportService.BuildCsv(_network);
                System.IO.File.WriteAllText(saveFileDialog.FileName, csv, System.Text.Encoding.UTF8);
                _dialogService.ShowInfo($"Данные успешно экспортированы в:\n{saveFileDialog.FileName}", "Экспорт завершён");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка при экспорте:\n{ex.Message}", "Ошибка экспорта");
            }
        }

        private void ClearCalculationResults()
        {
            _rows.Clear();
            Closure = null;
            AllowableClosure = null;
            ClosureVerdict = "Нет данных для расчёта.";
            StationsCount = 0;
            TotalAverageLength = 0;
        }

        private void InitializeSystemsFromRuns()
        {
            var defaultSystem = _systems.FirstOrDefault(s => s.Id == ITraverseSystemsManager.DEFAULT_SYSTEM_ID);
            if (defaultSystem == null)
            {
                defaultSystem = new TraverseSystem(ITraverseSystemsManager.DEFAULT_SYSTEM_ID, ITraverseSystemsManager.DEFAULT_SYSTEM_NAME, order: 0);
                _systems.Insert(0, defaultSystem);
            }

            foreach (var system in _systems)
            {
                foreach (var runIndex in system.RunIndexes.ToList())
                {
                    system.RemoveRun(runIndex);
                }
            }

            foreach (var run in Runs)
            {
                if (string.IsNullOrWhiteSpace(run.SystemId))
                {
                    run.SystemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
                }

                var system = _systems.FirstOrDefault(s => s.Id == run.SystemId) ?? defaultSystem;
                system.AddRun(run.Index);
            }

            if (SelectedSystem == null)
            {
                SelectedSystem = defaultSystem;
            }
        }

        private void ApplyRunActivation(LevelingNetwork network)
        {
            var runStates = Runs.ToList();
            for (int i = 0; i < network.Runs.Count && i < runStates.Count; i++)
            {
                if (runStates[i].IsActive)
                {
                    network.Runs[i].Activate();
                }
                else
                {
                    network.Runs[i].Deactivate();
                }
            }
        }

        private static LineSummary BuildLineSummary(int index, NetworkRunSummaryDto? runSummary)
        {
            runSummary ??= new NetworkRunSummaryDto(
                Id: Guid.Empty,
                Name: $"Ход {index:D2}",
                OriginalNumber: null,
                StationCount: 0,
                TotalLengthMeters: 0,
                StartPointCode: string.Empty,
                EndPointCode: string.Empty,
                DeltaHSum: 0,
                ClosureValueMm: null,
                ClosureToleranceMm: null,
                IsClosureWithinTolerance: null,
                IsActive: true,
                SystemName: null);

            var summary = new LineSummary(
                index,
                startTarget: runSummary.StartPointCode,
                startStation: null,
                endTarget: runSummary.EndPointCode,
                endStation: null,
                recordCount: runSummary.StationCount,
                deltaHSum: runSummary.DeltaHSum,
                totalDistanceBack: null,
                totalDistanceFore: null,
                armDifferenceAccumulation: null,
                knownPointsCount: 0,
                systemId: ITraverseSystemsManager.DEFAULT_SYSTEM_ID,
                isActive: runSummary.IsActive,
                originalLineNumber: runSummary.OriginalNumber);

            if (runSummary.ClosureValueMm.HasValue)
            {
                summary.SetClosures(new[] { runSummary.ClosureValueMm.Value / 1000.0 });
            }

            return summary;
        }

        private static void ApplyNetworkSummary(LineSummary summary, NetworkRunSummaryDto runSummary)
        {
            summary.TotalDistanceBack = null;
            summary.TotalDistanceFore = null;
            summary.ArmDifferenceAccumulation = null;
            summary.IsActive = runSummary.IsActive;
            summary.SystemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;

            if (runSummary.ClosureValueMm.HasValue)
            {
                summary.SetClosures(new[] { runSummary.ClosureValueMm.Value / 1000.0 });
            }
            else
            {
                summary.SetClosures(Array.Empty<double>());
            }
        }
    }

    public record LevelingMethodOption(string Code, string Description, ToleranceMode Mode, double Coefficient, double OrientationSign) : IToleranceOption
    {
        public string Display => Code;
    }

    public record LevelingClassOption(
        string Code,
        string Description,
        ToleranceMode Mode,
        double Coefficient,
        double ArmDiffStation,
        double ArmDiffAccumulation) : IToleranceOption
    {
        public string Display => Code;

        public double ArmDifferenceToleranceStation => ArmDiffStation;
        public double ArmDifferenceToleranceAccumulation => ArmDiffAccumulation;
    }
}
