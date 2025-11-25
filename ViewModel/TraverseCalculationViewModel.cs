using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Win32;
using Nivtropy.Models;
using Nivtropy.Services;

namespace Nivtropy.ViewModels
{
    public class TraverseCalculationViewModel : INotifyPropertyChanged
    {
        private readonly DataViewModel _dataViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ObservableCollection<TraverseRow> _rows = new();
        private readonly ObservableCollection<PointItem> _availablePoints = new();
        private readonly ObservableCollection<BenchmarkItem> _benchmarks = new();

        private bool _isUpdating = false; // Флаг для подавления обновлений

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

        public TraverseCalculationViewModel(DataViewModel dataViewModel, SettingsViewModel settingsViewModel)
        {
            _dataViewModel = dataViewModel;
            _settingsViewModel = settingsViewModel;
            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += OnRecordsCollectionChanged;
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            // Подписываемся на события батчевых обновлений
            _dataViewModel.BeginBatchUpdate += (_, __) => _isUpdating = true;
            _dataViewModel.EndBatchUpdate += (_, __) =>
            {
                _isUpdating = false;
                UpdateRows(); // Обновляем один раз после завершения батча
            };

            _selectedMethod = _methods.FirstOrDefault();
            _selectedClass = _classes.FirstOrDefault();
            UpdateRows();
        }

        private void OnRecordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isUpdating)
                return;

            UpdateRows();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<TraverseRow> Rows => _rows;
        public ObservableCollection<LineSummary> Runs => _dataViewModel.Runs;
        public ObservableCollection<PointItem> AvailablePoints => _availablePoints;
        public ObservableCollection<BenchmarkItem> Benchmarks => _benchmarks;
        public SettingsViewModel Settings => _settingsViewModel;

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
        /// Экспортирует данные в CSV с 4-частной структурой для каждого хода
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
                var csv = new System.Text.StringBuilder();

                // Группировка по ходам
                var groupedRows = _rows.GroupBy(r => r.LineName).ToList();

                foreach (var group in groupedRows)
                {
                    var lineName = group.Key;
                    var rows = group.ToList();
                    var lineSummary = rows.FirstOrDefault()?.LineSummary;

                    // 1. Start-line - начало хода
                    csv.AppendLine($"===== НАЧАЛО ХОДА: {lineName} =====");

                    // 2. Info line - информация о ходе
                    if (lineSummary != null)
                    {
                        var lengthBack = lineSummary.TotalDistanceBack ?? 0;
                        var lengthFore = lineSummary.TotalDistanceFore ?? 0;
                        var totalLength = lineSummary.TotalAverageLength ?? 0;
                        var armAccumulation = lineSummary.ArmDifferenceAccumulation ?? 0;
                        var stationCount = lineSummary.RecordCount;

                        csv.AppendLine($"Станций: {stationCount}; Длина назад: {lengthBack:F2} м; Длина вперёд: {lengthFore:F2} м; Общая длина: {totalLength:F2} м; Накопление плеч: {armAccumulation:F3} м");
                    }

                    // 3. Header row + data table
                    csv.AppendLine("Номер;Ход;Точка;Станция;Отсчет назад (м);Отсчет вперед (м);Превышение (м);Поправка (мм);Превышение испр. (м);Высота непров. (м);Высота (м);Длина станции (м)");

                    foreach (var dataRow in rows)
                    {
                        var heightZ0 = dataRow.IsVirtualStation ? dataRow.BackHeightZ0 : dataRow.ForeHeightZ0;
                        var height = dataRow.IsVirtualStation ? dataRow.BackHeight : dataRow.ForeHeight;

                        csv.AppendLine(string.Join(";",
                            dataRow.Index,
                            dataRow.LineName,
                            dataRow.PointCode,
                            dataRow.Station,
                            dataRow.Rb_m?.ToString("F4") ?? "",
                            dataRow.Rf_m?.ToString("F4") ?? "",
                            dataRow.DeltaH?.ToString("F4") ?? "",
                            dataRow.Correction.HasValue ? (dataRow.Correction.Value * 1000).ToString("F2") : "",
                            dataRow.AdjustedDeltaH?.ToString("F4") ?? "",
                            heightZ0?.ToString("F4") ?? "",
                            height?.ToString("F4") ?? "",
                            dataRow.StationLength_m?.ToString("F2") ?? ""
                        ));
                    }

                    // 4. End-line - конец хода
                    csv.AppendLine($"===== КОНЕЦ ХОДА: {lineName} =====");
                    csv.AppendLine(); // Пустая строка между ходами
                }

                System.IO.File.WriteAllText(saveFileDialog.FileName, csv.ToString(), System.Text.Encoding.UTF8);

                System.Windows.MessageBox.Show($"Данные успешно экспортированы в:\n{saveFileDialog.FileName}",
                    "Экспорт завершён",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при экспорте:\n{ex.Message}",
                    "Ошибка экспорта",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
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
        ///
        /// Поддерживает два режима:
        /// 1. Обычный - невязка распределяется по всему ходу
        /// 2. Локальное уравнивание - ход разбивается на секции между известными точками,
        ///    и каждая секция уравнивается отдельно
        /// </summary>
        private void CalculateCorrections(List<TraverseRow> items)
        {
            if (items.Count == 0)
                return;

            // Проверяем, нужно ли локальное уравнивание
            var lineSummary = items.FirstOrDefault()?.LineSummary;
            var hasIntermediateAnchors = lineSummary != null && lineSummary.KnownPointsCount > 1;

            if (hasIntermediateAnchors || (lineSummary?.UseLocalAdjustment ?? false))
            {
                // Локальное уравнивание: разбиваем на секции между известными точками
                CalculateCorrectionsWithSections(items);
            }
            else
            {
                // Обычное уравнивание: по всему ходу
                CalculateCorrectionsForSection(items);
            }
        }

        /// <summary>
        /// Локальное уравнивание: разбивает ход на секции и уравнивает каждую отдельно
        /// </summary>
        private void CalculateCorrectionsWithSections(List<TraverseRow> items)
        {
            if (items.Count == 0)
                return;

            // Находим индексы известных точек
            var knownPointIndices = new List<int>();

            for (int i = 0; i < items.Count; i++)
            {
                var row = items[i];

                // Проверяем заднюю точку
                if (!string.IsNullOrWhiteSpace(row.BackCode) && _dataViewModel.HasKnownHeight(row.BackCode))
                {
                    if (!knownPointIndices.Contains(i))
                        knownPointIndices.Add(i);
                }

                // Проверяем переднюю точку
                if (!string.IsNullOrWhiteSpace(row.ForeCode) && _dataViewModel.HasKnownHeight(row.ForeCode))
                {
                    // ForeCode станции i соответствует BackCode станции i+1
                    if (i < items.Count - 1 && !knownPointIndices.Contains(i + 1))
                        knownPointIndices.Add(i + 1);
                }
            }

            knownPointIndices.Sort();

            if (knownPointIndices.Count < 2)
            {
                // Недостаточно известных точек для секций, используем обычное уравнивание
                CalculateCorrectionsForSection(items);
                return;
            }

            // Разбиваем на секции между известными точками
            for (int i = 0; i < knownPointIndices.Count - 1; i++)
            {
                int startIdx = knownPointIndices[i];
                int endIdx = knownPointIndices[i + 1];

                // Секция включает станции от startIdx до endIdx (не включая startIdx, т.к. это виртуальная станция начала секции)
                var sectionRows = items.Skip(startIdx).Take(endIdx - startIdx).ToList();

                if (sectionRows.Count > 0)
                {
                    CalculateCorrectionsForSection(sectionRows);
                }
            }
        }

        /// <summary>
        /// Рассчитывает поправки для одной секции хода (или всего хода)
        /// </summary>
        private void CalculateCorrectionsForSection(List<TraverseRow> items)
        {
            if (items.Count == 0)
                return;

            // Вычисляем невязку для данной секции
            var sign = MethodOrientationSign;
            var sectionClosure = items
                .Where(r => r.DeltaH.HasValue)
                .Sum(r => r.DeltaH!.Value * sign);

            var adjustableRows = items.Where(r => r.DeltaH.HasValue).ToList();
            if (adjustableRows.Count == 0)
                return;

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
                ApplyRoundedCorrections(allocations);
                return;
            }

            // Распределяем невязку пропорционально длинам
            var correctionFactor = -sectionClosure / totalDistance;
            foreach (var row in adjustableRows)
            {
                var avgDistance = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;
                allocations.Add(new CorrectionAllocation(row, correctionFactor * avgDistance));
            }

            ApplyRoundedCorrections(allocations);
        }

        private const double CorrectionRoundingStep = 0.0001;

        private static void ApplyRoundedCorrections(List<CorrectionAllocation> allocations)
        {
            if (allocations.Count == 0)
                return;

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
                allocation.Row.Correction = allocation.Rounded;
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

        /// <summary>
        /// Рассчитывает высоты точек на основе известных высот и превышений
        /// Логика нивелирования: H_fore = H_back + Δh, где Δh = Rb - Rf
        /// Рассчитываются две версии: с поправкой (Z) и без поправки (Z0)
        ///
        /// Для Z: используются известные высоты для замыкания хода
        /// Для Z0: показывает чистое накопление превышений БЕЗ поправок
        ///         Если есть известная высота в начале - используется как стартовая
        ///         Если нет известных высот - начинается с условного нуля
        ///
        /// НОВОЕ: Поддержка установки высоты на любой точке (не только начальной)
        /// Алгоритм распространяет высоты в обе стороны от известных точек
        /// </summary>
        private void CalculateHeights(List<TraverseRow> items)
        {
            if (items.Count == 0)
                return;

            // Группируем станции по ходам и рассчитываем высоты отдельно для каждого хода
            var traverseGroups = items.GroupBy(r => r.LineName).ToList();

            // Глобальные словари высот: сначала известные точки, затем накопленные вычисления для всех ходов
            var adjustedGlobal = new Dictionary<string, double>(_dataViewModel.KnownHeights, StringComparer.OrdinalIgnoreCase);
            var rawGlobal = new Dictionary<string, double>(_dataViewModel.KnownHeights, StringComparer.OrdinalIgnoreCase);

            // Сбрасываем предыдущие значения, чтобы не смешивать данные разных пересчётов
            foreach (var row in traverseGroups.SelectMany(g => g))
            {
                row.BackHeight = null;
                row.ForeHeight = null;
                row.BackHeightZ0 = null;
                row.ForeHeightZ0 = null;
                row.IsBackHeightKnown = false;
                row.IsForeHeightKnown = false;
            }

            // Несколько итераций позволяют перенести вычисленные высоты на смежные ходы через общие точки
            var guard = 0;
            bool changed;

            do
            {
                changed = false;

                foreach (var traverseGroup in traverseGroups)
                {
                    var traverseRows = traverseGroup.OrderBy(r => r.Index).ToList();

                    // Z0: используем только первое доступное начало (известное или вычисленное), чтобы сохранить невязку
                    var rawLocal = ComputeTraverseHeights(traverseRows, rawGlobal, useAdjustedDelta: false, seedAllKnownAnchors: false);
                    changed |= MergeHeights(rawLocal, rawGlobal);

                    // Z: закрепляем ход на всех известных точках, используя уравнённые превышения
                    var adjustedLocal = ComputeTraverseHeights(traverseRows, adjustedGlobal, useAdjustedDelta: true, seedAllKnownAnchors: true);
                    changed |= MergeHeights(adjustedLocal, adjustedGlobal);
                }

                guard++;
            } while (changed && guard < 5);

            // Переносим рассчитанные значения в строки хода
            foreach (var row in traverseGroups.SelectMany(g => g))
            {
                if (!string.IsNullOrWhiteSpace(row.BackCode) && adjustedGlobal.TryGetValue(row.BackCode, out var back))
                {
                    row.BackHeight = back;
                    row.IsBackHeightKnown = _dataViewModel.HasKnownHeight(row.BackCode);
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode) && adjustedGlobal.TryGetValue(row.ForeCode, out var fore))
                {
                    row.ForeHeight = fore;
                    row.IsForeHeightKnown = _dataViewModel.HasKnownHeight(row.ForeCode);
                }

                if (!string.IsNullOrWhiteSpace(row.BackCode) && rawGlobal.TryGetValue(row.BackCode, out var backZ0))
                {
                    row.BackHeightZ0 = backZ0;
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode) && rawGlobal.TryGetValue(row.ForeCode, out var foreZ0))
                {
                    row.ForeHeightZ0 = foreZ0;
                }
            }
        }

        private Dictionary<string, double> ComputeTraverseHeights(
            List<TraverseRow> traverseRows,
            Dictionary<string, double> globalHeights,
            bool useAdjustedDelta,
            bool seedAllKnownAnchors)
        {
            var localHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            // Заполняем начальными известными высотами (полностью либо только первую доступную)
            if (seedAllKnownAnchors)
            {
                foreach (var row in traverseRows)
                {
                    TrySeed(row.BackCode);
                    TrySeed(row.ForeCode);
                }
            }
            else
            {
                // Только первое доступное начало (из глобальных или из списка известных)
                var anchor = FindAnchor(traverseRows, globalHeights);
                if (anchor.code != null)
                {
                    localHeights[anchor.code] = anchor.height;
                }
            }

            // Если ничего не знаем, стартуем с условного нуля на первой точке
            if (localHeights.Count == 0 && traverseRows.Count > 0 && !string.IsNullOrWhiteSpace(traverseRows[0].BackCode))
            {
                localHeights[traverseRows[0].BackCode] = 0.0;
            }

            // Итеративно распространяем высоты в обе стороны
            bool changed;
            int guard = 0;

            do
            {
                changed = false;

                foreach (var row in traverseRows)
                {
                    if (TryGetDelta(row, useAdjustedDelta, out var delta) &&
                        !string.IsNullOrWhiteSpace(row.BackCode) &&
                        !string.IsNullOrWhiteSpace(row.ForeCode))
                    {
                        if (localHeights.TryGetValue(row.BackCode, out var backHeight) && !localHeights.ContainsKey(row.ForeCode))
                        {
                            localHeights[row.ForeCode] = backHeight + delta;
                            changed = true;
                        }
                        else if (localHeights.TryGetValue(row.ForeCode, out var foreHeight) && !localHeights.ContainsKey(row.BackCode))
                        {
                            localHeights[row.BackCode] = foreHeight - delta;
                            changed = true;
                        }
                        else if (seedAllKnownAnchors)
                        {
                            // Если обе точки известны и одна задана вручную, закрепляем её значением из глобального словаря
                            if (globalHeights.TryGetValue(row.BackCode, out var globalBack))
                            {
                                changed |= EnsureOverride(localHeights, row.BackCode, globalBack);
                            }
                            if (globalHeights.TryGetValue(row.ForeCode, out var globalFore))
                            {
                                changed |= EnsureOverride(localHeights, row.ForeCode, globalFore);
                            }
                        }
                    }
                }

                guard++;
            } while (changed && guard < traverseRows.Count * 2 + 4);

            return localHeights;

            void TrySeed(string? code)
            {
                if (string.IsNullOrWhiteSpace(code))
                    return;

                if (localHeights.ContainsKey(code))
                    return;

                if (globalHeights.TryGetValue(code, out var existing))
                {
                    localHeights[code] = existing;
                }
                else
                {
                    var known = _dataViewModel.GetKnownHeight(code);
                    if (known.HasValue)
                    {
                        localHeights[code] = known.Value;
                    }
                }
            }
        }

        private static (string? code, double height) FindAnchor(List<TraverseRow> traverseRows, Dictionary<string, double> globalHeights)
        {
            foreach (var row in traverseRows)
            {
                if (!string.IsNullOrWhiteSpace(row.BackCode) && globalHeights.TryGetValue(row.BackCode, out var back))
                {
                    return (row.BackCode, back);
                }
                if (!string.IsNullOrWhiteSpace(row.ForeCode) && globalHeights.TryGetValue(row.ForeCode, out var fore))
                {
                    return (row.ForeCode, fore);
                }
            }

            foreach (var row in traverseRows)
            {
                if (!string.IsNullOrWhiteSpace(row.BackCode) && globalHeights.TryGetValue(row.BackCode, out var backKnown))
                {
                    return (row.BackCode, backKnown);
                }

                if (!string.IsNullOrWhiteSpace(row.ForeCode) && globalHeights.TryGetValue(row.ForeCode, out var foreKnown))
                {
                    return (row.ForeCode, foreKnown);
                }
            }

            // Ничего не нашли
            return (null, 0);
        }

        private static bool EnsureOverride(Dictionary<string, double> target, string code, double value)
        {
            if (!target.TryGetValue(code, out var current) || Math.Abs(current - value) > 1e-8)
            {
                target[code] = value;
                return true;
            }

            return false;
        }

        private static bool MergeHeights(Dictionary<string, double> source, Dictionary<string, double> destination)
        {
            var changed = false;

            foreach (var kvp in source)
            {
                if (!destination.ContainsKey(kvp.Key))
                {
                    destination[kvp.Key] = kvp.Value;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool TryGetDelta(TraverseRow row, bool useAdjusted, out double delta)
        {
            var source = useAdjusted ? row.AdjustedDeltaH : row.DeltaH;
            if (source.HasValue)
            {
                delta = source.Value;
                return true;
            }

            delta = 0;
            return false;
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

                // Подсчитываем количество известных точек в этом ходе
                var knownPointsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    if (!string.IsNullOrWhiteSpace(row.BackCode) && _dataViewModel.HasKnownHeight(row.BackCode))
                    {
                        knownPointsSet.Add(row.BackCode);
                    }
                    if (!string.IsNullOrWhiteSpace(row.ForeCode) && _dataViewModel.HasKnownHeight(row.ForeCode))
                    {
                        knownPointsSet.Add(row.ForeCode);
                    }
                }
                var knownPointsCount = knownPointsSet.Count;

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
                    knownPointsCount);

                // Сохраняем состояние UseLocalAdjustment
                newSummary.UseLocalAdjustment = existingSummary.UseLocalAdjustment;
                newSummary.IsArmDifferenceAccumulationExceeded = existingSummary.IsArmDifferenceAccumulationExceeded;

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
