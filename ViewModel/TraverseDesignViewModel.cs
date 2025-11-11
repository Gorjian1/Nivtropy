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
    public class TraverseDesignViewModel : INotifyPropertyChanged
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ObservableCollection<DesignRow> _rows = new();

        private double _targetClosure;
        private double _startHeight;
        private double? _actualClosure;
        private double? _designClosure;
        private double _correctionPerStation;
        private double? _allowableClosure;
        private string _closureStatus = "Нет данных";
        private double _totalDistance;
        private DesignRow? _selectedRow;

        public TraverseDesignViewModel(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel;
            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += (_, __) => UpdateRows();
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            TargetClosure = 0;
            StartHeight = 0;
            UpdateRows();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<DesignRow> Rows => _rows;
        public ObservableCollection<LineSummary> Runs => _dataViewModel.Runs;

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

        public double TargetClosure
        {
            get => _targetClosure;
            set
            {
                if (Math.Abs(_targetClosure - value) > double.Epsilon)
                {
                    _targetClosure = value;
                    OnPropertyChanged();
                    UpdateRows();
                }
            }
        }

        public double StartHeight
        {
            get => _startHeight;
            set
            {
                if (Math.Abs(_startHeight - value) > double.Epsilon)
                {
                    _startHeight = value;
                    OnPropertyChanged();
                    UpdateRows();
                }
            }
        }

        public double? ActualClosure
        {
            get => _actualClosure;
            private set => SetField(ref _actualClosure, value);
        }

        public double? DesignedClosure
        {
            get => _designClosure;
            private set => SetField(ref _designClosure, value);
        }

        public double CorrectionPerStation
        {
            get => _correctionPerStation;
            private set => SetField(ref _correctionPerStation, value);
        }

        public double? AllowableClosure
        {
            get => _allowableClosure;
            private set => SetField(ref _allowableClosure, value);
        }

        public string ClosureStatus
        {
            get => _closureStatus;
            private set => SetField(ref _closureStatus, value);
        }

        public double TotalDistance
        {
            get => _totalDistance;
            private set => SetField(ref _totalDistance, value);
        }

        public DesignRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                {
                    OnPropertyChanged(nameof(CanSetHeight));
                    OnPropertyChanged(nameof(CanClearHeight));
                }
            }
        }

        public bool CanSetHeight => SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.PointCode);
        public bool CanClearHeight => SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.PointCode)
            && _dataViewModel.HasKnownHeight(SelectedRow.PointCode);

        /// <summary>
        /// Устанавливает известную высоту для выбранной точки
        /// </summary>
        public void SetKnownHeightForSelectedPoint(double height)
        {
            if (SelectedRow == null || string.IsNullOrWhiteSpace(SelectedRow.PointCode))
                return;

            _dataViewModel.SetKnownHeight(SelectedRow.PointCode, height);
            UpdateRows();
        }

        /// <summary>
        /// Удаляет известную высоту у выбранной точки
        /// </summary>
        public void ClearKnownHeightForSelectedPoint()
        {
            if (SelectedRow == null || string.IsNullOrWhiteSpace(SelectedRow.PointCode))
                return;

            _dataViewModel.ClearKnownHeight(SelectedRow.PointCode);
            UpdateRows();
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
                ActualClosure = null;
                DesignedClosure = null;
                CorrectionPerStation = 0;
                AllowableClosure = null;
                ClosureStatus = "Нет данных";
                TotalDistance = 0;
                return;
            }

            var items = TraverseBuilder.Build(
                _dataViewModel.Records.Where(r => ReferenceEquals(r.LineSummary, SelectedRun)),
                SelectedRun);

            if (items.Count == 0)
            {
                ActualClosure = null;
                DesignedClosure = null;
                CorrectionPerStation = 0;
                AllowableClosure = null;
                ClosureStatus = "Нет данных";
                TotalDistance = 0;
                return;
            }

            // Расчет фактической невязки
            var originalClosure = items.Where(r => r.DeltaH.HasValue).Sum(r => r.DeltaH!.Value);
            ActualClosure = originalClosure;

            // Расчет общей длины хода (в метрах)
            var totalDistance = 0.0;
            foreach (var row in items)
            {
                var avgDist = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;
                totalDistance += avgDist;
            }
            TotalDistance = totalDistance;

            // Расчет допустимой невязки по формуле для IV класса: 20 мм × √L (L в км)
            var lengthKm = totalDistance / 1000.0;
            AllowableClosure = 0.020 * Math.Sqrt(Math.Max(lengthKm, 1e-6)); // в метрах

            // Проверка допуска
            var absActualClosure = Math.Abs(originalClosure);
            if (absActualClosure <= AllowableClosure.Value)
            {
                ClosureStatus = "✓ В пределах допуска";
            }
            else
            {
                ClosureStatus = $"✗ ПРЕВЫШЕНИЕ допуска! ({absActualClosure:F4} м > {AllowableClosure:F4} м)";
            }

            // Расчет невязки для распределения
            var closureToDistribute = TargetClosure - originalClosure;

            // Распределение поправок ПРОПОРЦИОНАЛЬНО ДЛИНАМ секций
            double correctionFactor = totalDistance > 0 ? closureToDistribute / totalDistance : 0;

            // Начальная высота: если у первой точки есть известная высота - использовать её
            var firstPointCode = items.FirstOrDefault()?.BackCode;
            double runningHeight = !string.IsNullOrWhiteSpace(firstPointCode) && _dataViewModel.GetKnownHeight(firstPointCode).HasValue
                ? _dataViewModel.GetKnownHeight(firstPointCode)!.Value
                : StartHeight;

            double adjustedSum = 0;
            bool isFirstStation = true;

            foreach (var row in items)
            {
                // Средняя длина для данного хода
                var avgDistance = ((row.HdBack_m ?? 0) + (row.HdFore_m ?? 0)) / 2.0;

                // Поправка пропорционально длине данного хода
                double correction = row.DeltaH.HasValue
                    ? correctionFactor * avgDistance
                    : 0;

                double? adjustedDelta = row.DeltaH.HasValue
                    ? row.DeltaH + correction
                    : null;

                // Рассчитываем высоту следующей точки
                if (adjustedDelta.HasValue)
                {
                    runningHeight += adjustedDelta.Value;
                    adjustedSum += adjustedDelta.Value;
                }

                // Проверяем, есть ли известная высота у конечной точки (ForeCode)
                var forePointCode = row.ForeCode;
                var knownHeightFore = !string.IsNullOrWhiteSpace(forePointCode)
                    ? _dataViewModel.GetKnownHeight(forePointCode)
                    : null;

                // Если у конечной точки есть известная высота - используем её
                if (knownHeightFore.HasValue)
                {
                    runningHeight = knownHeightFore.Value;
                }

                // Известная высота начальной точки (для первой станции)
                var knownHeightBack = isFirstStation && !string.IsNullOrWhiteSpace(row.BackCode)
                    ? _dataViewModel.GetKnownHeight(row.BackCode)
                    : null;

                _rows.Add(new DesignRow
                {
                    Index = row.Index,
                    Station = string.IsNullOrWhiteSpace(row.BackCode) && string.IsNullOrWhiteSpace(row.ForeCode)
                        ? row.LineName
                        : $"{row.BackCode ?? "?"} → {row.ForeCode ?? "?"}",
                    PointCode = row.ForeCode, // Код конечной точки
                    Distance_m = avgDistance > 0 ? avgDistance : null,
                    OriginalDeltaH = row.DeltaH,
                    Correction = correction,
                    AdjustedDeltaH = adjustedDelta,
                    DesignedHeight = runningHeight,
                    KnownHeight = knownHeightFore ?? (isFirstStation ? knownHeightBack : null)
                });

                isFirstStation = false;
            }

            DesignedClosure = adjustedSum;

            // Средняя поправка на станцию (для информации)
            var adjustableCount = items.Count(r => r.DeltaH.HasValue);
            CorrectionPerStation = adjustableCount > 0 ? closureToDistribute / adjustableCount : 0;
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
}
