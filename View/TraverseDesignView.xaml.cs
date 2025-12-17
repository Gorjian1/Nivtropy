using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Nivtropy.Models;
using Nivtropy.Services.Visualization;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseDesignView : UserControl
    {
        private readonly GeneratedProfileVisualizationService _visualizationService = new();
        private readonly HashSet<GeneratedMeasurement> _trackedMeasurements = new();
        private readonly Dictionary<Ellipse, ProfilePointVisual> _ellipseMap = new();

        private DataGeneratorViewModel? ViewModel => DataContext as DataGeneratorViewModel;
        private ProfileRenderResult? _lastRenderResult;
        private ProfilePointVisual? _draggingPoint;
        private bool _isDragging;
        private double? _dragStartDistance;
        private double? _dragStartHeight;
        private double? _dragStartBack;
        private double? _dragStartFore;

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
            if (e.PropertyName == nameof(DataGeneratorViewModel.SelectedLineName)
                || e.PropertyName == nameof(DataGeneratorViewModel.ProfileMinHeight)
                || e.PropertyName == nameof(DataGeneratorViewModel.ProfileMaxHeight))
            {
                RedrawProfile();
            }
        }

        private void Measurement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isDragging)
                return;

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

            _lastRenderResult = null;
            _ellipseMap.Clear();

            var selectedLine = ViewModel.SelectedLineName;
            var measurements = string.IsNullOrWhiteSpace(selectedLine)
                ? ViewModel.Measurements.ToList()
                : ViewModel.Measurements.Where(m => m.LineName == selectedLine).ToList();

            _lastRenderResult = _visualizationService.DrawProfile(
                GeneratedProfileCanvas,
                measurements,
                ViewModel.ProfileMinHeight,
                ViewModel.ProfileMaxHeight);

            if (_lastRenderResult != null)
            {
                foreach (var pointVisual in _lastRenderResult.Points)
                {
                    pointVisual.Ellipse.MouseLeftButtonDown += PointEllipse_MouseLeftButtonDown;
                    pointVisual.Ellipse.MouseLeftButtonUp += PointEllipse_MouseLeftButtonUp;
                    _ellipseMap[pointVisual.Ellipse] = pointVisual;
                }
            }
        }

        private void PointEllipse_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse ellipse && _ellipseMap.TryGetValue(ellipse, out var visual))
            {
                _draggingPoint = visual;
                _isDragging = true;
                _dragStartDistance = visual.Point.Distance;
                _dragStartHeight = visual.Point.Height;
                _dragStartBack = visual.Point.Measurement.HD_Back_m;
                _dragStartFore = visual.Point.Measurement.HD_Fore_m;
                ellipse.CaptureMouse();
                e.Handled = true;
            }
        }

        private void PointEllipse_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                EndDrag();
                e.Handled = true;
            }
        }

        private void GeneratedProfileCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _lastRenderResult == null || _draggingPoint == null)
                return;

            var position = e.GetPosition(GeneratedProfileCanvas);
            var transform = _lastRenderResult.Transform;

            var newDistance = transform.CanvasToDistance(position.X);
            var newHeight = transform.CanvasToHeight(position.Y);

            ApplyDragUpdate(_draggingPoint, newDistance, newHeight);
        }

        private void GeneratedProfileCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                EndDrag();
                e.Handled = true;
            }
        }

        private void GeneratedProfileCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                EndDrag();
            }
        }

        private void ApplyDragUpdate(ProfilePointVisual visual, double distance, double height)
        {
            if (_lastRenderResult == null || ViewModel == null)
                return;

            var measurement = visual.Point.Measurement;
            var previousDistance = visual.Index == 0 ? 0 : _lastRenderResult.Points[visual.Index - 1].Point.Distance;
            var constraint = ViewModel.DragConstraintMode;

            var constrainedHeight = constraint == DragConstraintMode.LockVertical
                ? (_dragStartHeight ?? visual.Point.Height)
                : height;

            var startDistance = _dragStartDistance ?? visual.Point.Distance;
            var constrainedDistance = constraint == DragConstraintMode.LockHorizontal
                ? startDistance
                : distance;

            const double minGap = 0.01;
            var startBack = _dragStartBack ?? measurement.HD_Back_m ?? 0;
            var startFore = _dragStartFore ?? measurement.HD_Fore_m ?? 0;

            var minDistance = previousDistance + minGap;
            var currentTotal = Math.Max(minGap * 2, startBack + startFore);
            var maxDistance = Math.Max(minDistance, previousDistance + currentTotal - minGap);

            var desiredDistance = Math.Clamp(constrainedDistance, minDistance, maxDistance);
            var desiredTotal = Math.Max(minGap * 2, Math.Min(currentTotal, desiredDistance - previousDistance));

            // Redistribute distances so moving left grows HD_Back and shrinking HD_Fore, and vice versa.
            var delta = desiredDistance - startDistance;
            var tentativeBack = startBack - delta;
            var newBack = Math.Clamp(tentativeBack, minGap, desiredTotal - minGap);
            var newFore = Math.Max(desiredTotal - newBack, minGap);

            // If clamping back forced the fore below the minimum, rebalance to keep totals consistent.
            if (newFore < minGap)
            {
                newFore = minGap;
                newBack = Math.Max(desiredTotal - newFore, minGap);
            }

            var newDistance = previousDistance + newBack + newFore;

            measurement.Height_m = Math.Round(constrainedHeight, 3);
            measurement.HD_Back_m = Math.Round(newBack, 3);
            measurement.HD_Fore_m = Math.Round(newFore, 3);

            visual.Point.Distance = newDistance;
            visual.Point.Height = measurement.Height_m ?? constrainedHeight;

            UpdateVisualPosition(visual, visual.Point.Distance, visual.Point.Height);
        }

        private void UpdateVisualPosition(ProfilePointVisual visual, double distance, double height)
        {
            if (_lastRenderResult == null)
                return;

            var x = _lastRenderResult.Transform.DistanceToCanvasX(distance);
            var y = _lastRenderResult.Transform.HeightToCanvasY(height);

            Canvas.SetLeft(visual.Ellipse, x - visual.Ellipse.Width / 2);
            Canvas.SetTop(visual.Ellipse, y - visual.Ellipse.Height / 2);

            if (visual.Label != null)
            {
                Canvas.SetLeft(visual.Label, x + 8);
                Canvas.SetTop(visual.Label, y - 10);
                visual.Label.Text = visual.Point.Label;
            }

            visual.Ellipse.ToolTip = $"{visual.Point.Label}: {height:F3} м";
        }

        private void EndDrag()
        {
            _isDragging = false;
            _draggingPoint?.Ellipse.ReleaseMouseCapture();
            _draggingPoint = null;
            _dragStartDistance = null;
            _dragStartHeight = null;
            _dragStartBack = null;
            _dragStartFore = null;
            RedrawProfile();
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
