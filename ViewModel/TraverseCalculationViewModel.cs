using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Nivtropy.Models;
using Nivtropy.Services;

namespace Nivtropy.ViewModels
{
    public class TraverseCalculationViewModel : INotifyPropertyChanged
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ObservableCollection<TraverseRow> _rows = new();
        private readonly ObservableCollection<PointItem> _availablePoints = new();
        private readonly ObservableCollection<BenchmarkItem> _benchmarks = new();

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

        public TraverseCalculationViewModel(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel;
            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += (_, __) => UpdateRows();
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            _selectedMethod = _methods.FirstOrDefault();
            _selectedClass = _classes.FirstOrDefault();
            UpdateRows();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<TraverseRow> Rows => _rows;
        public ObservableCollection<LineSummary> Runs => _dataViewModel.Runs;
        public ObservableCollection<PointItem> AvailablePoints => _availablePoints;
        public ObservableCollection<BenchmarkItem> Benchmarks => _benchmarks;

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
                }
            }
        }

        public double? ClosureAbsolute => Closure.HasValue ? Math.Abs(Closure.Value) : null;

        public double? AllowableClosure
        {
            get => _allowableClosure;
            private set => SetField(ref _allowableClosure, value);
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
        /// Для составных ходов (продолжений) включает длину предшествующих частей
        /// </summary>
        public double TotalLengthKilometers
        {
            get
            {
                var currentRun = SelectedRun;
                if (currentRun == null)
                    return TotalBackDistance / 1000.0;

                // Если это продолжение хода, суммируем длины с предшествующими частями
                if (currentRun.ContinuationOfLineIndex.HasValue)
                {
                    var totalDistance = TotalBackDistance;

                    // Находим все предшествующие части хода
                    var predecessorIndex = currentRun.ContinuationOfLineIndex.Value;
                    var predecessor = _dataViewModel.Runs.FirstOrDefault(r => r.Index == predecessorIndex);

                    while (predecessor != null)
                    {
                        totalDistance += predecessor.TotalDistanceBack ?? 0;

                        // Проверяем, является ли предшественник тоже продолжением
                        if (predecessor.ContinuationOfLineIndex.HasValue)
                        {
                            predecessorIndex = predecessor.ContinuationOfLineIndex.Value;
                            predecessor = _dataViewModel.Runs.FirstOrDefault(r => r.Index == predecessorIndex);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return totalDistance / 1000.0;
                }

                return TotalBackDistance / 1000.0;
            }
        }

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

        public bool CanSetHeight => !string.IsNullOrWhiteSpace(_selectedPointCode);
        public bool CanClearHeight => !string.IsNullOrWhiteSpace(_selectedPointCode) && _dataViewModel.HasKnownHeight(_selectedPointCode);

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

            // Обновляем список реперов
            UpdateBenchmarks();

            // Очищаем поля ввода
            SelectedPoint = null;
            NewBenchmarkHeight = string.Empty;

            // Пересчитываем высоты
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
            UpdateBenchmarks();
            UpdateRows();
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

            // Сортируем по индексу хода, затем по коду точки
            var sortedPoints = pointsDict.Values
                .OrderBy(p => p.LineIndex)
                .ThenBy(p => p.Code)
                .ToList();

            foreach (var point in sortedPoints)
            {
                _availablePoints.Add(point);
            }
        }

        /// <summary>
        /// Обновляет список реперов из DataViewModel
        /// </summary>
        private void UpdateBenchmarks()
        {
            _benchmarks.Clear();

            foreach (var kvp in _dataViewModel.KnownHeights.OrderBy(k => k.Key))
            {
                _benchmarks.Add(new BenchmarkItem(kvp.Key, kvp.Value));
            }
        }

        private void DataViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun))
            {
                OnPropertyChanged(nameof(SelectedRun));
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

            var items = TraverseBuilder.Build(records);

            // Группируем станции по ходам для корректного расчета поправок
            var traverseGroups = items.GroupBy(r => r.LineName).ToList();

            // Рассчитываем поправки для каждого хода отдельно
            foreach (var group in traverseGroups)
            {
                CalculateCorrections(group.ToList());
            }

            // Обновляем накопление разности плеч для каждого хода
            UpdateArmDifferenceAccumulation(traverseGroups);

            // Рассчитываем высоты точек
            CalculateHeights(items);

            foreach (var row in items)
            {
                _rows.Add(row);
            }

            StationsCount = items.Count;
            TotalBackDistance = items.Sum(r => r.HdBack_m ?? 0);
            TotalForeDistance = items.Sum(r => r.HdFore_m ?? 0);
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
        /// </summary>
        private void CalculateCorrections(List<TraverseRow> items)
        {
            if (items.Count == 0)
                return;

            // Вычисляем невязку для данного хода
            var sign = MethodOrientationSign;
            var traverseClosure = items
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value * sign);

            // Вычисляем общую длину хода (среднее расстояние для каждой станции)
            double totalDistance = 0;
            foreach (var row in items)
            {
                var avgDistance = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;
                totalDistance += avgDistance;
            }

            if (totalDistance <= 0)
            {
                // Если нет данных о расстояниях, распределяем поровну на все станции
                var adjustableCount = items.Count(r => r.DeltaH.HasValue);
                if (adjustableCount > 0)
                {
                    var correctionPerStation = -traverseClosure / adjustableCount;
                    foreach (var row in items)
                    {
                        if (row.DeltaH.HasValue)
                            row.Correction = correctionPerStation;
                    }
                }
                return;
            }

            // Распределяем невязку пропорционально длинам
            var correctionFactor = -traverseClosure / totalDistance;
            foreach (var row in items)
            {
                if (row.DeltaH.HasValue)
                {
                    var avgDistance = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;
                    row.Correction = correctionFactor * avgDistance;
                }
            }
        }

        /// <summary>
        /// Рассчитывает высоты точек на основе известных высот и превышений
        /// Логика нивелирования: H_fore = H_back + Δh, где Δh = Rb - Rf
        /// </summary>
        private void CalculateHeights(List<TraverseRow> items)
        {
            if (items.Count == 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                var row = items[i];

                // Проверяем известные высоты
                var backKnownHeight = !string.IsNullOrWhiteSpace(row.BackCode)
                    ? _dataViewModel.GetKnownHeight(row.BackCode)
                    : null;
                var foreKnownHeight = !string.IsNullOrWhiteSpace(row.ForeCode)
                    ? _dataViewModel.GetKnownHeight(row.ForeCode)
                    : null;

                // Устанавливаем высоту задней точки
                if (backKnownHeight.HasValue)
                {
                    row.BackHeight = backKnownHeight.Value;
                    row.IsBackHeightKnown = true;
                }
                else if (i > 0 && !string.IsNullOrWhiteSpace(row.BackCode))
                {
                    // Пытаемся найти эту точку как переднюю в предыдущих станциях
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (items[j].ForeCode == row.BackCode && items[j].ForeHeight.HasValue)
                        {
                            row.BackHeight = items[j].ForeHeight.Value;
                            row.IsBackHeightKnown = items[j].IsForeHeightKnown;
                            break;
                        }
                    }
                }

                // Рассчитываем высоту передней точки: H_fore = H_back + Δh (используем исправленное превышение)
                if (row.BackHeight.HasValue && row.AdjustedDeltaH.HasValue)
                {
                    row.ForeHeight = row.BackHeight.Value + row.AdjustedDeltaH.Value;
                    row.IsForeHeightKnown = false;
                }

                // Если у передней точки есть известная высота - перезаписываем
                if (foreKnownHeight.HasValue)
                {
                    row.ForeHeight = foreKnownHeight.Value;
                    row.IsForeHeightKnown = true;
                }
            }
        }

        /// <summary>
        /// Обновляет накопление разности плеч для каждого хода на основе TraverseRow
        /// Обновляет существующие LineSummary в DataViewModel.Runs
        /// </summary>
        private void UpdateArmDifferenceAccumulation(List<IGrouping<string, TraverseRow>> traverseGroups)
        {
            foreach (var group in traverseGroups)
            {
                var lineName = group.Key;
                var rows = group.ToList();

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

                // Создаем новый LineSummary с обновленными значениями
                var newSummary = new LineSummary(
                    existingSummary.Index,
                    existingSummary.StartTarget,
                    existingSummary.StartStation,
                    existingSummary.EndTarget,
                    existingSummary.EndStation,
                    existingSummary.RecordCount,
                    existingSummary.DeltaHSum,
                    totalDistanceBack,
                    totalDistanceFore,
                    accumulation,
                    existingSummary.ContinuationOfLineIndex,
                    existingSummary.DisplayIndex);

                // Заменяем в коллекции
                _dataViewModel.Runs[existingIndex] = newSummary;

                // Обновляем ссылки в TraverseRow
                foreach (var row in rows)
                {
                    row.LineSummary = newSummary;
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
                    row.IsArmDifferenceExceeded = row.ArmDifference_m.Value > stationTolerance;
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
                        lineSummary.ArmDifferenceAccumulation.Value > accumulationTolerance;
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

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
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
