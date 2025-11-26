using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Nivtropy.Models;

namespace Nivtropy.ViewModels
{
    /// <summary>
    /// ViewModel для журнального представления нивелирного хода.
    /// Конвертирует данные из TraverseCalculationViewModel в формат журнала
    /// (каждая станция = 3 строки: задняя точка, превышение, передняя точка)
    /// </summary>
    public class TraverseJournalViewModel : INotifyPropertyChanged
    {
        private readonly TraverseCalculationViewModel _calculationViewModel;
        private readonly ObservableCollection<JournalRow> _journalRows = new();

        public TraverseJournalViewModel(TraverseCalculationViewModel calculationViewModel)
        {
            _calculationViewModel = calculationViewModel;

            // Подписываемся на изменения в расчётной модели
            ((INotifyCollectionChanged)_calculationViewModel.Rows).CollectionChanged += OnCalculationRowsChanged;
            _calculationViewModel.PropertyChanged += OnCalculationViewModelPropertyChanged;

            // Первоначальное заполнение
            UpdateJournalRows();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<JournalRow> JournalRows => _journalRows;

        /// <summary>
        /// Доступ к расчётной модели для отображения сводных данных
        /// </summary>
        public TraverseCalculationViewModel Calculation => _calculationViewModel;

        public int StationsCount => _calculationViewModel.StationsCount;

        public double? Closure => _calculationViewModel.Closure;

        public bool IsClosureWithinTolerance => _calculationViewModel.IsClosureWithinTolerance;

        public double? AllowableClosure => _calculationViewModel.AllowableClosure;

        public double? TotalAverageLength => _calculationViewModel.TotalAverageLength;

        public Method? SelectedMethod => _calculationViewModel.SelectedMethod;

        public TraverseClass? SelectedClass => _calculationViewModel.SelectedClass;

        /// <summary>
        /// Получаем настройки из расчётной модели
        /// </summary>
        public SettingsViewModel Settings => _calculationViewModel.Settings;

        private void OnCalculationRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateJournalRows();
        }

        private void OnCalculationViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Обновляем журнал при изменении данных расчёта
            if (e.PropertyName == nameof(TraverseCalculationViewModel.Rows))
            {
                UpdateJournalRows();
            }

            if (e.PropertyName == nameof(TraverseCalculationViewModel.StationsCount))
                OnPropertyChanged(nameof(StationsCount));

            if (e.PropertyName == nameof(TraverseCalculationViewModel.Closure))
            {
                OnPropertyChanged(nameof(Closure));
                OnPropertyChanged(nameof(IsClosureWithinTolerance));
            }

            if (e.PropertyName == nameof(TraverseCalculationViewModel.AllowableClosure))
                OnPropertyChanged(nameof(AllowableClosure));

            if (e.PropertyName == nameof(TraverseCalculationViewModel.TotalAverageLength))
                OnPropertyChanged(nameof(TotalAverageLength));

            if (e.PropertyName == nameof(TraverseCalculationViewModel.SelectedMethod))
                OnPropertyChanged(nameof(SelectedMethod));

            if (e.PropertyName == nameof(TraverseCalculationViewModel.SelectedClass))
                OnPropertyChanged(nameof(SelectedClass));
        }

        /// <summary>
        /// Конвертирует данные из TraverseRow в журнальный формат
        /// Каждая станция TraverseRow преобразуется в 3 строки JournalRow
        /// </summary>
        private void UpdateJournalRows()
        {
            _journalRows.Clear();

            foreach (var traverseRow in _calculationViewModel.Rows)
            {
                // Строка 1: Задняя точка (BackCode)
                _journalRows.Add(new JournalRow
                {
                    RowType = JournalRowType.BackPoint,
                    StationNumber = traverseRow.Index,
                    LineName = traverseRow.LineName,
                    LineSummary = traverseRow.LineSummary,
                    PointCode = traverseRow.BackCode,
                    Z0 = traverseRow.BackHeightZ0,
                    Z = traverseRow.BackHeight
                });

                // Строка 2: Превышение (средняя строка с расчётными данными)
                _journalRows.Add(new JournalRow
                {
                    RowType = JournalRowType.Elevation,
                    StationNumber = traverseRow.Index,
                    LineName = traverseRow.LineName,
                    LineSummary = traverseRow.LineSummary,
                    StationLength = traverseRow.StationLength_m,
                    DeltaH = traverseRow.DeltaH,
                    Correction = traverseRow.Correction,
                    AdjustedDeltaH = traverseRow.AdjustedDeltaH
                });

                // Строка 3: Передняя точка (ForeCode)
                _journalRows.Add(new JournalRow
                {
                    RowType = JournalRowType.ForePoint,
                    StationNumber = traverseRow.Index,
                    LineName = traverseRow.LineName,
                    LineSummary = traverseRow.LineSummary,
                    PointCode = traverseRow.ForeCode,
                    Z0 = traverseRow.ForeHeightZ0,
                    Z = traverseRow.ForeHeight
                });
            }

            OnPropertyChanged(nameof(JournalRows));
        }
    }
}
