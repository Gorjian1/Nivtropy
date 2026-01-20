using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Services;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.Mappers;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels
{
    public class TraverseDesignViewModel : ViewModelBase
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ITraverseCalculationService _calculationService;
        private readonly IDesignCalculationService _designService;
        private readonly ObservableCollection<DesignRow> _rows = new();

        private double _targetClosure;
        private double _startHeight;
        private double? _actualClosure;
        private double? _designClosure;
        private double _correctionPerStation;
        private double? _allowableClosure;
        private string _closureStatus = "Нет данных";
        private double _totalDistance;

        public TraverseDesignViewModel(
            DataViewModel dataViewModel,
            ITraverseCalculationService calculationService,
            IDesignCalculationService designService)
        {
            _dataViewModel = dataViewModel ?? throw new ArgumentNullException(nameof(dataViewModel));
            _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
            _designService = designService ?? throw new ArgumentNullException(nameof(designService));

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

            // Используем TraverseCalculationService для построения строк хода
            var traverseRows = _calculationService.BuildTraverseRows(
                _dataViewModel.GetRawRecordsForRun(SelectedRun),
                new[] { SelectedRun.ToDto() });

            if (traverseRows.Count == 0)
            {
                ActualClosure = null;
                DesignedClosure = null;
                CorrectionPerStation = 0;
                AllowableClosure = null;
                ClosureStatus = "Нет данных";
                TotalDistance = 0;
                return;
            }

            // Используем DesignCalculationService для расчёта проектных данных
            var result = _designService.BuildDesignRows(traverseRows, StartHeight, TargetClosure);

            // Обновляем свойства ViewModel
            ActualClosure = result.ActualClosure;
            DesignedClosure = result.DesignedClosure;
            CorrectionPerStation = result.CorrectionPerStation;
            TotalDistance = result.TotalDistance;
            AllowableClosure = result.AllowableClosure;
            ClosureStatus = result.ClosureStatus;

            // Добавляем строки и подписываемся на изменения
            foreach (var designRow in result.Rows.Select(row => row.ToModel()))
            {
                designRow.PropertyChanged += OnDesignRowPropertyChanged;
                _rows.Add(designRow);
            }
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
            // Временно отписываемся от событий
            foreach (var row in _rows)
            {
                row.PropertyChanged -= OnDesignRowPropertyChanged;
            }

            // Используем сервис для пересчёта
            var dtos = _rows.Select(row => row.ToDto()).ToList();
            _designService.RecalculateHeightsFrom(dtos, startIndex);
            ApplyDtosToRows(dtos);

            // Подписываемся обратно
            foreach (var row in _rows)
            {
                row.PropertyChanged += OnDesignRowPropertyChanged;
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

            // Временно отписываемся от событий
            foreach (var row in _rows)
            {
                row.PropertyChanged -= OnDesignRowPropertyChanged;
            }

            // Используем сервис для пересчёта поправок и высот
            var dtos = _rows.Select(row => row.ToDto()).ToList();
            var adjustedSum = _designService.RecalculateCorrectionsAndHeights(
                dtos, StartHeight, TargetClosure);
            ApplyDtosToRows(dtos);

            DesignedClosure = adjustedSum;

            // Обновляем среднюю поправку на станцию
            var originalClosure = _rows
                .Where(r => r.OriginalDeltaH.HasValue)
                .Sum(r => r.OriginalDeltaH!.Value);
            var closureToDistribute = TargetClosure - originalClosure;
            var adjustableCount = _rows.Count(r => r.OriginalDeltaH.HasValue);
            CorrectionPerStation = adjustableCount > 0 ? closureToDistribute / adjustableCount : 0;

            // Подписываемся обратно
            foreach (var row in _rows)
            {
                row.PropertyChanged += OnDesignRowPropertyChanged;
            }
        }

        private void ApplyDtosToRows(IList<DesignPointDto> dtos)
        {
            var count = Math.Min(_rows.Count, dtos.Count);
            for (int i = 0; i < count; i++)
            {
                var dto = dtos[i];
                var row = _rows[i];
                row.Distance_m = dto.Distance;
                row.OriginalDeltaH = dto.OriginalDeltaH;
                row.Correction = dto.Correction;
                row.AdjustedDeltaH = dto.AdjustedDeltaH;
                row.DesignedHeight = dto.DesignedHeight;
                row.IsEdited = dto.IsEdited;
                row.OriginalHeight = dto.OriginalHeight;
                row.OriginalDistance = dto.OriginalDistance;
            }
        }
    }
}
