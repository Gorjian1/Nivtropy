using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Nivtropy;
using Nivtropy.Models;

namespace Nivtropy.ViewModels
{
    public class TraverseDesignViewModel : INotifyPropertyChanged
    {
        private readonly RelayCommand _computeCommand;

        public TraverseDesignViewModel()
        {
            _computeCommand = new RelayCommand(_ => Compute(), _ => CanCompute());
            SelectedClass = TraverseAccuracyClass.Presets.First();
            StationsCount = 1;
        }

        private double? _designStartHeight;
        public double? DesignStartHeight
        {
            get => _designStartHeight;
            set
            {
                if (SetField(ref _designStartHeight, value))
                    _computeCommand.RaiseCanExecuteChanged();
            }
        }

        private double? _designTargetHeight;
        public double? DesignTargetHeight
        {
            get => _designTargetHeight;
            set
            {
                if (SetField(ref _designTargetHeight, value))
                    _computeCommand.RaiseCanExecuteChanged();
            }
        }

        private int _stationsCount;
        public int StationsCount
        {
            get => _stationsCount;
            set
            {
                var sanitized = Math.Max(1, value);
                if (SetField(ref _stationsCount, sanitized))
                    _computeCommand.RaiseCanExecuteChanged();
            }
        }

        private TraverseAccuracyClass _selectedClass = null!;
        public TraverseAccuracyClass SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (SetField(ref _selectedClass, value))
                {
                    _computeCommand.RaiseCanExecuteChanged();
                    if (CanCompute())
                    {
                        Compute();
                    }
                }
            }
        }

        public IReadOnlyList<TraverseAccuracyClass> Classes => TraverseAccuracyClass.Presets;

        private double? _averageDeltaPerStation;
        public double? AverageDeltaPerStation
        {
            get => _averageDeltaPerStation;
            private set => SetField(ref _averageDeltaPerStation, value);
        }

        private double? _recommendedSightLength;
        public double? RecommendedSightLength
        {
            get => _recommendedSightLength;
            private set => SetField(ref _recommendedSightLength, value);
        }

        private double? _recommendedDifference;
        public double? RecommendedDifference
        {
            get => _recommendedDifference;
            private set => SetField(ref _recommendedDifference, value);
        }

        private double? _projectedMisclosureAllowance;
        public double? ProjectedMisclosureAllowance
        {
            get => _projectedMisclosureAllowance;
            private set => SetField(ref _projectedMisclosureAllowance, value);
        }

        private string _recommendation = "Введите исходные параметры и нажмите «Рассчитать».";
        public string Recommendation
        {
            get => _recommendation;
            private set => SetField(ref _recommendation, value);
        }

        public ICommand ComputeCommand => _computeCommand;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private bool CanCompute()
        {
            return StationsCount > 0 && DesignStartHeight.HasValue && DesignTargetHeight.HasValue;
        }

        private void Compute()
        {
            if (!CanCompute())
                return;

            var totalDelta = DesignTargetHeight!.Value - DesignStartHeight!.Value;
            AverageDeltaPerStation = totalDelta / StationsCount;
            RecommendedSightLength = SelectedClass.RecommendedSightLength;
            RecommendedDifference = SelectedClass.MaxHdDifferencePerSetup;

            var estimatedLength = StationsCount * SelectedClass.RecommendedSightLength;
            ProjectedMisclosureAllowance = SelectedClass.MisclosureCoefficientMm / 1000.0 * Math.Sqrt(estimatedLength / 1000.0);

            Recommendation = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Распределите Δh {0:+0.000;-0.000;+0.000} м по {1} станц. (~{2:+0.000;-0.000;+0.000} м на станцию).\n" +
                "Длины визиров подбирайте около {3:0} м, удерживая разность BF/FB ≤ {4:0.00} м.\n" +
                "Ожидаемый допуск невязки: ±{5:0.000} м.",
                totalDelta,
                StationsCount,
                AverageDeltaPerStation,
                RecommendedSightLength,
                RecommendedDifference,
                ProjectedMisclosureAllowance ?? 0d);
        }

        public void ApplyFromCalculation(TraverseCalculationViewModel calculation)
        {
            if (calculation == null)
                throw new ArgumentNullException(nameof(calculation));

            StationsCount = Math.Max(1, calculation.Rows.Count);
            SelectedClass = calculation.SelectedClass;
            RecommendedSightLength = SelectedClass.RecommendedSightLength;
            AverageDeltaPerStation = calculation.AverageDelta;

            if (calculation.StartHeight.HasValue)
                DesignStartHeight = calculation.StartHeight;
            if (calculation.FinishHeight.HasValue)
                DesignTargetHeight = calculation.FinishHeight;

            if (CanCompute())
            {
                Compute();
            }
            else
            {
                ProjectedMisclosureAllowance = null;
                Recommendation = "Значения загружены из расчёта. Дополните недостающие данные для проектирования.";
            }
        }
    }
}
