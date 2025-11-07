using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Nivtropy.Models;
using Nivtropy.Services;

namespace Nivtropy.ViewModels
{
    public class TraverseCalculationViewModel : INotifyPropertyChanged
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ObservableCollection<TraverseRow> _rows = new();

        private readonly LevelingClassOption[] _classes =
        {
            new("BF", "Двойной ход: 4 мм · √n", ToleranceMode.SqrtStations, 0.004),
            new("FB", "Обратный двойной ход: 4 мм · √n", ToleranceMode.SqrtStations, 0.004),
            new("III", "Класс III: 10 мм · √L", ToleranceMode.SqrtLength, 0.010),
            new("IV", "Класс IV: 20 мм · √L", ToleranceMode.SqrtLength, 0.020)
        };

        private LevelingClassOption? _selectedClass;
        private double? _closure;
        private double? _allowableClosure;
        private string _closureVerdict = "Нет данных для расчёта.";
        private double _totalBackDistance;
        private double _totalForeDistance;
        private double _totalAverageDistance;
        private int _stationsCount;

        public TraverseCalculationViewModel(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel;
            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += (_, __) => UpdateRows();
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            _selectedClass = _classes.First();
            UpdateRows();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<TraverseRow> Rows => _rows;
        public ObservableCollection<LineSummary> Runs => _dataViewModel.Runs;

        public LevelingClassOption[] Classes => _classes;

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

        public double TotalBackDistance
        {
            get => _totalBackDistance;
            private set => SetField(ref _totalBackDistance, value);
        }

        public double TotalForeDistance
        {
            get => _totalForeDistance;
            private set => SetField(ref _totalForeDistance, value);
        }

        public double TotalAverageDistance
        {
            get => _totalAverageDistance;
            private set => SetField(ref _totalAverageDistance, value);
        }

        public double TotalLengthKilometers => TotalAverageDistance / 1000.0;

        public int StationsCount
        {
            get => _stationsCount;
            private set => SetField(ref _stationsCount, value);
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

            if (SelectedRun == null)
            {
                Closure = null;
                AllowableClosure = null;
                ClosureVerdict = "Выберите ход для расчёта.";
                StationsCount = 0;
                TotalBackDistance = 0;
                TotalForeDistance = 0;
                TotalAverageDistance = 0;
                return;
            }

            var items = TraverseBuilder.Build(
                _dataViewModel.Records.Where(r => ReferenceEquals(r.LineSummary, SelectedRun)),
                SelectedRun);

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

            var closure = items.Where(r => r.DeltaH.HasValue).Sum(r => r.DeltaH!.Value);
            Closure = StationsCount > 0 ? closure : null;

            UpdateTolerance();
        }

        private void UpdateTolerance()
        {
            if (SelectedClass == null || !Closure.HasValue || StationsCount == 0)
            {
                AllowableClosure = null;
                ClosureVerdict = StationsCount == 0 ? "Нет данных для расчёта." : "Выберите класс нивелирного хода.";
                return;
            }

            double tolerance = SelectedClass.Mode switch
            {
                ToleranceMode.SqrtStations => SelectedClass.Coefficient * Math.Sqrt(Math.Max(StationsCount, 1)),
                ToleranceMode.SqrtLength => SelectedClass.Coefficient * Math.Sqrt(Math.Max(TotalLengthKilometers, 1e-6)),
                _ => 0
            };

            AllowableClosure = tolerance;

            var absClosure = Math.Abs(Closure.Value);
            ClosureVerdict = absClosure <= tolerance
                ? "В пределах допуска."
                : "Превышение допуска!";
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

    public record LevelingClassOption(string Code, string Description, ToleranceMode Mode, double Coefficient)
    {
        public string Display => Code;
    }
}
