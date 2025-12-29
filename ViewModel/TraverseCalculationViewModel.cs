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
using Nivtropy.Models;
using Nivtropy.Services;
using Nivtropy.Services.Export;
using Nivtropy.ViewModels.Base;

namespace Nivtropy.ViewModels
{
    public class TraverseCalculationViewModel : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ITraverseBuilder _traverseBuilder;
        private readonly IExportService _exportService;
        private readonly ObservableCollection<TraverseRow> _rows = new();
        private readonly ObservableCollection<PointItem> _availablePoints = new();
        private readonly ObservableCollection<BenchmarkItem> _benchmarks = new();
        private readonly ObservableCollection<SharedPointLinkItem> _sharedPoints = new();
        private readonly ObservableCollection<TraverseSystem> _systems = new();
        private readonly Dictionary<string, SharedPointLinkItem> _sharedPointLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _benchmarkSystems = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<int, List<string>> _sharedPointsByRun = new();

        // ID системы по умолчанию
        private const string DEFAULT_SYSTEM_ID = "system-default";
        private const string DEFAULT_SYSTEM_NAME = "Основная";

        // Методы нивелирования для двойного хода
        // Допуск: 4 мм × √n, где n - число станций
        private readonly LevelingMethodOption[] _methods =
        {
            new("BF", "Двойной ход (Back → Forward)", ToleranceMode.SqrtStations, 0.004, 1.0),
            new("FB", "Двойной ход (Forward → Back)", ToleranceMode.SqrtStations, 0.004, -1.0)
        };

        // Классы нивелирования согласно ГКИНП 03-010-02
        // Допуск невязки: коэффициент × √L, где L - длина хода в км (в один конец)
        // Допуски разности плеч: на станции и накопление за ход
        private readonly LevelingClassOption[] _classes =
        {
            new("I", "Класс I: 4 мм · √L", ToleranceMode.SqrtLength, 0.004, ArmDiffStation: 0.5, ArmDiffAccumulation: 1.0),
            new("II", "Класс II: 8 мм · √L", ToleranceMode.SqrtLength, 0.008, ArmDiffStation: 1.0, ArmDiffAccumulation: 2.0),
            new("III", "Класс III: 10 мм · √L", ToleranceMode.SqrtLength, 0.010, ArmDiffStation: 2.0, ArmDiffAccumulation: 5.0),
            new("IV", "Класс IV: 20 мм · √L", ToleranceMode.SqrtLength, 0.020, ArmDiffStation: 5.0, ArmDiffAccumulation: 10.0),
            new("Техническое", "Техническое: 50 мм · √L", ToleranceMode.SqrtLength, 0.050, ArmDiffStation: 10.0, ArmDiffAccumulation: 20.0)
        };

        private LevelingMethodOption? _selectedMethod;
        private LevelingClassOption? _selectedClass;
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

        public TraverseCalculationViewModel(DataViewModel dataViewModel, SettingsViewModel settingsViewModel, ITraverseBuilder traverseBuilder, IExportService exportService)
        {
            _dataViewModel = dataViewModel;
            _settingsViewModel = settingsViewModel;
            _traverseBuilder = traverseBuilder;
            _exportService = exportService;
            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += OnRecordsCollectionChanged;
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            // Используем базовый класс для batch updates
            SubscribeToBatchUpdates(_dataViewModel);

            _selectedMethod = _methods.FirstOrDefault();
            _selectedClass = _classes.FirstOrDefault();

            // Инициализация системы по умолчанию
            var defaultSystem = new TraverseSystem(DEFAULT_SYSTEM_ID, DEFAULT_SYSTEM_NAME, order: 0);
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
                    // При смене системы обновляем список доступных реперов
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
            _benchmarkSystems[SelectedPoint.Code] = SelectedSystem?.Id ?? DEFAULT_SYSTEM_ID;

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
        /// </summary>
        private void UpdateAvailablePoints()
        {
            _availablePoints.Clear();

            var pointsDict = new Dictionary<string, PointItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rows)
            {
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
                .ThenBy(p => ParsePointCode(p.Code).isNumeric ? 0 : 1)
                .ThenBy(p => ParsePointCode(p.Code).number)
                .ThenBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var point in sortedPoints)
            {
                _availablePoints.Add(point);
            }
        }

        private static (bool isNumeric, double number) ParsePointCode(string code)
        {
            if (double.TryParse(code, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return (true, value);
            }

            return (false, double.NaN);
        }

        /// <summary>
        /// Обновляет список реперов из DataViewModel с фильтрацией по выбранной системе
        /// </summary>
        private void UpdateBenchmarks()
        {
            _benchmarks.Clear();

            var selectedSystemId = SelectedSystem?.Id;

            foreach (var kvp in _dataViewModel.KnownHeights
                             .OrderBy(k => ParsePointCode(k.Key).isNumeric ? 0 : 1)
                             .ThenBy(k => ParsePointCode(k.Key).number)
                             .ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Определяем систему для этого репера
                if (!_benchmarkSystems.TryGetValue(kvp.Key, out var benchmarkSystemId))
                {
                    // Если репер еще не привязан к системе, привязываем к системе по умолчанию
                    benchmarkSystemId = DEFAULT_SYSTEM_ID;
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
            _rows.Clear();

            var records = _dataViewModel.Records;

            if (records.Count == 0)
            {
                Closure = null;
                AllowableClosure = null;
                ClosureVerdict = "Нет данных для расчёта.";
                StationsCount = 0;
                TotalBackDistance = 0;
                TotalForeDistance = 0;
                TotalAverageDistance = 0;
                MethodTolerance = null;
                ClassTolerance = null;
                return;
            }

            var items = _traverseBuilder.Build(records);

            // Группируем станции по ходам для корректного расчета поправок
            // Используем ToDictionary для O(1) доступа вместо повторного перебора
            var traverseGroupsDict = items.GroupBy(r => r.LineName)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Создаём lookup для быстрого поиска ходов по имени (оптимизация LINQ)
            var runsLookup = Runs.ToDictionary(r => r.DisplayName, r => r, StringComparer.OrdinalIgnoreCase);

            // ВАЖНО: Сначала обновляем метаданные общих точек, затем инициализируем системы
            // Это необходимо, т.к. RebuildSystemsByConnectivity() использует _sharedPoints
            UpdateSharedPointsMetadata(_dataViewModel.Records);

            // Инициализация систем для новых ходов (использует актуальные _sharedPoints)
            InitializeRunSystems();

            // Обрабатываем каждую систему отдельно с независимыми пространствами высот
            foreach (var system in _systems.OrderBy(s => s.Order))
            {
                // Получаем группы ходов для текущей системы (только активные)
                var systemTraverseGroups = traverseGroupsDict.Where(kvp =>
                {
                    runsLookup.TryGetValue(kvp.Key, out var run);
                    return run != null && run.SystemId == system.Id && run.IsActive;
                }).ToList();

                if (systemTraverseGroups.Count == 0)
                    continue;

                // Высоты для текущей системы (изолированные от других систем)
                var availableAdjustedHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var availableRawHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                // Добавляем только реперы текущей системы
                foreach (var kvp in _dataViewModel.KnownHeights)
                {
                    if (_benchmarkSystems.TryGetValue(kvp.Key, out var benchSystemId) && benchSystemId == system.Id)
                    {
                        availableAdjustedHeights[kvp.Key] = kvp.Value;
                        availableRawHeights[kvp.Key] = kvp.Value;
                    }
                }

                bool AnchorChecker(string code) => IsAnchorAllowed(code, availableAdjustedHeights.ContainsKey);

                var processedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Обрабатываем ходы системы
                for (int iteration = 0; iteration < systemTraverseGroups.Count; iteration++)
                {
                    bool progress = false;

                    foreach (var group in systemTraverseGroups)
                    {
                        if (processedGroups.Contains(group.Key))
                            continue;

                        var groupItems = group.Value;
                        bool hasAnchor = groupItems.Any(r =>
                            (!string.IsNullOrWhiteSpace(r.BackCode) && AnchorChecker(r.BackCode!)) ||
                            (!string.IsNullOrWhiteSpace(r.ForeCode) && AnchorChecker(r.ForeCode!)));

                        if (!hasAnchor && iteration < systemTraverseGroups.Count - 1)
                            continue;

                        // Если к этому моменту высот нет ни у одной точки ни одного хода в системе,
                        // стартуем первый обработанный ход с 0 в первой точке
                        if (!hasAnchor)
                        {
                            var firstCode = groupItems.Select(r => r.BackCode ?? r.ForeCode)
                                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                            if (!string.IsNullOrWhiteSpace(firstCode))
                            {
                                availableAdjustedHeights[firstCode!] = 0.0;
                                availableRawHeights[firstCode!] = 0.0;
                                hasAnchor = true;
                            }
                        }

                        CalculateCorrections(groupItems, AnchorChecker);
                        CalculateHeightsForRun(groupItems, availableAdjustedHeights, availableRawHeights, group.Key);
                        processedGroups.Add(group.Key);
                        progress = true;
                    }

                    if (!progress)
                        break;
                }

                // Обновляем накопление разности плеч для ходов текущей системы
                UpdateArmDifferenceAccumulation(systemTraverseGroups, AnchorChecker);
            }

            // Добавляем в таблицу только строки из активных ходов
            // Используем runsLookup для O(1) доступа вместо O(n) FirstOrDefault
            foreach (var row in items)
            {
                if (runsLookup.TryGetValue(row.LineName, out var run) && run.IsActive)
                {
                    _rows.Add(row);
                }
            }

            // Статистика считается только по активным ходам
            StationsCount = _rows.Count;
            TotalBackDistance = _rows.Sum(r => r.HdBack_m ?? 0);
            TotalForeDistance = _rows.Sum(r => r.HdFore_m ?? 0);
            TotalAverageDistance = StationsCount > 0
                ? (TotalBackDistance + TotalForeDistance) / 2.0
                : 0;

            // Обновляем списки доступных точек и реперов
            UpdateAvailablePoints();
            UpdateBenchmarks();

            RecalculateClosure();
            UpdateTolerance();
            CheckArmDifferenceTolerances();
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
                    await Application.Current.Dispatcher.InvokeAsync(() =>
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
                    return _traverseBuilder.Build(records);
                }, token);

                token.ThrowIfCancellationRequested();

                // Обновление UI в главном потоке
                await Application.Current.Dispatcher.InvokeAsync(() =>
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
            _rows.Clear();

            // Группируем станции по ходам для корректного расчета поправок
            var traverseGroupsDict = items.GroupBy(r => r.LineName)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var runsLookup = Runs.ToDictionary(r => r.DisplayName, r => r, StringComparer.OrdinalIgnoreCase);

            // ВАЖНО: Сначала обновляем метаданные общих точек, затем инициализируем системы
            UpdateSharedPointsMetadata(_dataViewModel.Records);
            InitializeRunSystems();

            foreach (var system in _systems.OrderBy(s => s.Order))
            {
                var systemTraverseGroups = traverseGroupsDict.Where(kvp =>
                {
                    runsLookup.TryGetValue(kvp.Key, out var run);
                    return run != null && run.SystemId == system.Id && run.IsActive;
                }).ToList();

                if (systemTraverseGroups.Count == 0)
                    continue;

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
                            (!string.IsNullOrWhiteSpace(r.BackCode) && AnchorChecker(r.BackCode!)) ||
                            (!string.IsNullOrWhiteSpace(r.ForeCode) && AnchorChecker(r.ForeCode!)));

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
                                hasAnchor = true;
                            }
                        }

                        CalculateCorrections(groupItems, AnchorChecker);
                        CalculateHeightsForRun(groupItems, availableAdjustedHeights, availableRawHeights, group.Key);
                        processedGroups.Add(group.Key);
                        progress = true;
                    }

                    if (!progress)
                        break;
                }

                UpdateArmDifferenceAccumulation(systemTraverseGroups, AnchorChecker);
            }

            foreach (var row in items)
            {
                if (runsLookup.TryGetValue(row.LineName, out var run) && run.IsActive)
                {
                    _rows.Add(row);
                }
            }

            StationsCount = _rows.Count;
            TotalBackDistance = _rows.Sum(r => r.HdBack_m ?? 0);
            TotalForeDistance = _rows.Sum(r => r.HdFore_m ?? 0);
            TotalAverageDistance = StationsCount > 0
                ? (TotalBackDistance + TotalForeDistance) / 2.0
                : 0;

            UpdateAvailablePoints();
            UpdateBenchmarks();
            RecalculateClosure();
            UpdateTolerance();
            CheckArmDifferenceTolerances();
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
                RecalculateHeightsForRunInternal(runRows, knownHeights);
            }

            // Обновляем статистику
            RecalculateClosure();
            UpdateTolerance();
        }

        /// <summary>
        /// Внутренний метод пересчёта высот для одного хода
        /// </summary>
        private void RecalculateHeightsForRunInternal(List<TraverseRow> runRows, Dictionary<string, double> knownHeights)
        {
            if (runRows.Count == 0)
                return;

            double? runningHeight = null;
            double? runningHeightZ0 = null;

            foreach (var row in runRows)
            {
                // Проверяем известную высоту задней точки
                if (!string.IsNullOrEmpty(row.BackCode) &&
                    knownHeights.TryGetValue(row.BackCode, out var backKnownHeight))
                {
                    runningHeight = backKnownHeight;
                    runningHeightZ0 = backKnownHeight;
                }

                // Устанавливаем высоту задней точки
                if (runningHeight.HasValue)
                {
                    row.BackHeight = runningHeight;
                    row.BackHeightZ0 = runningHeightZ0;
                }

                // Рассчитываем высоту передней точки
                if (runningHeight.HasValue && row.AdjustedDeltaH.HasValue)
                {
                    row.ForeHeight = runningHeight.Value + row.AdjustedDeltaH.Value;
                    runningHeight = row.ForeHeight;
                }

                if (runningHeightZ0.HasValue && row.DeltaH.HasValue)
                {
                    row.ForeHeightZ0 = runningHeightZ0.Value + row.DeltaH.Value;
                    runningHeightZ0 = row.ForeHeightZ0;
                }

                // Проверяем известную высоту передней точки
                if (!string.IsNullOrEmpty(row.ForeCode) &&
                    knownHeights.TryGetValue(row.ForeCode, out var foreKnownHeight))
                {
                    row.ForeHeight = foreKnownHeight;
                    runningHeight = foreKnownHeight;
                }
            }
        }

        #endregion

        private void RecalculateClosure()
        {
            if (StationsCount == 0)
            {
                Closure = null;
                return;
            }

            // Невязка = сумма измеренных превышений
            // Знак ориентации определяет направление хода (прямой +1, обратный -1)
            var sign = MethodOrientationSign;
            var orientedClosure = _rows
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value * sign);

            Closure = orientedClosure;
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

            MethodTolerance = TryCalculateTolerance(SelectedMethod);
            ClassTolerance = TryCalculateTolerance(SelectedClass);

            var toleranceCandidates = new[] { MethodTolerance, ClassTolerance }
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            AllowableClosure = toleranceCandidates.Count > 0 ? toleranceCandidates.Min() : (double?)null;

            var absClosure = Math.Abs(Closure.Value);

            if (!AllowableClosure.HasValue)
            {
                ClosureVerdict = "Выберите метод или класс для оценки допуска.";
                return;
            }

            var verdict = absClosure <= AllowableClosure.Value
                ? "Общий вывод: в пределах допуска."
                : "Общий вывод: превышение допуска!";

            var details = new List<string>();

            if (MethodTolerance.HasValue && SelectedMethod != null)
            {
                details.Add(absClosure <= MethodTolerance.Value
                    ? $"Метод {SelectedMethod.Code}: в норме."
                    : $"Метод {SelectedMethod.Code}: превышение." );
            }

            if (ClassTolerance.HasValue && SelectedClass != null)
            {
                details.Add(absClosure <= ClassTolerance.Value
                    ? $"Класс {SelectedClass.Code}: в норме."
                    : $"Класс {SelectedClass.Code}: превышение." );
            }

            ClosureVerdict = details.Count > 0
                ? string.Join(" ", new[] { verdict }.Concat(details))
                : verdict;
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
                .OrderBy(p => ParsePointCode(p.Code).isNumeric ? 0 : 1)
                .ThenBy(p => ParsePointCode(p.Code).number)
                .ThenBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
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

            var defaultSystem = _systems.FirstOrDefault(s => s.Id == DEFAULT_SYSTEM_ID);
            if (defaultSystem == null)
                return;

            foreach (var run in Runs)
            {
                // Если ход еще не привязан к системе, привязываем к системе по умолчанию
                if (string.IsNullOrEmpty(run.SystemId))
                {
                    run.SystemId = DEFAULT_SYSTEM_ID;
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

            // Строим граф связности: ход -> список связанных ходов через включённые общие точки
            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (var run in Runs)
            {
                adjacency[run.Index] = new HashSet<int>();
            }

            // Для каждой включённой общей точки добавляем рёбра между ходами
            foreach (var sp in _sharedPoints.Where(p => p.IsEnabled))
            {
                var runsWithPoint = Runs
                    .Where(r => sp.IsUsedInRun(r.Index))
                    .Select(r => r.Index)
                    .ToList();

                // Связываем все ходы, использующие эту точку
                for (int i = 0; i < runsWithPoint.Count; i++)
                {
                    for (int j = i + 1; j < runsWithPoint.Count; j++)
                    {
                        adjacency[runsWithPoint[i]].Add(runsWithPoint[j]);
                        adjacency[runsWithPoint[j]].Add(runsWithPoint[i]);
                    }
                }
            }

            // Находим компоненты связности через BFS
            var visited = new HashSet<int>();
            var components = new List<List<int>>();

            foreach (var run in Runs)
            {
                if (visited.Contains(run.Index))
                    continue;

                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(run.Index);
                visited.Add(run.Index);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    foreach (var neighbor in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                components.Add(component);
            }

            // Если все ходы в одной компоненте - все в основной системе
            if (components.Count <= 1)
            {
                foreach (var run in Runs)
                {
                    run.SystemId = DEFAULT_SYSTEM_ID;
                }
                return;
            }

            // Назначаем системы для каждой компоненты
            // Первая компонента остаётся в основной системе
            var sortedComponents = components.OrderByDescending(c => c.Count).ToList();

            for (int i = 0; i < sortedComponents.Count; i++)
            {
                var component = sortedComponents[i];
                string systemId;

                if (i == 0)
                {
                    // Самая большая компонента - в основную систему
                    systemId = DEFAULT_SYSTEM_ID;
                }
                else
                {
                    // Создаём или находим дополнительную систему
                    systemId = $"system-auto-{i}";
                    var existingSystem = _systems.FirstOrDefault(s => s.Id == systemId);
                    if (existingSystem == null)
                    {
                        var newSystem = new TraverseSystem(systemId, $"Система {i + 1}", i + 1);
                        _systems.Add(newSystem);
                    }
                }

                // Назначаем ходам систему
                foreach (var runIndex in component)
                {
                    var run = Runs.FirstOrDefault(r => r.Index == runIndex);
                    if (run != null)
                    {
                        run.SystemId = systemId;
                    }
                }
            }

            // Удаляем пустые автосистемы
            var emptyAutoSystems = _systems
                .Where(s => s.Id.StartsWith("system-auto-") && !Runs.Any(r => r.SystemId == s.Id))
                .ToList();
            foreach (var sys in emptyAutoSystems)
            {
                _systems.Remove(sys);
            }
        }

        public List<SharedPointLinkItem> GetSharedPointsForRun(LineSummary? run)
        {
            if (run == null)
                return new List<SharedPointLinkItem>();

            return _sharedPoints
                .Where(p => p.IsUsedInRun(run.Index))
                .OrderBy(p => ParsePointCode(p.Code).isNumeric ? 0 : 1)
                .ThenBy(p => ParsePointCode(p.Code).number)
                .ThenBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
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
        /// Рассчитывает поправки для распределения невязки пропорционально длинам станций
        /// Невязка рассчитывается для конкретного хода (группы станций)
        ///
        /// Поддерживает два режима:
        /// 1. Обычный - невязка распределяется по всему ходу
        /// 2. Локальное уравнивание - ход разбивается на секции между известными точками,
        ///    и каждая секция уравнивается отдельно
        /// </summary>
        private void CalculateCorrections(List<TraverseRow> items, Func<string, bool> isAnchor)
        {
            if (items.Count == 0)
                return;

            ResetCorrections(items);

            var closures = new List<double>();
            var lineSummary = items.FirstOrDefault()?.LineSummary;
            var anchorPoints = CollectAnchorPoints(items, isAnchor);
            var distinctAnchorCount = anchorPoints
                .Select(a => a.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (lineSummary != null)
            {
                lineSummary.KnownPointsCount = distinctAnchorCount;
            }

            var closureMode = DetermineClosureMode(items, isAnchor, anchorPoints, lineSummary);

            switch (closureMode)
            {
                case TraverseClosureMode.Open:
                    break;

                case TraverseClosureMode.Simple:
                    {
                        var closure = CalculateCorrectionsForSection(
                            items,
                            (row, value) =>
                            {
                                row.Correction = value;
                                row.BaselineCorrection = value;
                                row.CorrectionMode = CorrectionDisplayMode.Single;
                            });

                        if (closure.HasValue)
                        {
                            closures.Add(closure.Value);
                        }
                        break;
                    }

                case TraverseClosureMode.Local:
                    {
                        // Базовая поправка для наглядности — как если бы уравнивали весь ход целиком
                        CalculateCorrectionsForSection(items, (row, value) => row.BaselineCorrection = value);

                        // Фактическое локальное уравнивание по секциям
                        CalculateCorrectionsWithSections(
                            items,
                            anchorPoints,
                            closures,
                            (row, value) =>
                            {
                                row.Correction = value;
                                row.CorrectionMode = CorrectionDisplayMode.Local;
                            });

                        break;
                    }
            }

            if (lineSummary != null)
            {
                if (closures.Count == 0)
                {
                    var orientedClosure = items
                        .Where(r => r.DeltaH.HasValue)
                        .Sum(r => r.DeltaH!.Value * MethodOrientationSign);

                    closures.Add(orientedClosure);
                }

                lineSummary.SetClosures(closures);
            }
        }

        private static void ResetCorrections(List<TraverseRow> items)
        {
            foreach (var row in items)
            {
                row.Correction = null;
                row.BaselineCorrection = null;
                row.CorrectionMode = CorrectionDisplayMode.None;
            }
        }

        private static List<(int Index, string Code)> CollectAnchorPoints(List<TraverseRow> items, Func<string, bool> isAnchor)
        {
            var knownPoints = new List<(int Index, string Code)>();

            for (int i = 0; i < items.Count; i++)
            {
                var row = items[i];

                if (!string.IsNullOrWhiteSpace(row.BackCode) && isAnchor(row.BackCode))
                {
                    if (knownPoints.All(p => p.Index != i))
                        knownPoints.Add((i, row.BackCode));
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode) && isAnchor(row.ForeCode))
                {
                    var anchorIndex = Math.Min(i + 1, items.Count);
                    if (knownPoints.All(p => p.Index != anchorIndex))
                        knownPoints.Add((anchorIndex, row.ForeCode));
                }
            }

            return knownPoints.OrderBy(p => p.Index).ToList();
        }

        private TraverseClosureMode DetermineClosureMode(
            List<TraverseRow> items,
            Func<string, bool> isAnchor,
            List<(int Index, string Code)> anchorPoints,
            LineSummary? lineSummary)
        {
            var startCode = items.FirstOrDefault()?.BackCode ?? items.FirstOrDefault()?.ForeCode;
            var endCode = items.LastOrDefault()?.ForeCode ?? items.LastOrDefault()?.BackCode;

            bool startKnown = !string.IsNullOrWhiteSpace(startCode) && isAnchor(startCode!);
            bool endKnown = !string.IsNullOrWhiteSpace(endCode) && isAnchor(endCode!);

            // Циклический ход определяется по совпадению кодов начала и конца,
            // независимо от наличия известной высоты
            bool closesByLoop = !string.IsNullOrWhiteSpace(startCode)
                && !string.IsNullOrWhiteSpace(endCode)
                && string.Equals(startCode, endCode, StringComparison.OrdinalIgnoreCase);

            // Ход замкнут, если:
            // - Циклический (замыкается на себя), ИЛИ
            // - Начальная и конечная точки обе известны
            bool isClosed = closesByLoop || (startKnown && endKnown);
            if (!isClosed)
            {
                return TraverseClosureMode.Open;
            }

            var distinctAnchorCount = anchorPoints
                .Select(a => a.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            // Локальное уравнивание только если больше одной различной известной точки
            if (distinctAnchorCount > 1 || (lineSummary?.UseLocalAdjustment ?? false))
                return TraverseClosureMode.Local;

            return TraverseClosureMode.Simple;
        }

        /// <summary>
        /// Локальное уравнивание: разбивает ход на секции и уравнивает каждую отдельно
        /// </summary>
        private void CalculateCorrectionsWithSections(
            List<TraverseRow> items,
            List<(int Index, string Code)> knownPoints,
            List<double> closures,
            Action<TraverseRow, double>? applyCorrection)
        {
            if (items.Count == 0)
                return;

            if (knownPoints.Count < 2)
            {
                // Недостаточно известных точек для секций, используем обычное уравнивание
                var closure = CalculateCorrectionsForSection(items, applyCorrection);
                if (closure.HasValue)
                    closures.Add(closure.Value);
                return;
            }

            // Разбиваем на секции между известными точками
            for (int i = 0; i < knownPoints.Count - 1; i++)
            {
                int startIdx = knownPoints[i].Index;
                int endIdx = knownPoints[i + 1].Index;

                // Секция включает станции от startIdx до endIdx (не включая startIdx, т.к. это виртуальная станция начала секции)
                var sectionRows = items.Skip(startIdx).Take(endIdx - startIdx).ToList();

                if (sectionRows.Count > 0)
                {
                    var closure = CalculateCorrectionsForSection(sectionRows, applyCorrection);
                    if (closure.HasValue)
                        closures.Add(closure.Value);
                }
            }

            // Замыкание хода: если первая и последняя известные точки совпадают по коду,
            // добавляем секцию от последней известной точки до конца списка
            var firstAnchor = knownPoints.First();
            var lastAnchor = knownPoints.Last();

            if (!string.IsNullOrWhiteSpace(firstAnchor.Code)
                && string.Equals(firstAnchor.Code, lastAnchor.Code, StringComparison.OrdinalIgnoreCase)
                && lastAnchor.Index < items.Count)
            {
                var wrapSection = items.Skip(lastAnchor.Index).ToList();
                if (wrapSection.Count > 0)
                {
                    var closure = CalculateCorrectionsForSection(wrapSection, applyCorrection);
                    if (closure.HasValue)
                        closures.Add(closure.Value);
                }
            }
        }

        /// <summary>
        /// Рассчитывает поправки для одной секции хода (или всего хода)
        /// </summary>
        private double? CalculateCorrectionsForSection(
            List<TraverseRow> items,
            Action<TraverseRow, double>? applyCorrection = null)
        {
            if (items.Count == 0)
                return null;

            // Вычисляем невязку для данной секции
            var sign = MethodOrientationSign;
            var sectionClosure = items
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value * sign);

            var adjustableRows = items.Where(r => r.DeltaH.HasValue).ToList();
            if (adjustableRows.Count == 0)
                return sectionClosure;

            // Вычисляем общую длину секции (среднее расстояние для каждой станции)
            double totalDistance = 0;
            foreach (var row in items)
            {
                var avgDistance = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;
                totalDistance += avgDistance;
            }

            var allocations = new List<CorrectionAllocation>();

            if (totalDistance <= 0)
            {
                // Если нет данных о расстояниях, распределяем поровну на все станции
                var correctionPerStation = -sectionClosure / adjustableRows.Count;
                foreach (var row in adjustableRows)
                {
                    allocations.Add(new CorrectionAllocation(row, correctionPerStation));
                }
                ApplyRoundedCorrections(allocations, applyCorrection);
                return sectionClosure;
            }

            // Распределяем невязку пропорционально длинам
            var correctionFactor = -sectionClosure / totalDistance;
            foreach (var row in adjustableRows)
            {
                var avgDistance = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;
                allocations.Add(new CorrectionAllocation(row, correctionFactor * avgDistance));
            }

            ApplyRoundedCorrections(allocations, applyCorrection);

            return sectionClosure;
        }

        private const double CorrectionRoundingStep = 0.0001;

        private static void ApplyRoundedCorrections(
            List<CorrectionAllocation> allocations,
            Action<TraverseRow, double>? applyCorrection)
        {
            if (allocations.Count == 0)
                return;

            applyCorrection ??= (row, value) => row.Correction = value;

            foreach (var allocation in allocations)
            {
                allocation.Rounded = Math.Round(allocation.Raw, 4, MidpointRounding.AwayFromZero);
            }

            var targetTotal = allocations.Sum(a => a.Raw);
            var roundedTotal = allocations.Sum(a => a.Rounded);
            var remaining = Math.Round(targetTotal - roundedTotal, 4, MidpointRounding.AwayFromZero);

            var steps = (int)Math.Round(remaining / CorrectionRoundingStep, MidpointRounding.AwayFromZero);
            while (steps != 0)
            {
                var positive = steps > 0;
                var candidate = allocations
                    .OrderByDescending(a => positive ? (a.Raw - a.Rounded) : (a.Rounded - a.Raw))
                    .ThenByDescending(a => Math.Abs(a.Raw))
                    .FirstOrDefault();

                if (candidate == null)
                    break;

                candidate.Rounded += positive ? CorrectionRoundingStep : -CorrectionRoundingStep;
                steps += positive ? -1 : 1;
            }

            foreach (var allocation in allocations)
            {
                applyCorrection(allocation.Row, allocation.Rounded);
            }
        }

        private sealed class CorrectionAllocation
        {
            public CorrectionAllocation(TraverseRow row, double raw)
            {
                Row = row;
                Raw = raw;
                Rounded = raw;
            }

            public TraverseRow Row { get; }
            public double Raw { get; }
            public double Rounded { get; set; }
        }

        private sealed class AliasKeyComparer : IEqualityComparer<(TraverseRow row, bool isBack)>
        {
            public bool Equals((TraverseRow row, bool isBack) x, (TraverseRow row, bool isBack) y)
            {
                return ReferenceEquals(x.row, y.row) && x.isBack == y.isBack;
            }

            public int GetHashCode((TraverseRow row, bool isBack) obj)
            {
                return HashCode.Combine(obj.row, obj.isBack);
            }
        }

        private enum TraverseClosureMode
        {
            Open,
            Simple,
            Local
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

            // Сбрасываем предыдущие значения
            foreach (var row in items)
            {
                row.BackHeight = null;
                row.ForeHeight = null;
                row.BackHeightZ0 = null;
                row.ForeHeightZ0 = null;
                row.IsBackHeightKnown = false;
                row.IsForeHeightKnown = false;
            }

            // Карта соответствия "код → визит" для повторяющихся точек внутри хода
            var aliasByRowSide = new Dictionary<(TraverseRow row, bool isBack), string>(
                new AliasKeyComparer());
            var aliasToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var occurrenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            string? previousForeCode = null;
            string? previousForeAlias = null;

            string RegisterAlias(string code, bool reusePrevious)
            {
                if (IsAnchorAllowed(code, availableAdjustedHeights.ContainsKey))
                {
                    aliasToOriginal[code] = code;
                    return code;
                }

                if (reusePrevious && previousForeAlias != null &&
                    string.Equals(previousForeCode, code, StringComparison.OrdinalIgnoreCase))
                {
                    aliasToOriginal[previousForeAlias] = code;
                    return previousForeAlias;
                }

                var next = occurrenceCount.TryGetValue(code, out var count) ? count + 1 : 1;
                occurrenceCount[code] = next;

                var alias = next == 1 ? code : $"{code} ({next})";
                aliasToOriginal[alias] = code;
                return alias;
            }

            foreach (var row in items)
            {
                if (!string.IsNullOrWhiteSpace(row.BackCode))
                {
                    var alias = RegisterAlias(
                        row.BackCode!,
                        reusePrevious: previousForeAlias != null &&
                                      string.Equals(previousForeCode, row.BackCode, StringComparison.OrdinalIgnoreCase));
                    aliasByRowSide[(row, true)] = alias;
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode))
                {
                    var alias = RegisterAlias(row.ForeCode!, reusePrevious: false);
                    aliasByRowSide[(row, false)] = alias;

                    previousForeCode = row.ForeCode;
                    previousForeAlias = alias;
                }
                else
                {
                    previousForeCode = null;
                    previousForeAlias = null;
                }
            }

            string? AliasFor(TraverseRow row, bool isBack)
            {
                return aliasByRowSide.TryGetValue((row, isBack), out var value) ? value : null;
            }

            bool IsCopyAlias(string alias)
            {
                return aliasToOriginal.TryGetValue(alias, out var original)
                    && !string.Equals(alias, original, StringComparison.OrdinalIgnoreCase);
            }

            var adjusted = new Dictionary<string, double>(availableAdjustedHeights, StringComparer.OrdinalIgnoreCase);
            var raw = new Dictionary<string, double>(availableRawHeights, StringComparer.OrdinalIgnoreCase);

            // Обновляем локальные словари с учётом отвязанных точек
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

            for (int iteration = 0; iteration < 20; iteration++)
            {
                bool changedRaw = PropagateHeightsWithinRun(items, raw, useAdjusted: false, AliasFor);
                bool changedAdjusted = PropagateHeightsWithinRun(items, adjusted, useAdjusted: true, AliasFor);

                if (!changedRaw && !changedAdjusted)
                    break;
            }

            foreach (var row in items)
            {
                var backAlias = AliasFor(row, isBack: true);
                if (!string.IsNullOrWhiteSpace(backAlias) && adjusted.TryGetValue(backAlias!, out var backZ))
                {
                    row.BackHeight = backZ;
                    row.IsBackHeightKnown = IsAnchorAllowed(row.BackCode, availableAdjustedHeights.ContainsKey);
                }

                var foreAlias = AliasFor(row, isBack: false);
                if (!string.IsNullOrWhiteSpace(foreAlias) && adjusted.TryGetValue(foreAlias!, out var foreZ))
                {
                    row.ForeHeight = foreZ;
                    row.IsForeHeightKnown = IsAnchorAllowed(row.ForeCode, availableAdjustedHeights.ContainsKey);
                }
            }

            var runRawHeights = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

            double? GetRunHeight(string? alias)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    return null;

                if (runRawHeights.TryGetValue(alias, out var history) && history.Count > 0)
                {
                    return history[^1];
                }

                return raw.TryGetValue(alias, out var value) ? value : null;
            }

            bool HasRunHistory(string? alias)
            {
                return !string.IsNullOrWhiteSpace(alias)
                    && runRawHeights.TryGetValue(alias!, out var history)
                    && history.Count > 0;
            }

            void RecordRunHeight(string? alias, double value)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    return;

                if (!runRawHeights.TryGetValue(alias!, out var history))
                {
                    history = new List<double>();
                    runRawHeights[alias!] = history;
                }

                history.Add(value);

                if (!raw.ContainsKey(alias!) && !availableRawHeights.ContainsKey(alias!))
                {
                    raw[alias!] = value;
                }
            }

            foreach (var row in items)
            {
                var delta = row.DeltaH;

                var backAlias = AliasFor(row, isBack: true);
                var foreAlias = AliasFor(row, isBack: false);

                bool backIsAnchor = !string.IsNullOrWhiteSpace(row.BackCode)
                    && (_dataViewModel.HasKnownHeight(row.BackCode) ||
                        (!_dataViewModel.IsSharedPointEnabled(row.BackCode) &&
                         _dataViewModel.HasKnownHeight(_dataViewModel.GetPointCodeForRun(row.BackCode, runName))));
                bool foreIsAnchor = !string.IsNullOrWhiteSpace(row.ForeCode)
                    && (_dataViewModel.HasKnownHeight(row.ForeCode) ||
                        (!_dataViewModel.IsSharedPointEnabled(row.ForeCode) &&
                         _dataViewModel.HasKnownHeight(_dataViewModel.GetPointCodeForRun(row.ForeCode, runName))));

                // Подхватываем текущее известное значение (якорь или полученное ранее в этом ходе)
                if (row.BackHeightZ0 == null)
                {
                    var existingBack = GetRunHeight(backAlias);
                    if (existingBack.HasValue)
                    {
                        row.BackHeightZ0 = existingBack;
                        RecordRunHeight(backAlias, existingBack.Value);
                    }
                }

                if (row.ForeHeightZ0 == null)
                {
                    var existingFore = GetRunHeight(foreAlias);
                    if (existingFore.HasValue)
                    {
                        row.ForeHeightZ0 = existingFore;
                        RecordRunHeight(foreAlias, existingFore.Value);
                    }
                }

                if (!delta.HasValue)
                    continue;

                var backHeight = row.BackHeightZ0 ?? GetRunHeight(backAlias);
                var foreHeight = row.ForeHeightZ0 ?? GetRunHeight(foreAlias);

                if (backHeight.HasValue)
                {
                    var computedFore = backHeight.Value + delta.Value;
                    row.ForeHeightZ0 = computedFore;
                    RecordRunHeight(foreAlias, computedFore);
                }
                else if (foreHeight.HasValue)
                {
                    var computedBack = foreHeight.Value - delta.Value;
                    row.BackHeightZ0 = computedBack;
                    RecordRunHeight(backAlias, computedBack);
                }

                // Если обе стороны уже имели значения до расчёта, фиксируем их для повторных точек
                if (backHeight.HasValue && !HasRunHistory(backAlias))
                {
                    RecordRunHeight(backAlias, backHeight.Value);
                }
                if (foreHeight.HasValue && !HasRunHistory(foreAlias))
                {
                    RecordRunHeight(foreAlias, foreHeight.Value);
                }
            }

            foreach (var kvp in adjusted)
            {
                if (!IsCopyAlias(kvp.Key) && AllowPropagation(kvp.Key))
                {
                    availableAdjustedHeights[kvp.Key] = kvp.Value;
                }
            }

            foreach (var kvp in raw)
            {
                if (!IsCopyAlias(kvp.Key) && AllowPropagation(kvp.Key))
                {
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
        /// Обновляет отображение строк в DataGrid путём принудительного уведомления об изменении
        /// </summary>
        private void RefreshRowsDisplay()
        {
            // Создаём копию для принудительного обновления привязок
            var tempRows = _rows.ToList();
            _rows.Clear();
            foreach (var row in tempRows)
            {
                _rows.Add(row);
            }
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
