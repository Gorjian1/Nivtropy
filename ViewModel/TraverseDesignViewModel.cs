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
                return;
            }

            var items = TraverseBuilder.Build(
                _dataViewModel.Records.Where(r => ReferenceEquals(r.LineSummary, SelectedRun)),
                SelectedRun);

            var originalClosure = items.Where(r => r.DeltaH.HasValue).Sum(r => r.DeltaH!.Value);
            ActualClosure = items.Count > 0 ? originalClosure : null;

            var adjustableCount = items.Count(r => r.DeltaH.HasValue);
            CorrectionPerStation = adjustableCount > 0 ? (TargetClosure - originalClosure) / adjustableCount : 0;

            double runningHeight = StartHeight;
            double adjustedSum = 0;

            foreach (var row in items)
            {
                double correction = row.DeltaH.HasValue ? CorrectionPerStation : 0;
                double? adjustedDelta = row.DeltaH.HasValue ? row.DeltaH + correction : null;

                if (adjustedDelta.HasValue)
                {
                    runningHeight += adjustedDelta.Value;
                    adjustedSum += adjustedDelta.Value;
                }

                _rows.Add(new DesignRow
                {
                    Index = row.Index,
                    Station = string.IsNullOrWhiteSpace(row.BackCode) && string.IsNullOrWhiteSpace(row.ForeCode)
                        ? row.LineName
                        : $"{row.BackCode ?? "?"} → {row.ForeCode ?? "?"}",
                    OriginalDeltaH = row.DeltaH,
                    Correction = correction,
                    AdjustedDeltaH = adjustedDelta,
                    DesignedHeight = runningHeight
                });
            }

            DesignedClosure = items.Count > 0 ? adjustedSum : null;
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
