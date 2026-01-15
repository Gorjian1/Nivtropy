using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Nivtropy.Presentation.Models;
using Nivtropy.Services;
using Nivtropy.ViewModels.Base;

namespace Nivtropy.ViewModels
{
    public class TraverseDesignViewModel : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ITraverseBuilder _traverseBuilder;
        private readonly ObservableCollection<DesignRow> _rows = new();

        private double _targetClosure;
        private double _startHeight;
        private double? _actualClosure;
        private double? _designClosure;
        private double _correctionPerStation;
        private double? _allowableClosure;
        private string _closureStatus = "Нет данных";
        private double _totalDistance;

        public TraverseDesignViewModel(DataViewModel dataViewModel, ITraverseBuilder traverseBuilder)
        {
            _dataViewModel = dataViewModel;
            _traverseBuilder = traverseBuilder;

            ((INotifyCollectionChanged)_dataViewModel.Records).CollectionChanged += OnRecordsCollectionChanged;
            ((INotifyCollectionChanged)_dataViewModel.Runs).CollectionChanged += (_, __) => OnPropertyChanged(nameof(Runs));
            _dataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;

            // Используем базовый класс для batch updates
            SubscribeToBatchUpdates(_dataViewModel);

            TargetClosure = 0;
            StartHeight = 0;
            UpdateRows();
        }

        /// <summary>
        /// Вызывается после завершения batch update
        /// </summary>
        protected override void OnBatchUpdateCompleted()
        {
            UpdateRows();
        }

        private void OnRecordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsUpdating)
                return;

            UpdateRows();
        }

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
            // Отписываемся от событий у существующих строк перед очисткой
            foreach (var row in _rows)
            {
                row.PropertyChanged -= OnDesignRowPropertyChanged;
            }

            _rows.Clear();

            // Автоматически выбираем первый ход, если он не выбран
            if (SelectedRun == null && Runs.Count > 0)
            {
                SelectedRun = Runs[0];
                return; // UpdateRows будет вызван повторно через setter SelectedRun
            }

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

            var items = _traverseBuilder.Build(
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

            double runningHeight = StartHeight;
            double adjustedSum = 0;

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

                if (adjustedDelta.HasValue)
                {
                    runningHeight += adjustedDelta.Value;
                    adjustedSum += adjustedDelta.Value;
                }

                var designRow = new DesignRow
                {
                    Index = row.Index,
                    Station = string.IsNullOrWhiteSpace(row.BackCode) && string.IsNullOrWhiteSpace(row.ForeCode)
                        ? row.LineName
                        : $"{row.BackCode ?? "?"} → {row.ForeCode ?? "?"}",
                    Distance_m = avgDistance > 0 ? avgDistance : null,
                    OriginalDeltaH = row.DeltaH,
                    Correction = correction,
                    AdjustedDeltaH = adjustedDelta,
                    DesignedHeight = runningHeight,
                    OriginalHeight = runningHeight,
                    OriginalDistance = avgDistance > 0 ? avgDistance : null,
                    IsEdited = false
                };

                // Подписываемся на изменения в строке для автоматического пересчета
                designRow.PropertyChanged += OnDesignRowPropertyChanged;

                _rows.Add(designRow);
            }

            DesignedClosure = adjustedSum;

            // Средняя поправка на станцию (для информации)
            var adjustableCount = items.Count(r => r.DeltaH.HasValue);
            CorrectionPerStation = adjustableCount > 0 ? closureToDistribute / adjustableCount : 0;
        }

        /// <summary>
        /// Обработчик изменений в строке проектирования
        /// Пересчитывает связанные параметры при редактировании Z или HD
        /// </summary>
        private void OnDesignRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DesignRow changedRow)
                return;

            // Игнорируем изменения в служебных свойствах
            if (e.PropertyName == nameof(DesignRow.IsEdited))
                return;

            // Находим индекс измененной строки
            var changedIndex = _rows.IndexOf(changedRow);
            if (changedIndex < 0)
                return;

            // Отмечаем строку как отредактированную
            changedRow.IsEdited = true;

            // Если изменилась высота (DesignedHeight), пересчитываем все последующие высоты
            if (e.PropertyName == nameof(DesignRow.DesignedHeight))
            {
                RecalculateHeightsFromIndex(changedIndex);
            }

            // Если изменилась дистанция (Distance_m), пересчитываем поправки и высоты
            if (e.PropertyName == nameof(DesignRow.Distance_m))
            {
                RecalculateCorrectionsAndHeights();
            }
        }

        /// <summary>
        /// Пересчитывает высоты всех точек начиная с указанного индекса + 1
        /// </summary>
        private void RecalculateHeightsFromIndex(int startIndex)
        {
            // Начинаем со следующей строки
            for (int i = startIndex + 1; i < _rows.Count; i++)
            {
                var prevRow = _rows[i - 1];
                var currentRow = _rows[i];

                // Временно отписываемся от событий, чтобы избежать рекурсии
                currentRow.PropertyChanged -= OnDesignRowPropertyChanged;

                // Пересчитываем высоту: H_current = H_prev + ΔH_adjusted
                if (currentRow.AdjustedDeltaH.HasValue)
                {
                    currentRow.DesignedHeight = prevRow.DesignedHeight + currentRow.AdjustedDeltaH.Value;
                }

                // Подписываемся обратно
                currentRow.PropertyChanged += OnDesignRowPropertyChanged;
            }
        }

        /// <summary>
        /// Пересчитывает поправки для всех строк и высоты
        /// Вызывается при изменении дистанции (Distance_m)
        /// </summary>
        private void RecalculateCorrectionsAndHeights()
        {
            if (_rows.Count == 0)
                return;

            // Рассчитываем фактическую невязку (сумма исходных превышений)
            var originalClosure = _rows
                .Where(r => r.OriginalDeltaH.HasValue)
                .Sum(r => r.OriginalDeltaH!.Value);

            // Рассчитываем общую длину хода с учетом отредактированных дистанций
            var totalDistance = _rows.Sum(r => r.Distance_m ?? 0);

            // Расчет невязки для распределения
            var closureToDistribute = TargetClosure - originalClosure;

            // Распределение поправок ПРОПОРЦИОНАЛЬНО ДЛИНАМ
            double correctionFactor = totalDistance > 0 ? closureToDistribute / totalDistance : 0;

            double runningHeight = StartHeight;
            double adjustedSum = 0;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];

                // Временно отписываемся от событий, чтобы избежать рекурсии
                row.PropertyChanged -= OnDesignRowPropertyChanged;

                // Поправка пропорционально длине данного хода
                double correction = row.OriginalDeltaH.HasValue
                    ? correctionFactor * (row.Distance_m ?? 0)
                    : 0;

                double? adjustedDelta = row.OriginalDeltaH.HasValue
                    ? row.OriginalDeltaH + correction
                    : null;

                row.Correction = correction;
                row.AdjustedDeltaH = adjustedDelta;

                if (adjustedDelta.HasValue)
                {
                    runningHeight += adjustedDelta.Value;
                    adjustedSum += adjustedDelta.Value;
                }

                // Обновляем высоту только если строка не была отредактирована вручную
                if (!row.IsEdited || i == 0)
                {
                    row.DesignedHeight = runningHeight;
                }

                // Подписываемся обратно
                row.PropertyChanged += OnDesignRowPropertyChanged;
            }

            DesignedClosure = adjustedSum;

            // Обновляем среднюю поправку на станцию
            var adjustableCount = _rows.Count(r => r.OriginalDeltaH.HasValue);
            CorrectionPerStation = adjustableCount > 0 ? closureToDistribute / adjustableCount : 0;
        }
    }
}
