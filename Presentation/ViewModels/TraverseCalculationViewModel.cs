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
using Nivtropy.Presentation.Models;
using Nivtropy.Models;
using Nivtropy.Application.Enums;
using Nivtropy.Application.Services;
using Nivtropy.Services.Calculation;
using Nivtropy.Infrastructure.Export;
using Nivtropy.Utilities;
using Nivtropy.Presentation.ViewModels.Base;
using Nivtropy.Presentation.ViewModels.Helpers;
using Nivtropy.Presentation.ViewModels.Managers;
using Nivtropy.Constants;

namespace Nivtropy.Presentation.ViewModels
{
    public class TraverseCalculationViewModel : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ITraverseCalculationService _calculationService;
        private readonly IClosureCalculationService _closureService;
        private readonly IExportService _exportService;
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
            IClosureCalculationService closureService,
            IExportService exportService,
            ISystemConnectivityService connectivityService)
        {
            _dataViewModel = dataViewModel;
            _settingsViewModel = settingsViewModel;
            _calculationService = calculationService;
            _closureService = closureService;
            _exportService = exportService;
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
                    UpdateTolerance();
                    CheckArmDifferenceTolerances(); // Пересчёт при смене класса
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
        /// Использовать инкрементальное обновление при изменении высот.
        /// Если false - всегда пересчитывается всё (безопасный режим).
        /// </summary>
        public bool UseIncrementalUpdates { get; set; } = true;

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

            // Если уже есть рассчитанные данные и включены инкрементальные обновления
            if (UseIncrementalUpdates && _rows.Count > 0)
            {
                var affectedRuns = FindRunsContainingPoint(pointCode);
                if (affectedRuns.Count > 0 && affectedRuns.Count < Runs.Count)
                {
                    // Пересчитываем только затронутые ходы
                    RecalculateRunsIncremental(affectedRuns);
                    return;
                }
            }

            // Полный пересчёт при первой загрузке или если затронуты все ходы
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
        /// Делегирует логику экспорта в IExportService
        /// </summary>
        private void ExportToCsv()
        {
            _exportService.ExportToCsv(_rows);
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
                var records = _dataViewModel.Records;

                if (records.Count == 0)
                {
                    ClearCalculationResults();
                    return;
                }

                var items = _calculationService.BuildTraverseRows(records.ToList(), Runs);
                ProcessTraverseItems(items);
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
                var records = _dataViewModel.Records.ToList();

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

                // Тяжёлые вычисления в фоновом потоке
                var items = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    return _calculationService.BuildTraverseRows(records, Runs);
                }, token);

                token.ThrowIfCancellationRequested();

                // Обновление UI в главном потоке
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    // Вызываем синхронный метод для обновления (уже в UI потоке)
                    UpdateRowsFromItems(items);
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

        /// <summary>
        /// Обновляет строки из уже построенных items (для использования из async метода)
        /// </summary>
        private void UpdateRowsFromItems(List<TraverseRow> items)
        {
            ProcessTraverseItems(items);
        }

        /// <summary>
        /// Общая логика обработки items - используется из UpdateRows и UpdateRowsFromItems
        /// </summary>
        private void ProcessTraverseItems(List<TraverseRow> items)
        {
            _rows.Clear();

            var traverseGroupsDict = items.GroupBy(r => r.LineName)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var runsLookup = Runs.ToDictionary(r => r.DisplayName, r => r, StringComparer.OrdinalIgnoreCase);

            UpdateSharedPointsMetadata(_dataViewModel.Records);
            InitializeRunSystems();

            foreach (var system in _systems.OrderBy(s => s.Order))
            {
                ProcessSystem(system, traverseGroupsDict, runsLookup);
            }

            foreach (var row in items)
            {
                if (runsLookup.TryGetValue(row.LineName, out var run) && run.IsActive)
                    _rows.Add(row);
            }

            UpdateStatistics();
            UpdateAvailablePoints();
            UpdateBenchmarks();
            RecalculateClosure();
            UpdateTolerance();
            CheckArmDifferenceTolerances();
        }

        /// <summary>
        /// Обрабатывает одну систему ходов
        /// </summary>
        private void ProcessSystem(
            TraverseSystem system,
            Dictionary<string, List<TraverseRow>> traverseGroupsDict,
            Dictionary<string, LineSummary> runsLookup)
        {
            var systemTraverseGroups = traverseGroupsDict
                .Where(kvp => runsLookup.TryGetValue(kvp.Key, out var run) &&
                              run.SystemId == system.Id && run.IsActive)
                .ToList();

            if (systemTraverseGroups.Count == 0)
                return;

            var availableAdjustedHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var availableRawHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _dataViewModel.KnownHeights)
            {
                if (_benchmarkSystems.TryGetValue(kvp.Key, out var benchSystemId) && benchSystemId == system.Id)
                {
                    availableAdjustedHeights[kvp.Key] = kvp.Value;
                    availableRawHeights[kvp.Key] = kvp.Value;
                }
            }

            bool AnchorChecker(string code) => IsAnchorAllowed(code, availableAdjustedHeights.ContainsKey);

            ProcessSystemTraverseGroups(systemTraverseGroups, availableAdjustedHeights, availableRawHeights, AnchorChecker);
            UpdateArmDifferenceAccumulation(systemTraverseGroups, AnchorChecker);
        }

        /// <summary>
        /// Обрабатывает группы ходов одной системы
        /// </summary>
        private void ProcessSystemTraverseGroups(
            List<KeyValuePair<string, List<TraverseRow>>> systemTraverseGroups,
            Dictionary<string, double> availableAdjustedHeights,
            Dictionary<string, double> availableRawHeights,
            Func<string, bool> anchorChecker)
        {
            var processedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int iteration = 0; iteration < systemTraverseGroups.Count; iteration++)
            {
                bool progress = false;

                foreach (var group in systemTraverseGroups)
                {
                    if (processedGroups.Contains(group.Key))
                        continue;

                    var groupItems = group.Value;
                    bool hasAnchor = groupItems.Any(r =>
                        (!string.IsNullOrWhiteSpace(r.BackCode) && anchorChecker(r.BackCode!)) ||
                        (!string.IsNullOrWhiteSpace(r.ForeCode) && anchorChecker(r.ForeCode!)));

                    if (!hasAnchor && iteration < systemTraverseGroups.Count - 1)
                        continue;

                    if (!hasAnchor)
                    {
                        var firstCode = groupItems.Select(r => r.BackCode ?? r.ForeCode)
                            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                        if (!string.IsNullOrWhiteSpace(firstCode))
                        {
                            availableAdjustedHeights[firstCode!] = 0.0;
                            availableRawHeights[firstCode!] = 0.0;
                        }
                    }

                    _calculationService.ApplyCorrections(
                        groupItems,
                        anchorChecker,
                        MethodOrientationSign,
                        AdjustmentMode);
                    CalculateHeightsForRun(groupItems, availableAdjustedHeights, availableRawHeights, group.Key);
                    processedGroups.Add(group.Key);
                    progress = true;
                }

                if (!progress)
                    break;
            }
        }

        /// <summary>
        /// Обновляет статистику по строкам
        /// </summary>
        private void UpdateStatistics()
        {
            StationsCount = _rows.Count;
            TotalBackDistance = _rows.Sum(r => r.HdBack_m ?? 0);
            TotalForeDistance = _rows.Sum(r => r.HdFore_m ?? 0);
            TotalAverageDistance = StationsCount > 0
                ? (TotalBackDistance + TotalForeDistance) / 2.0
                : 0;
        }

        #endregion

        #region Incremental Updates

        /// <summary>
        /// Находит все ходы, содержащие указанную точку
        /// </summary>
        /// <param name="pointCode">Код точки</param>
        /// <returns>Список имён ходов</returns>
        private List<string> FindRunsContainingPoint(string pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return new List<string>();

            var affectedRuns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rows)
            {
                if (string.Equals(row.BackCode, pointCode, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.ForeCode, pointCode, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(row.LineName))
                    {
                        affectedRuns.Add(row.LineName);
                    }
                }
            }

            return affectedRuns.ToList();
        }

        /// <summary>
        /// Пересчитывает высоты только для указанных ходов (инкрементальное обновление)
        /// </summary>
        /// <param name="runNames">Список имён ходов для пересчёта</param>
        private void RecalculateRunsIncremental(IEnumerable<string> runNames)
        {
            if (runNames == null || !runNames.Any())
                return;

            var runNamesSet = new HashSet<string>(runNames, StringComparer.OrdinalIgnoreCase);

            // Получаем строки только затронутых ходов
            var affectedRows = _rows.Where(r => runNamesSet.Contains(r.LineName)).ToList();
            if (affectedRows.Count == 0)
                return;

            // Собираем известные высоты
            var knownHeights = new Dictionary<string, double>(_dataViewModel.KnownHeights, StringComparer.OrdinalIgnoreCase);

            // Добавляем высоты из связанных точек
            foreach (var sp in _sharedPoints.Where(p => p.IsEnabled))
            {
                if (_dataViewModel.KnownHeights.TryGetValue(sp.Code, out var h))
                {
                    knownHeights[sp.Code] = h;
                }
            }

            // Группируем по ходам
            var groupedRows = affectedRows.GroupBy(r => r.LineName);

            foreach (var group in groupedRows)
            {
                var runRows = group.OrderBy(r => r.Index).ToList();
                _calculationService.RecalculateHeights(
                    runRows,
                    code => knownHeights.TryGetValue(code, out var height) ? height : null);
            }

            // Обновляем статистику
            RecalculateClosure();
            UpdateTolerance();
        }

        #endregion

        private void RecalculateClosure()
        {
            if (StationsCount == 0)
            {
                Closure = null;
                return;
            }

            // Используем сервис для расчёта невязки
            Closure = _closureService.CalculateClosure(_rows.ToList(), MethodOrientationSign);
        }

        private void UpdateTolerance()
        {
            if (!Closure.HasValue || StationsCount == 0)
            {
                AllowableClosure = null;
                MethodTolerance = null;
                ClassTolerance = null;
                ClosureVerdict = StationsCount == 0 ? "Нет данных для расчёта." : "Выберите параметры расчёта.";
                return;
            }

            // Используем сервис для расчёта допусков
            MethodTolerance = TryCalculateTolerance(SelectedMethod);
            ClassTolerance = TryCalculateTolerance(SelectedClass);

            var toleranceCandidates = new[] { MethodTolerance, ClassTolerance }
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            AllowableClosure = toleranceCandidates.Count > 0 ? toleranceCandidates.Min() : (double?)null;

            // Генерируем вердикт через сервис
            ClosureVerdict = _closureService.GenerateVerdict(
                Closure,
                AllowableClosure,
                MethodTolerance,
                ClassTolerance,
                SelectedMethod?.Code,
                SelectedClass?.Code);
        }

        private bool IsAnchorAllowed(string? code, Func<string, bool> contains)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            return contains(code) && (_dataViewModel.HasKnownHeight(code) || _dataViewModel.IsSharedPointEnabled(code));
        }

        private bool AllowPropagation(string code)
        {
            return _dataViewModel.HasKnownHeight(code) || _dataViewModel.IsSharedPointEnabled(code);
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
                Runs.ToList(),
                _sharedPoints.ToList(),
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

        private double? TryCalculateTolerance(IToleranceOption? option)
        {
            if (option == null)
                return null;

            return option.Mode switch
            {
                ToleranceMode.SqrtStations => option.Coefficient * Math.Sqrt(Math.Max(StationsCount, 1)),
                ToleranceMode.SqrtLength => option.Coefficient * Math.Sqrt(Math.Max(TotalLengthKilometers, 1e-6)),
                _ => null
            };
        }

        /// <summary>
        /// Рассчитывает высоты точек внутри конкретного хода и добавляет их в словарь доступных высот
        /// с учётом локального уравнивания и уже пересчитанных смежных ходов.
        /// </summary>
        private void CalculateHeightsForRun(
            List<TraverseRow> items,
            Dictionary<string, double> availableAdjustedHeights,
            Dictionary<string, double> availableRawHeights,
            string runName)
        {
            if (items.Count == 0)
                return;

            // Фаза 1: Сброс предыдущих значений
            ResetRowHeights(items);

            // Фаза 2: Инициализация alias-менеджера для обработки повторяющихся точек
            var aliasManager = new RunAliasManager(code => IsAnchorAllowed(code, availableAdjustedHeights.ContainsKey));
            InitializeAliases(items, aliasManager);

            // Фаза 3: Инициализация локальных словарей высот
            var adjusted = new Dictionary<string, double>(availableAdjustedHeights, StringComparer.OrdinalIgnoreCase);
            var raw = new Dictionary<string, double>(availableRawHeights, StringComparer.OrdinalIgnoreCase);
            UpdateLocalHeightsForDisabledSharedPoints(items, runName, availableAdjustedHeights, availableRawHeights, adjusted, raw);

            // Фаза 4: Итеративное распространение высот
            PropagateHeightsIteratively(items, adjusted, raw, aliasManager);

            // Фаза 5: Присвоение уравненных высот строкам
            AssignAdjustedHeightsToRows(items, adjusted, availableAdjustedHeights, aliasManager);

            // Фаза 6: Расчёт Z0 (неуравненных) высот с историей визитов
            var heightTracker = new RunHeightTracker(raw, availableRawHeights);
            CalculateRawHeightsForwardPass(items, runName, heightTracker, aliasManager);
            CalculateRawHeightsBackwardPass(items, adjusted, heightTracker, aliasManager);
            UpdateVirtualStations(items, adjusted, raw, aliasManager);

            // Фаза 7: Обновление глобальных словарей доступных высот
            UpdateAvailableHeights(adjusted, availableAdjustedHeights, availableRawHeights, aliasManager, AllowPropagation);
        }

        /// <summary>
        /// Сбрасывает высоты для всех строк хода
        /// </summary>
        private static void ResetRowHeights(List<TraverseRow> items)
        {
            foreach (var row in items)
            {
                row.BackHeight = null;
                row.ForeHeight = null;
                row.BackHeightZ0 = null;
                row.ForeHeightZ0 = null;
                row.IsBackHeightKnown = false;
                row.IsForeHeightKnown = false;
            }
        }

        /// <summary>
        /// Инициализирует alias'ы для всех точек хода
        /// </summary>
        private static void InitializeAliases(List<TraverseRow> items, RunAliasManager aliasManager)
        {
            string? previousForeCode = null;

            foreach (var row in items)
            {
                if (!string.IsNullOrWhiteSpace(row.BackCode))
                {
                    var reusePrevious = previousForeCode != null &&
                        string.Equals(previousForeCode, row.BackCode, StringComparison.OrdinalIgnoreCase);
                    var alias = aliasManager.RegisterAlias(row.BackCode!, reusePrevious);
                    aliasManager.RegisterRowAlias(row, isBack: true, alias);
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode))
                {
                    var alias = aliasManager.RegisterAlias(row.ForeCode!, reusePrevious: false);
                    aliasManager.RegisterRowAlias(row, isBack: false, alias);
                    previousForeCode = row.ForeCode;
                }
                else
                {
                    aliasManager.ResetPreviousFore();
                    previousForeCode = null;
                }
            }
        }

        /// <summary>
        /// Обновляет локальные словари высот для отключённых общих точек
        /// </summary>
        private void UpdateLocalHeightsForDisabledSharedPoints(
            List<TraverseRow> items,
            string runName,
            Dictionary<string, double> availableAdjustedHeights,
            Dictionary<string, double> availableRawHeights,
            Dictionary<string, double> adjusted,
            Dictionary<string, double> raw)
        {
            var pointsInRun = items.SelectMany(r => new[] { r.BackCode, r.ForeCode })
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var pointCode in pointsInRun)
            {
                if (!_dataViewModel.IsSharedPointEnabled(pointCode!))
                {
                    var codeWithRun = _dataViewModel.GetPointCodeForRun(pointCode!, runName);
                    if (availableAdjustedHeights.TryGetValue(codeWithRun, out var adjValue))
                        adjusted[pointCode!] = adjValue;
                    if (availableRawHeights.TryGetValue(codeWithRun, out var rawValue))
                        raw[pointCode!] = rawValue;
                }
            }
        }

        /// <summary>
        /// Итеративно распространяет высоты внутри хода
        /// </summary>
        private static void PropagateHeightsIteratively(
            List<TraverseRow> items,
            Dictionary<string, double> adjusted,
            Dictionary<string, double> raw,
            RunAliasManager aliasManager)
        {
            const int maxIterations = 20;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool changedRaw = PropagateHeightsWithinRun(items, raw, useAdjusted: false, aliasManager.GetAlias);
                bool changedAdjusted = PropagateHeightsWithinRun(items, adjusted, useAdjusted: true, aliasManager.GetAlias);

                if (!changedRaw && !changedAdjusted)
                    break;
            }
        }

        /// <summary>
        /// Присваивает уравненные высоты строкам
        /// </summary>
        private void AssignAdjustedHeightsToRows(
            List<TraverseRow> items,
            Dictionary<string, double> adjusted,
            Dictionary<string, double> availableAdjustedHeights,
            RunAliasManager aliasManager)
        {
            foreach (var row in items)
            {
                var backAlias = aliasManager.GetAlias(row, isBack: true);
                if (!string.IsNullOrWhiteSpace(backAlias) && adjusted.TryGetValue(backAlias!, out var backZ))
                {
                    row.BackHeight = backZ;
                    row.IsBackHeightKnown = IsAnchorAllowed(row.BackCode, availableAdjustedHeights.ContainsKey);
                }

                var foreAlias = aliasManager.GetAlias(row, isBack: false);
                if (!string.IsNullOrWhiteSpace(foreAlias) && adjusted.TryGetValue(foreAlias!, out var foreZ))
                {
                    row.ForeHeight = foreZ;
                    row.IsForeHeightKnown = IsAnchorAllowed(row.ForeCode, availableAdjustedHeights.ContainsKey);
                }
            }
        }

        /// <summary>
        /// Прямой проход: расчёт Z0 высот от начала хода
        /// </summary>
        private void CalculateRawHeightsForwardPass(
            List<TraverseRow> items,
            string runName,
            RunHeightTracker heightTracker,
            RunAliasManager aliasManager)
        {
            foreach (var row in items)
            {
                var delta = row.DeltaH;
                var backAlias = aliasManager.GetAlias(row, isBack: true);
                var foreAlias = aliasManager.GetAlias(row, isBack: false);

                // Подхватываем текущее известное значение
                if (row.BackHeightZ0 == null)
                {
                    var existingBack = heightTracker.GetHeight(backAlias);
                    if (existingBack.HasValue)
                    {
                        row.BackHeightZ0 = existingBack;
                        heightTracker.RecordHeight(backAlias, existingBack.Value);
                    }
                }

                if (row.ForeHeightZ0 == null)
                {
                    var existingFore = heightTracker.GetHeight(foreAlias);
                    if (existingFore.HasValue)
                    {
                        row.ForeHeightZ0 = existingFore;
                        heightTracker.RecordHeight(foreAlias, existingFore.Value);
                    }
                }

                if (!delta.HasValue)
                    continue;

                var backHeight = row.BackHeightZ0 ?? heightTracker.GetHeight(backAlias);
                var foreHeight = row.ForeHeightZ0 ?? heightTracker.GetHeight(foreAlias);

                if (backHeight.HasValue)
                {
                    var computedFore = backHeight.Value + delta.Value;
                    row.ForeHeightZ0 = computedFore;
                    heightTracker.RecordHeight(foreAlias, computedFore);
                }
                else if (foreHeight.HasValue)
                {
                    var computedBack = foreHeight.Value - delta.Value;
                    row.BackHeightZ0 = computedBack;
                    heightTracker.RecordHeight(backAlias, computedBack);
                }

                // Фиксируем значения для повторных точек
                if (backHeight.HasValue && !heightTracker.HasHistory(backAlias))
                    heightTracker.RecordHeight(backAlias, backHeight.Value);
                if (foreHeight.HasValue && !heightTracker.HasHistory(foreAlias))
                    heightTracker.RecordHeight(foreAlias, foreHeight.Value);
            }
        }

        /// <summary>
        /// Обратный проход: распространение Z0 высот от конца хода
        /// </summary>
        private static void CalculateRawHeightsBackwardPass(
            List<TraverseRow> items,
            Dictionary<string, double> adjusted,
            RunHeightTracker heightTracker,
            RunAliasManager aliasManager)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var row = items[i];
                var delta = row.DeltaH;
                var adjustedDelta = row.AdjustedDeltaH ?? row.DeltaH;

                if (!delta.HasValue)
                    continue;

                var backAlias = aliasManager.GetAlias(row, isBack: true);
                var foreAlias = aliasManager.GetAlias(row, isBack: false);

                if (row.ForeHeightZ0.HasValue && !row.BackHeightZ0.HasValue)
                {
                    var computedBack = row.ForeHeightZ0.Value - delta.Value;
                    row.BackHeightZ0 = computedBack;
                    heightTracker.RecordHeight(backAlias, computedBack);

                    if (!string.IsNullOrWhiteSpace(backAlias) && !adjusted.ContainsKey(backAlias!))
                    {
                        var foreAdjusted = row.ForeHeight ?? row.ForeHeightZ0;
                        if (foreAdjusted.HasValue && adjustedDelta.HasValue)
                        {
                            var computedBackAdj = foreAdjusted.Value - adjustedDelta.Value;
                            adjusted[backAlias!] = computedBackAdj;
                            row.BackHeight ??= computedBackAdj;
                        }
                    }
                }
                else if (row.BackHeightZ0.HasValue && !row.ForeHeightZ0.HasValue)
                {
                    var computedFore = row.BackHeightZ0.Value + delta.Value;
                    row.ForeHeightZ0 = computedFore;
                    heightTracker.RecordHeight(foreAlias, computedFore);

                    if (!string.IsNullOrWhiteSpace(foreAlias) && !adjusted.ContainsKey(foreAlias!))
                    {
                        var backAdjusted = row.BackHeight ?? row.BackHeightZ0;
                        if (backAdjusted.HasValue && adjustedDelta.HasValue)
                        {
                            var computedForeAdj = backAdjusted.Value + adjustedDelta.Value;
                            adjusted[foreAlias!] = computedForeAdj;
                            row.ForeHeight ??= computedForeAdj;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет виртуальные станции (первая точка хода без DeltaH)
        /// </summary>
        private static void UpdateVirtualStations(
            List<TraverseRow> items,
            Dictionary<string, double> adjusted,
            Dictionary<string, double> raw,
            RunAliasManager aliasManager)
        {
            foreach (var row in items)
            {
                if (!row.DeltaH.HasValue && !string.IsNullOrWhiteSpace(row.BackCode))
                {
                    var backAlias = aliasManager.GetAlias(row, isBack: true);
                    if (!string.IsNullOrWhiteSpace(backAlias) && adjusted.TryGetValue(backAlias!, out var height))
                    {
                        row.BackHeight ??= height;
                        row.BackHeightZ0 ??= raw.TryGetValue(backAlias!, out var rawH) ? rawH : height;
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет глобальные словари доступных высот
        /// </summary>
        private static void UpdateAvailableHeights(
            Dictionary<string, double> adjusted,
            Dictionary<string, double> availableAdjustedHeights,
            Dictionary<string, double> availableRawHeights,
            RunAliasManager aliasManager,
            Func<string, bool> allowPropagation)
        {
            foreach (var kvp in adjusted)
            {
                if (!aliasManager.IsCopyAlias(kvp.Key) && allowPropagation(kvp.Key))
                {
                    availableAdjustedHeights[kvp.Key] = kvp.Value;
                    availableRawHeights[kvp.Key] = kvp.Value;
                }
            }
        }

        private static bool PropagateHeightsWithinRun(
            List<TraverseRow> sections,
            Dictionary<string, double> heights,
            bool useAdjusted,
            Func<TraverseRow, bool, string?> aliasSelector)
        {
            bool changed = false;

            foreach (var section in sections)
            {
                var delta = useAdjusted ? section.AdjustedDeltaH : section.DeltaH;
                if (!delta.HasValue)
                    continue;

                var backCode = aliasSelector(section, true);
                var foreCode = aliasSelector(section, false);

                if (string.IsNullOrWhiteSpace(backCode) || string.IsNullOrWhiteSpace(foreCode))
                    continue;

                if (heights.TryGetValue(backCode, out var backHeight) && !heights.ContainsKey(foreCode))
                {
                    heights[foreCode] = backHeight + delta.Value;
                    changed = true;
                }
                else if (heights.TryGetValue(foreCode, out var foreHeight) && !heights.ContainsKey(backCode))
                {
                    heights[backCode] = foreHeight - delta.Value;
                    changed = true;
                }
            }

            return changed;
        }



        /// <summary>
        /// Обновляет накопление разности плеч для каждого хода на основе TraverseRow
        /// Обновляет существующие LineSummary в DataViewModel.Runs
        /// </summary>
        private void UpdateArmDifferenceAccumulation(
            List<KeyValuePair<string, List<TraverseRow>>> traverseGroups,
            Func<string, bool> isAnchor)
        {
            foreach (var group in traverseGroups)
            {
                var lineName = group.Key;
                var rows = group.Value;

                // Находим индекс существующего LineSummary
                var existingIndex = -1;
                LineSummary? existingSummary = null;
                for (int i = 0; i < _dataViewModel.Runs.Count; i++)
                {
                    if (_dataViewModel.Runs[i].DisplayName == lineName)
                    {
                        existingIndex = i;
                        existingSummary = _dataViewModel.Runs[i];
                        break;
                    }
                }

                if (existingSummary == null)
                    continue;

                // Вычисляем накопление разности плеч и длины для этого хода
                double? accumulation = null;
                double? totalDistanceBack = null;
                double? totalDistanceFore = null;

                foreach (var row in rows)
                {
                    if (row.ArmDifference_m.HasValue)
                    {
                        accumulation = (accumulation ?? 0) + row.ArmDifference_m.Value;
                    }

                    if (row.HdBack_m.HasValue)
                    {
                        totalDistanceBack = (totalDistanceBack ?? 0) + row.HdBack_m.Value;
                    }

                    if (row.HdFore_m.HasValue)
                    {
                        totalDistanceFore = (totalDistanceFore ?? 0) + row.HdFore_m.Value;
                    }
                }

                // Подсчитываем количество известных точек в этом ходе
                var knownPointsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    if (!string.IsNullOrWhiteSpace(row.BackCode) && isAnchor(row.BackCode))
                    {
                        knownPointsSet.Add(row.BackCode);
                    }
                    if (!string.IsNullOrWhiteSpace(row.ForeCode) && isAnchor(row.ForeCode))
                    {
                        knownPointsSet.Add(row.ForeCode);
                    }
                }
                var knownPointsCount = knownPointsSet.Count;

                // Обновляем существующий LineSummary вместо создания нового
                existingSummary.TotalDistanceBack = totalDistanceBack;
                existingSummary.TotalDistanceFore = totalDistanceFore;
                existingSummary.ArmDifferenceAccumulation = accumulation;
                existingSummary.KnownPointsCount = knownPointsCount;

                if (_sharedPointsByRun.TryGetValue(existingSummary.Index, out var sharedCodesForRun))
                {
                    existingSummary.SetSharedPoints(sharedCodesForRun);
                }
                else
                {
                    existingSummary.SetSharedPoints(Array.Empty<string>());
                }
            }
        }

        /// <summary>
        /// Проверяет допуски разности плеч на станциях и накопление за ход
        /// </summary>
        private void CheckArmDifferenceTolerances()
        {
            if (SelectedClass == null)
                return;

            var stationTolerance = SelectedClass.ArmDifferenceToleranceStation;
            var accumulationTolerance = SelectedClass.ArmDifferenceToleranceAccumulation;

            // Проверка разности плеч на каждой станции
            foreach (var row in _rows)
            {
                if (row.ArmDifference_m.HasValue)
                {
                    row.IsArmDifferenceExceeded = Math.Abs(row.ArmDifference_m.Value) > stationTolerance;
                }
                else
                {
                    row.IsArmDifferenceExceeded = false;
                }
            }

            // Проверка накопления разности плеч за ходы
            var lineGroups = _rows.GroupBy(r => r.LineName);
            foreach (var group in lineGroups)
            {
                var lineName = group.Key;
                var lineSummary = _dataViewModel.Runs.FirstOrDefault(r => r.DisplayName == lineName);

                if (lineSummary != null && lineSummary.ArmDifferenceAccumulation.HasValue)
                {
                    lineSummary.IsArmDifferenceAccumulationExceeded =
                        Math.Abs(lineSummary.ArmDifferenceAccumulation.Value) > accumulationTolerance;
                }
            }

            // Принудительное обновление отображения через сброс коллекции
            RefreshRowsDisplay();
        }

        /// <summary>
        /// Обновляет отображение строк в DataGrid путём принудительного обновления представления коллекции
        /// </summary>
        private void RefreshRowsDisplay()
        {
            // Используем CollectionView.Refresh() вместо пересоздания коллекции - это эффективнее
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_rows);
            view?.Refresh();
        }
    }

    public enum ToleranceMode
    {
        SqrtStations,
        SqrtLength
    }

    public interface IToleranceOption
    {
        string Code { get; }
        string Description { get; }
        ToleranceMode Mode { get; }
        double Coefficient { get; }
        string Display { get; }
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

        /// <summary>
        /// Допуск разности плеч на станции (в метрах)
        /// </summary>
        public double ArmDifferenceToleranceStation => ArmDiffStation;

        /// <summary>
        /// Допуск накопления разности плеч за ход (в метрах)
        /// </summary>
        public double ArmDifferenceToleranceAccumulation => ArmDiffAccumulation;
    }
}
