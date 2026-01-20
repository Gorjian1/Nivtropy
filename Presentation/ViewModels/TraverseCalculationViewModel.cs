using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Win32;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Export;
using Nivtropy.Presentation.Models;
using Nivtropy.Application.Enums;
using Nivtropy.Application.Services;
using Nivtropy.Domain.Services;
using Nivtropy.Utilities;
using Nivtropy.Presentation.ViewModels.Base;
using Nivtropy.Presentation.ViewModels.Managers;
using Nivtropy.Presentation.Mappers;
using Nivtropy.Constants;
using Nivtropy.Presentation.Services.Dialog;

namespace Nivtropy.Presentation.ViewModels
{
    public class TraverseCalculationViewModel : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ITraverseCalculationService _calculationService;
        private readonly ITraverseCalculationWorkflowService _workflowService;
        private readonly ITraverseExportService _exportService;
        private readonly IDialogService _dialogService;
        private readonly ISystemConnectivityService _connectivityService;
        private readonly ObservableCollection<TraverseRow> _rows = new();
        private readonly ObservableCollection<PointItem> _availablePoints = new();
        private readonly ObservableCollection<BenchmarkItem> _benchmarks = new();
        private readonly ObservableCollection<SharedPointLinkItem> _sharedPoints = new();
        private readonly ObservableCollection<TraverseSystem> _systems = new();
        private readonly Dictionary<string, SharedPointLinkItem> _sharedPointLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _benchmarkSystems = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<int, List<string>> _sharedPointsByRun = new();

        // Методы нивелирования для двойного хода
        // Допуск: 4 мм × √n, где n - число станций
        private readonly LevelingMethodOption[] _methods =
        {
            new("BF", "Двойной ход (Back → Forward)", ToleranceMode.SqrtStations, ToleranceCoefficients.DoubleRun, 1.0),
            new("FB", "Двойной ход (Forward → Back)", ToleranceMode.SqrtStations, ToleranceCoefficients.DoubleRun, -1.0)
        };

        // Классы нивелирования согласно ГКИНП 03-010-02
        // Допуск невязки: коэффициент × √L, где L - длина хода в км (в один конец)
        // Допуски разности плеч: на станции и накопление за ход
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
        private AdjustmentMode _adjustmentMode = AdjustmentMode.Local;
        private double? _closure;
        private double? _allowableClosure;
        private string _closureVerdict = "Нет данных для расчёта.";
        private double _totalBackDistance;
        private double _totalForeDistance;
        private double _totalAverageDistance;
        private double? _methodTolerance;
        private double? _classTolerance;
        private int _stationsCount;
        private TraverseRow? _selectedRow;
        private string? _selectedPointCode;
        private PointItem? _selectedPoint;
        private string? _newBenchmarkHeight;
        private TraverseSystem? _selectedSystem;
        private bool _isCalculating;
        private CancellationTokenSource? _calculationCts;

        public TraverseCalculationViewModel(
            DataViewModel dataViewModel,
            SettingsViewModel settingsViewModel,
            ITraverseCalculationService calculationService,
            ITraverseCalculationWorkflowService workflowService,
            ITraverseExportService exportService,
            IDialogService dialogService,
            ISystemConnectivityService connectivityService)
        {
            _dataViewModel = dataViewModel;
            _settingsViewModel = settingsViewModel;
            _calculationService = calculationService;
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _exportService = exportService;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _connectivityService = connectivityService;
            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += OnRecordsCollectionChanged;
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            // Используем базовый класс для batch updates
            SubscribeToBatchUpdates(_dataViewModel);

            _selectedMethod = _methods.FirstOrDefault();
            _selectedClass = _classes.FirstOrDefault();

            // Инициализация системы по умолчанию
            var defaultSystem = new TraverseSystem(ITraverseSystemsManager.DEFAULT_SYSTEM_ID, ITraverseSystemsManager.DEFAULT_SYSTEM_NAME, order: 0);
            _systems.Add(defaultSystem);
            _selectedSystem = defaultSystem;

            UpdateRows();
        }

        /// <summary>
        /// Вызывается после завершения batch update
        /// </summary>
        protected override void OnBatchUpdateCompleted()
        {
            if (UseAsyncCalculations)
            {
                // Fire-and-forget async update
                _ = UpdateRowsAsync();
            }
            else
            {
                UpdateRows();
            }
        }

        private void OnRecordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsUpdating)
                return;

            if (UseAsyncCalculations)
            {
                _ = UpdateRowsAsync();
            }
            else
            {
                UpdateRows();
            }
        }

        public ObservableCollection<TraverseRow> Rows => _rows;
        public ObservableCollection<LineSummary> Runs => _dataViewModel.Runs;
        public ObservableCollection<PointItem> AvailablePoints => _availablePoints;
        public ObservableCollection<BenchmarkItem> Benchmarks => _benchmarks;
        public ObservableCollection<SharedPointLinkItem> SharedPoints => _sharedPoints;
        public ObservableCollection<TraverseSystem> Systems => _systems;
        public SettingsViewModel Settings => _settingsViewModel;

        public TraverseSystem? SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (_selectedSystem != value)
                {
                    _selectedSystem = value;
                    OnPropertyChanged();
                    // При смене системы обновляем списки доступных точек и реперов
                    UpdateAvailablePoints();
                    UpdateBenchmarks();
                }
            }
        }

        public LevelingMethodOption[] Methods => _methods;
        public LevelingClassOption[] Classes => _classes;

        public LevelingMethodOption? SelectedMethod
        {
            get => _selectedMethod;
            set
            {
                if (_selectedMethod != value)
                {
                    _selectedMethod = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MethodOrientationSign));

                    if (StationsCount > 0)
                    {
                        UpdateRows();
                    }
                }
            }
        }

        public LevelingClassOption? SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (_selectedClass != value)
                {
                    _selectedClass = value;
                    OnPropertyChanged();
                    if (UseAsyncCalculations)
                        _ = UpdateRowsAsync();
                    else
                        UpdateRows();
                }
            }
        }

        public AdjustmentMode AdjustmentMode
        {
            get => _adjustmentMode;
            set
            {
                if (SetField(ref _adjustmentMode, value))
                {
                    if (UseAsyncCalculations)
                        _ = UpdateRowsAsync();
                    else
                        UpdateRows();
                }
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
                    UpdateRows();
                }
            }
        }

        public double? Closure
        {
            get => _closure;
            private set
            {
                if (SetField(ref _closure, value))
                {
                    OnPropertyChanged(nameof(ClosureAbsolute));
                    OnPropertyChanged(nameof(IsClosureWithinTolerance));
                }
            }
        }

        public double? ClosureAbsolute => Closure.HasValue ? Math.Abs(Closure.Value) : null;

        public bool IsClosureWithinTolerance =>
            ClosureAbsolute.HasValue && AllowableClosure.HasValue &&
            ClosureAbsolute.Value <= AllowableClosure.Value;

        public double? AllowableClosure
        {
            get => _allowableClosure;
            private set
            {
                if (SetField(ref _allowableClosure, value))
                {
                    OnPropertyChanged(nameof(IsClosureWithinTolerance));
                }
            }
        }

        public double? MethodTolerance
        {
            get => _methodTolerance;
            private set => SetField(ref _methodTolerance, value);
        }

        public double? ClassTolerance
        {
            get => _classTolerance;
            private set => SetField(ref _classTolerance, value);
        }

        public string ClosureVerdict
        {
            get => _closureVerdict;
            private set => SetField(ref _closureVerdict, value);
        }

        public double TotalBackDistance
        {
            get => _totalBackDistance;
            private set
            {
                if (SetField(ref _totalBackDistance, value))
                {
                    OnPropertyChanged(nameof(TotalAverageLength));
                }
            }
        }

        public double TotalForeDistance
        {
            get => _totalForeDistance;
            private set
            {
                if (SetField(ref _totalForeDistance, value))
                {
                    OnPropertyChanged(nameof(TotalAverageLength));
                }
            }
        }

        public double TotalAverageDistance
        {
            get => _totalAverageDistance;
            private set => SetField(ref _totalAverageDistance, value);
        }

        /// <summary>
        /// Общая длина хода: сумма длин назад и вперёд (в метрах)
        /// </summary>
        public double TotalAverageLength => TotalBackDistance + TotalForeDistance;

        /// <summary>
        /// Длина хода в километрах (используется в формулах допусков по классу)
        /// По теории - берется длина в один конец, не среднее
        /// </summary>
        public double TotalLengthKilometers => TotalBackDistance / 1000.0;

        public double MethodOrientationSign => SelectedMethod?.OrientationSign ?? 1.0;

        public int StationsCount
        {
            get => _stationsCount;
            private set => SetField(ref _stationsCount, value);
        }

        public TraverseRow? SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        public PointItem? SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (SetField(ref _selectedPoint, value))
                {
                    OnPropertyChanged(nameof(CanAddBenchmark));
                }
            }
        }

        public string? NewBenchmarkHeight
        {
            get => _newBenchmarkHeight;
            set
            {
                if (SetField(ref _newBenchmarkHeight, value))
                {
                    OnPropertyChanged(nameof(CanAddBenchmark));
                }
            }
        }

        public bool CanAddBenchmark =>
            SelectedPoint != null &&
            !string.IsNullOrWhiteSpace(NewBenchmarkHeight) &&
            double.TryParse(NewBenchmarkHeight, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

        public ICommand AddBenchmarkCommand => new RelayCommand(_ => AddBenchmark(), _ => CanAddBenchmark);
        public ICommand RemoveBenchmarkCommand => new RelayCommand(param => RemoveBenchmark(param as BenchmarkItem));
        public ICommand ExportCommand => new RelayCommand(_ => ExportToCsv(), _ => Rows.Count > 0);

        public bool CanSetHeight => !string.IsNullOrWhiteSpace(_selectedPointCode);
        public bool CanClearHeight => !string.IsNullOrWhiteSpace(_selectedPointCode) && _dataViewModel.HasKnownHeight(_selectedPointCode);

        /// <summary>
        /// Проверяет, есть ли известная высота у точки
        /// </summary>
        public bool HasKnownHeight(string? pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return false;

            return _dataViewModel.HasKnownHeight(pointCode);
        }

        /// <summary>
        /// Проверяет, включена ли точка как общая между ходами
        /// </summary>
        public bool IsSharedPointEnabled(string? pointCode)
        {
            return _dataViewModel.IsSharedPointEnabled(pointCode);
        }

        /// <summary>
        /// Обновляет выбранную точку для установки высоты
        /// </summary>
        public void UpdateSelectedPoint(string? pointCode)
        {
            _selectedPointCode = pointCode;
            OnPropertyChanged(nameof(CanSetHeight));
            OnPropertyChanged(nameof(CanClearHeight));
        }

        /// <summary>
        /// Использовать асинхронные вычисления при загрузке данных.
        /// </summary>
        public bool UseAsyncCalculations { get; set; } = true;

        /// <summary>
        /// Индикатор выполнения расчётов (для UI).
        /// </summary>
        public bool IsCalculating
        {
            get => _isCalculating;
            private set => SetField(ref _isCalculating, value);
        }

        /// <summary>
        /// Устанавливает известную высоту для точки
        /// </summary>
        public void SetKnownHeightForPoint(string pointCode, double height)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            _dataViewModel.SetKnownHeight(pointCode, height);
            UpdateRows();
        }

        /// <summary>
        /// Удаляет известную высоту у точки
        /// </summary>
        public void ClearKnownHeightForPoint(string pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            _dataViewModel.ClearKnownHeight(pointCode);
            UpdateRows();
        }

        /// <summary>
        /// Добавляет новый репер (точку с известной высотой)
        /// </summary>
        private void AddBenchmark()
        {
            if (SelectedPoint == null || string.IsNullOrWhiteSpace(NewBenchmarkHeight))
                return;

            if (!double.TryParse(NewBenchmarkHeight, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var height))
                return;

            // Устанавливаем высоту в DataViewModel
            _dataViewModel.SetKnownHeight(SelectedPoint.Code, height);

            // Привязываем репер к текущей системе
            _benchmarkSystems[SelectedPoint.Code] = SelectedSystem?.Id ?? ITraverseSystemsManager.DEFAULT_SYSTEM_ID;

            // Очищаем поля ввода
            SelectedPoint = null;
            NewBenchmarkHeight = string.Empty;

            // Пересчитываем высоты (внутри вызывается UpdateBenchmarks)
            UpdateRows();
        }

        /// <summary>
        /// Удаляет репер
        /// </summary>
        private void RemoveBenchmark(BenchmarkItem? benchmark)
        {
            if (benchmark == null)
                return;

            _dataViewModel.ClearKnownHeight(benchmark.Code);
            _benchmarkSystems.Remove(benchmark.Code);
            UpdateBenchmarks();
            UpdateRows();
        }

        /// <summary>
        /// Экспортирует данные в CSV с 4-частной структурой для каждого хода
        /// Делегирует логику экспорта в ITraverseExportService
        /// </summary>
        private void ExportToCsv()
        {
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
                var dtos = MapRowsToDtos(_rows.ToList());
                var csv = _exportService.BuildCsv(dtos);
                System.IO.File.WriteAllText(saveFileDialog.FileName, csv, System.Text.Encoding.UTF8);
                _dialogService.ShowInfo($"Данные успешно экспортированы в:\n{saveFileDialog.FileName}", "Экспорт завершён");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка при экспорте:\n{ex.Message}", "Ошибка экспорта");
            }
        }

        private static List<StationDto> MapRowsToDtos(IEnumerable<TraverseRow> rows)
        {
            return rows.Select(row => row.ToDto()).ToList();
        }

        /// <summary>
        /// Обновляет список доступных точек из текущих станций
        /// Фильтрует по выбранной системе
        /// </summary>
        private void UpdateAvailablePoints()
        {
            _availablePoints.Clear();

            var selectedSystemId = SelectedSystem?.Id;
            var pointsDict = new Dictionary<string, PointItem>(StringComparer.OrdinalIgnoreCase);

            // Получаем ходы выбранной системы
            var runsInSystem = selectedSystemId != null
                ? Runs.Where(r => r.SystemId == selectedSystemId).Select(r => r.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var row in _rows)
            {
                // Фильтруем по выбранной системе
                if (runsInSystem != null && !runsInSystem.Contains(row.LineName ?? ""))
                    continue;

                // Добавляем заднюю точку
                if (!string.IsNullOrWhiteSpace(row.BackCode) && !pointsDict.ContainsKey(row.BackCode))
                {
                    var lineIndex = row.LineSummary?.Index ?? 0;
                    pointsDict[row.BackCode] = new PointItem(row.BackCode, row.LineName, lineIndex);
                }

                // Добавляем переднюю точку
                if (!string.IsNullOrWhiteSpace(row.ForeCode) && !pointsDict.ContainsKey(row.ForeCode))
                {
                    var lineIndex = row.LineSummary?.Index ?? 0;
                    pointsDict[row.ForeCode] = new PointItem(row.ForeCode, row.LineName, lineIndex);
                }
            }

            // Сортируем по индексу хода, затем по естественному порядку кода (числовой/алфавитный)
            var sortedPoints = pointsDict.Values
                .OrderBy(p => p.LineIndex)
                .ThenBy(p => PointCodeHelper.GetSortKey(p.Code))
                .ToList();

            foreach (var point in sortedPoints)
            {
                _availablePoints.Add(point);
            }
        }

        /// <summary>
        /// Обновляет список реперов из DataViewModel с фильтрацией по выбранной системе
        /// </summary>
        private void UpdateBenchmarks()
        {
            _benchmarks.Clear();

            var selectedSystemId = SelectedSystem?.Id;

            foreach (var kvp in _dataViewModel.KnownHeights
                             .OrderBy(k => PointCodeHelper.GetSortKey(k.Key)))
            {
                // Определяем систему для этого репера
                if (!_benchmarkSystems.TryGetValue(kvp.Key, out var benchmarkSystemId))
                {
                    // Если репер еще не привязан к системе, привязываем к системе по умолчанию
                    benchmarkSystemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
                    _benchmarkSystems[kvp.Key] = benchmarkSystemId;
                }

                // Показываем только реперы текущей системы
                if (string.IsNullOrEmpty(selectedSystemId) || benchmarkSystemId == selectedSystemId)
                {
                    _benchmarks.Add(new BenchmarkItem(kvp.Key, kvp.Value, benchmarkSystemId));
                }
            }
        }

        private void DataViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun))
            {
                OnPropertyChanged(nameof(SelectedRun));
                UpdateRows();
            }
            else if (e.PropertyName == nameof(DataViewModel.SharedPointStates))
            {
                UpdateRows();
            }
        }

        private void UpdateRows()
        {
            IsCalculating = true;
            try
            {
                var records = _dataViewModel.RawRecords;

                if (records.Count == 0)
                {
                    ClearCalculationResults();
                    return;
                }

                var runDtos = Runs.Select(r => r.ToDto()).ToList();
                UpdateSharedPointsMetadata(_dataViewModel.Records);
                InitializeRunSystems();

                var items = _calculationService.BuildTraverseRows(records.ToList(), runDtos);
                var request = CreateCalculationRequest(items, runDtos);
                var result = _workflowService.Calculate(request);
                ApplyCalculationResult(result);
            }
            finally
            {
                IsCalculating = false;
            }
        }

        /// <summary>
        /// Очищает результаты расчётов при отсутствии данных
        /// </summary>
        private void ClearCalculationResults()
        {
            _rows.Clear();
            Closure = null;
            AllowableClosure = null;
            ClosureVerdict = "Нет данных для расчёта.";
            StationsCount = 0;
            TotalBackDistance = 0;
            TotalForeDistance = 0;
            TotalAverageDistance = 0;
            MethodTolerance = null;
            ClassTolerance = null;
        }

        #region Async Calculations

        /// <summary>
        /// Асинхронно обновляет строки хода.
        /// Тяжёлые вычисления выполняются в фоновом потоке.
        /// </summary>
        public async Task UpdateRowsAsync()
        {
            // Отменяем предыдущий расчёт если он ещё выполняется
            _calculationCts?.Cancel();
            _calculationCts = new CancellationTokenSource();
            var token = _calculationCts.Token;

            IsCalculating = true;

            try
            {
                var records = _dataViewModel.RawRecords.ToList();

                if (records.Count == 0)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _rows.Clear();
                        Closure = null;
                        AllowableClosure = null;
                        ClosureVerdict = "Нет данных для расчёта.";
                        StationsCount = 0;
                        TotalBackDistance = 0;
                        TotalForeDistance = 0;
                        TotalAverageDistance = 0;
                        MethodTolerance = null;
                        ClassTolerance = null;
                    });
                    return;
                }

                var runDtos = Runs.Select(r => r.ToDto()).ToList();
                UpdateSharedPointsMetadata(_dataViewModel.Records);
                InitializeRunSystems();

                var systemsSnapshot = _systems
                    .Select(system => new TraverseSystemDescriptorDto(system.Id, system.Order))
                    .ToList();
                var sharedPointsByRun = _sharedPointsByRun.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToList());
                var knownHeightsSnapshot = new Dictionary<string, double>(_dataViewModel.KnownHeights, StringComparer.OrdinalIgnoreCase);
                var sharedPointStatesSnapshot = new Dictionary<string, bool>(_dataViewModel.SharedPointStates, StringComparer.OrdinalIgnoreCase);
                var benchmarkSystemsSnapshot = new Dictionary<string, string>(_benchmarkSystems, StringComparer.OrdinalIgnoreCase);
                var methodOption = SelectedMethod;
                var classOption = SelectedClass;
                var adjustmentMode = AdjustmentMode;
                var orientationSign = MethodOrientationSign;

                var result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var items = _calculationService.BuildTraverseRows(records, runDtos);
                    var request = CreateCalculationRequest(
                        items,
                        runDtos,
                        systemsSnapshot,
                        sharedPointsByRun,
                        knownHeightsSnapshot,
                        sharedPointStatesSnapshot,
                        benchmarkSystemsSnapshot,
                        methodOption,
                        classOption,
                        adjustmentMode,
                        orientationSign);
                    return _workflowService.Calculate(request);
                }, token);

                token.ThrowIfCancellationRequested();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    ApplyCalculationResult(result);
                });
            }
            catch (OperationCanceledException)
            {
                // Расчёт был отменён - это нормально
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateRowsAsync error: {ex.Message}");
            }
            finally
            {
                IsCalculating = false;
            }
        }
        #endregion

        private List<SharedPointDto> BuildSharedPointDtos()
        {
            var runIndexesByPoint = _sharedPointsByRun
                .SelectMany(kvp => kvp.Value.Select(code => (code, runIndex: kvp.Key)))
                .GroupBy(x => x.code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.runIndex).Distinct().ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return _sharedPoints
                .Select(point =>
                {
                    runIndexesByPoint.TryGetValue(point.Code, out var indexes);
                    return point.ToDto(indexes ?? new List<int>());
                })
                .ToList();
        }

        private TraverseCalculationRequest CreateCalculationRequest(
            IReadOnlyList<StationDto> items,
            IReadOnlyList<RunSummaryDto> runDtos)
        {
            var systemsSnapshot = _systems
                .Select(system => new TraverseSystemDescriptorDto(system.Id, system.Order))
                .ToList();

            var sharedPointsByRun = _sharedPointsByRun.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList());

            return CreateCalculationRequest(
                items,
                runDtos,
                systemsSnapshot,
                sharedPointsByRun,
                new Dictionary<string, double>(_dataViewModel.KnownHeights, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, bool>(_dataViewModel.SharedPointStates, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(_benchmarkSystems, StringComparer.OrdinalIgnoreCase),
                SelectedMethod,
                SelectedClass,
                AdjustmentMode,
                MethodOrientationSign);
        }

        private static TraverseCalculationRequest CreateCalculationRequest(
            IReadOnlyList<StationDto> items,
            IReadOnlyList<RunSummaryDto> runDtos,
            IReadOnlyList<TraverseSystemDescriptorDto> systemsSnapshot,
            IReadOnlyDictionary<int, List<string>> sharedPointsByRun,
            IReadOnlyDictionary<string, double> knownHeights,
            IReadOnlyDictionary<string, bool> sharedPointStates,
            IReadOnlyDictionary<string, string> benchmarkSystems,
            IToleranceOption? methodOption,
            LevelingClassOption? classOption,
            AdjustmentMode adjustmentMode,
            double orientationSign)
        {
            return new TraverseCalculationRequest
            {
                Stations = items.ToList(),
                Runs = runDtos.ToList(),
                Systems = systemsSnapshot.ToList(),
                SharedPointsByRun = sharedPointsByRun.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList()),
                KnownHeights = new Dictionary<string, double>(knownHeights, StringComparer.OrdinalIgnoreCase),
                SharedPointStates = new Dictionary<string, bool>(sharedPointStates, StringComparer.OrdinalIgnoreCase),
                BenchmarkSystems = new Dictionary<string, string>(benchmarkSystems, StringComparer.OrdinalIgnoreCase),
                MethodOption = methodOption,
                ClassOption = classOption,
                AdjustmentMode = adjustmentMode,
                MethodOrientationSign = orientationSign
            };
        }

        private void ApplyCalculationResult(TraverseCalculationResult result)
        {
            _rows.Clear();

            var runLookup = Runs.ToDictionary(r => r.Index);
            foreach (var runDto in result.Runs)
            {
                if (runLookup.TryGetValue(runDto.Index, out var summary))
                {
                    summary.ApplyFrom(runDto);
                }
            }

            foreach (var station in result.Stations)
            {
                if (station.RunSummary?.IsActive == false)
                    continue;

                LineSummary? summary = null;
                if (station.RunSummary != null && runLookup.TryGetValue(station.RunSummary.Index, out var existing))
                {
                    summary = existing;
                }

                _rows.Add(station.ToModel(summary));
            }

            StationsCount = result.StationsCount;
            TotalBackDistance = result.TotalBackDistance;
            TotalForeDistance = result.TotalForeDistance;
            TotalAverageDistance = result.TotalAverageDistance;

            Closure = result.Closure.Closure;
            AllowableClosure = result.Closure.AllowableClosure;
            MethodTolerance = result.Closure.MethodTolerance;
            ClassTolerance = result.Closure.ClassTolerance;
            ClosureVerdict = result.Closure.Verdict;

            UpdateAvailablePoints();
            UpdateBenchmarks();
        }

        private void UpdateSharedPointsMetadata(IReadOnlyCollection<MeasurementRecord> records)
        {
            var usage = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            void AddUsage(string? code, int runIndex)
            {
                if (runIndex == 0 || string.IsNullOrWhiteSpace(code))
                    return;

                var trimmed = code.Trim();
                if (!usage.TryGetValue(trimmed, out var set))
                {
                    set = new HashSet<int>();
                    usage[trimmed] = set;
                }

                set.Add(runIndex);
            }

            foreach (var record in records)
            {
                if (!string.IsNullOrWhiteSpace(record.LineMarker))
                    continue;

                var runIndex = record.LineSummary?.Index ?? 0;
                AddUsage(record.Target, runIndex);
                AddUsage(record.StationCode, runIndex);
            }

            var sharedCodes = usage
                .Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _sharedPointsByRun = sharedCodes
                .SelectMany(kvp => kvp.Value.Select(run => (run, kvp.Key)))
                .GroupBy(x => x.run)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Key)
                        .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                        .ToList());

            var codesToRemove = _sharedPointLookup.Keys.Where(code => !sharedCodes.ContainsKey(code)).ToList();
            foreach (var code in codesToRemove)
            {
                if (_sharedPointLookup.TryGetValue(code, out var item))
                {
                    _sharedPoints.Remove(item);
                    _sharedPointLookup.Remove(code);
                }
            }

            foreach (var kvp in sharedCodes)
            {
                if (!_sharedPointLookup.TryGetValue(kvp.Key, out var item))
                {
                    var enabled = _dataViewModel.IsSharedPointEnabled(kvp.Key);
                    item = new SharedPointLinkItem(kvp.Key, enabled, (code, state) => _dataViewModel.SetSharedPointEnabled(code, state));
                    _sharedPointLookup[kvp.Key] = item;
                    _sharedPoints.Add(item);
                }

                item.SetRunIndexes(kvp.Value);
            }

            var ordered = _sharedPoints
                .OrderBy(p => PointCodeHelper.GetSortKey(p.Code))
                .ToList();

            _sharedPoints.Clear();
            foreach (var item in ordered)
            {
                _sharedPoints.Add(item);
            }
        }

        /// <summary>
        /// Инициализация систем для новых ходов
        /// </summary>
        private void InitializeRunSystems()
        {
            // Сначала автоматически разбиваем на системы по связности
            RebuildSystemsByConnectivity();

            var defaultSystem = _systems.FirstOrDefault(s => s.Id == ITraverseSystemsManager.DEFAULT_SYSTEM_ID);
            if (defaultSystem == null)
                return;

            foreach (var run in Runs)
            {
                // Если ход еще не привязан к системе, привязываем к системе по умолчанию
                if (string.IsNullOrEmpty(run.SystemId))
                {
                    run.SystemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
                }

                // Добавляем ход в RunIndexes соответствующей системы
                var system = _systems.FirstOrDefault(s => s.Id == run.SystemId);
                if (system != null && !system.ContainsRun(run.Index))
                {
                    system.AddRun(run.Index);
                }
            }
        }

        /// <summary>
        /// Автоматически разбивает ходы на системы по связности через общие точки.
        /// Ходы, связанные через включённые общие точки, попадают в одну систему.
        /// </summary>
        private void RebuildSystemsByConnectivity()
        {
            if (Runs.Count == 0)
                return;

            var existingAutoSystemIds = _systems
                .Where(s => s.Id.StartsWith("system-auto-"))
                .Select(s => s.Id)
                .ToList();

            var result = _connectivityService.AnalyzeConnectivity(
                Runs.Select(r => r.ToDto()).ToList(),
                BuildSharedPointDtos(),
                existingAutoSystemIds);

            // Применяем результат: назначаем ходам системы
            foreach (var kvp in result.RunToSystemId)
            {
                var run = Runs.FirstOrDefault(r => r.Index == kvp.Key);
                if (run != null)
                {
                    run.SystemId = kvp.Value;
                }
            }

            // Создаём новые системы
            foreach (var (id, name, order) in result.NewSystems)
            {
                _systems.Add(new TraverseSystem(id, name, order));
            }

            // Удаляем ненужные автосистемы
            foreach (var systemId in result.SystemsToRemove)
            {
                var system = _systems.FirstOrDefault(s => s.Id == systemId);
                if (system != null)
                {
                    _systems.Remove(system);
                }
            }
        }

        public List<SharedPointLinkItem> GetSharedPointsForRun(LineSummary? run)
        {
            if (run == null)
                return new List<SharedPointLinkItem>();

            return _sharedPoints
                .Where(p => p.IsUsedInRun(run.Index))
                .OrderBy(p => PointCodeHelper.GetSortKey(p.Code))
                .ToList();
        }
    }
