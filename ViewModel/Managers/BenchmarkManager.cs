using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nivtropy.Models;
using Nivtropy.ViewModels.Base;

namespace Nivtropy.ViewModels.Managers
{
    /// <summary>
    /// Менеджер реперов (точек с известными высотами)
    /// Отвечает за добавление, удаление и обновление реперов
    /// </summary>
    public class BenchmarkManager : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ObservableCollection<BenchmarkItem> _benchmarks = new();
        private readonly Dictionary<string, string> _benchmarkSystems = new(StringComparer.OrdinalIgnoreCase);

        private PointItem? _selectedPoint;
        private string? _newBenchmarkHeight;
        private string? _selectedSystemId;

        public const string DEFAULT_SYSTEM_ID = "system-default";

        public BenchmarkManager(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel ?? throw new ArgumentNullException(nameof(dataViewModel));
        }

        public event EventHandler? BenchmarksChanged;

        public ObservableCollection<BenchmarkItem> Benchmarks => _benchmarks;
        public IReadOnlyDictionary<string, string> BenchmarkSystems => _benchmarkSystems;

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

        public string? SelectedSystemId
        {
            get => _selectedSystemId;
            set
            {
                if (SetField(ref _selectedSystemId, value))
                {
                    UpdateBenchmarks();
                }
            }
        }

        public bool CanAddBenchmark =>
            SelectedPoint != null &&
            !string.IsNullOrWhiteSpace(NewBenchmarkHeight) &&
            double.TryParse(NewBenchmarkHeight, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        public ICommand AddBenchmarkCommand => new RelayCommand(_ => AddBenchmark(), _ => CanAddBenchmark);
        public ICommand RemoveBenchmarkCommand => new RelayCommand(param => RemoveBenchmark(param as BenchmarkItem));

        /// <summary>
        /// Добавляет новый репер (точку с известной высотой)
        /// </summary>
        public void AddBenchmark()
        {
            if (SelectedPoint == null || string.IsNullOrWhiteSpace(NewBenchmarkHeight))
                return;

            if (!double.TryParse(NewBenchmarkHeight, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var height))
                return;

            // Устанавливаем высоту в DataViewModel
            _dataViewModel.SetKnownHeight(SelectedPoint.Code, height);

            // Привязываем репер к текущей системе
            _benchmarkSystems[SelectedPoint.Code] = SelectedSystemId ?? DEFAULT_SYSTEM_ID;

            // Очищаем поля ввода
            SelectedPoint = null;
            NewBenchmarkHeight = string.Empty;

            // Уведомляем об изменениях
            OnBenchmarksChanged();
        }

        /// <summary>
        /// Удаляет репер
        /// </summary>
        public void RemoveBenchmark(BenchmarkItem? benchmark)
        {
            if (benchmark == null)
                return;

            _dataViewModel.ClearKnownHeight(benchmark.Code);
            _benchmarkSystems.Remove(benchmark.Code);
            UpdateBenchmarks();
            OnBenchmarksChanged();
        }

        /// <summary>
        /// Обновляет список реперов из DataViewModel с фильтрацией по выбранной системе
        /// </summary>
        public void UpdateBenchmarks()
        {
            _benchmarks.Clear();

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
                if (string.IsNullOrEmpty(SelectedSystemId) || benchmarkSystemId == SelectedSystemId)
                {
                    _benchmarks.Add(new BenchmarkItem(kvp.Key, kvp.Value, benchmarkSystemId));
                }
            }
        }

        /// <summary>
        /// Получает систему для указанного репера
        /// </summary>
        public string GetSystemForBenchmark(string pointCode)
        {
            return _benchmarkSystems.TryGetValue(pointCode, out var systemId)
                ? systemId
                : DEFAULT_SYSTEM_ID;
        }

        /// <summary>
        /// Проверяет, принадлежит ли репер указанной системе
        /// </summary>
        public bool BelongsToSystem(string pointCode, string systemId)
        {
            if (!_benchmarkSystems.TryGetValue(pointCode, out var benchmarkSystemId))
            {
                benchmarkSystemId = DEFAULT_SYSTEM_ID;
            }

            return benchmarkSystemId == systemId;
        }

        /// <summary>
        /// Получает реперы для указанной системы
        /// </summary>
        public IEnumerable<KeyValuePair<string, double>> GetBenchmarksForSystem(string systemId)
        {
            foreach (var kvp in _dataViewModel.KnownHeights)
            {
                if (BelongsToSystem(kvp.Key, systemId))
                {
                    yield return kvp;
                }
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

        private void OnBenchmarksChanged()
            => BenchmarksChanged?.Invoke(this, EventArgs.Empty);
    }
}
