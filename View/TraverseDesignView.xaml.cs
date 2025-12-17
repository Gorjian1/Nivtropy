using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using Nivtropy.Models;
using Nivtropy.Services.Visualization;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseDesignView : UserControl
    {
        private readonly GeneratedProfileVisualizationService _visualizationService = new();
        private readonly HashSet<GeneratedMeasurement> _trackedMeasurements = new();

        private DataGeneratorViewModel? ViewModel => DataContext as DataGeneratorViewModel;

        public TraverseDesignView()
        {
            InitializeComponent();

            DataContextChanged += TraverseDesignView_DataContextChanged;
        }

        private void TraverseDesignView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DataGeneratorViewModel oldVm)
            {
                oldVm.Measurements.CollectionChanged -= Measurements_CollectionChanged;
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
                DetachMeasurementHandlers(oldVm);
            }

            if (e.NewValue is DataGeneratorViewModel newVm)
            {
                newVm.Measurements.CollectionChanged += Measurements_CollectionChanged;
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                AttachMeasurementHandlers(newVm);
            }

            RedrawProfile();
        }

        private void Measurements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<GeneratedMeasurement> collection)
            {
                if (e.NewItems != null)
                {
                    foreach (GeneratedMeasurement measurement in e.NewItems)
                    {
                        TrackMeasurement(measurement);
                    }
                }

                if (e.OldItems != null)
                {
                    foreach (GeneratedMeasurement measurement in e.OldItems)
                    {
                        UntrackMeasurement(measurement);
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    ResetTracking(collection);
                }
            }

            RedrawProfile();
        }

        private void GeneratedProfileCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            RedrawProfile();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataGeneratorViewModel.SelectedLineName))
            {
                RedrawProfile();
            }
        }

        private void Measurement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GeneratedMeasurement.Height_m)
                || e.PropertyName == nameof(GeneratedMeasurement.HD_Back_m)
                || e.PropertyName == nameof(GeneratedMeasurement.HD_Fore_m))
            {
                RedrawProfile();
            }
        }

        private void RedrawProfile()
        {
            if (ViewModel == null)
                return;

            var selectedLine = ViewModel.SelectedLineName;
            var measurements = string.IsNullOrWhiteSpace(selectedLine)
                ? ViewModel.Measurements.ToList()
                : ViewModel.Measurements.Where(m => m.LineName == selectedLine).ToList();

            _visualizationService.DrawProfile(GeneratedProfileCanvas, measurements);
        }

        private void AttachMeasurementHandlers(DataGeneratorViewModel viewModel)
        {
            ResetTracking(viewModel.Measurements);
        }

        private void DetachMeasurementHandlers(DataGeneratorViewModel viewModel)
        {
            foreach (var measurement in _trackedMeasurements.ToList())
            {
                measurement.PropertyChanged -= Measurement_PropertyChanged;
                _trackedMeasurements.Remove(measurement);
            }
        }

        private void TrackMeasurement(GeneratedMeasurement measurement)
        {
            if (_trackedMeasurements.Add(measurement))
            {
                measurement.PropertyChanged += Measurement_PropertyChanged;
            }
        }

        private void UntrackMeasurement(GeneratedMeasurement measurement)
        {
            if (_trackedMeasurements.Remove(measurement))
            {
                measurement.PropertyChanged -= Measurement_PropertyChanged;
            }
        }

        private void ResetTracking(IEnumerable<GeneratedMeasurement> measurements)
        {
            foreach (var measurement in _trackedMeasurements.ToList())
            {
                measurement.PropertyChanged -= Measurement_PropertyChanged;
            }

            _trackedMeasurements.Clear();

            foreach (var measurement in measurements)
            {
                TrackMeasurement(measurement);
            }
        }
    }
}
