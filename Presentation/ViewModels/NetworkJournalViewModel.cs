using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Services;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.Mappers;
using Nivtropy.Presentation.Visualization;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel для журнального представления нивелирного хода через DDD-сеть.
    /// </summary>
    public class NetworkJournalViewModel : ViewModelBase
    {
        private readonly NetworkViewModel _calculationViewModel;
        private readonly IProfileVisualizationService _visualizationService;
        private readonly IProfileStatisticsService _statisticsService;
        private readonly ITraverseSystemVisualizationService _systemVisualizationService;
        private readonly ObservableCollection<JournalRow> _journalRows = new();

        private ProfileStatistics? _currentStatistics;
        private Color _profileColor = Color.FromRgb(0x19, 0x76, 0xD2);
        private Color _profileZ0Color = Color.FromRgb(0x80, 0x80, 0x80);
        private bool _showZ = true;
        private bool _showZ0 = true;
        private bool _showAnomalies = true;
        private double _sensitivitySigma = 2.5;
        private double? _manualMinHeight;
        private double? _manualMaxHeight;
        private string? _selectedProfileLineName;

        public NetworkJournalViewModel(
            NetworkViewModel calculationViewModel,
            IProfileVisualizationService visualizationService,
            IProfileStatisticsService statisticsService,
            ITraverseSystemVisualizationService systemVisualizationService)
        {
            _calculationViewModel = calculationViewModel;
            _visualizationService = visualizationService;
            _statisticsService = statisticsService;
            _systemVisualizationService = systemVisualizationService;

            ((INotifyCollectionChanged)_calculationViewModel.Rows).CollectionChanged += OnCalculationRowsChanged;
            _calculationViewModel.PropertyChanged += OnCalculationViewModelPropertyChanged;

            UpdateJournalRows();
            RecalculateStatistics();
        }

        public ObservableCollection<JournalRow> JournalRows => _journalRows;

        public NetworkViewModel Calculation => _calculationViewModel;

        public SettingsViewModel Settings => _calculationViewModel.Settings;

        public ProfileStatistics? CurrentStatistics
        {
            get => _currentStatistics;
            private set
            {
                if (_currentStatistics != value)
                {
                    _currentStatistics = value;
                    OnPropertyChanged();
                }
            }
        }

        public Color ProfileColor
        {
            get => _profileColor;
            set
            {
                if (_profileColor != value)
                {
                    _profileColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public Color ProfileZ0Color
        {
            get => _profileZ0Color;
            set
            {
                if (_profileZ0Color != value)
                {
                    _profileZ0Color = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowZ
        {
            get => _showZ;
            set
            {
                if (_showZ != value)
                {
                    _showZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowZ0
        {
            get => _showZ0;
            set
            {
                if (_showZ0 != value)
                {
                    _showZ0 = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowAnomalies
        {
            get => _showAnomalies;
            set
            {
                if (_showAnomalies != value)
                {
                    _showAnomalies = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SensitivitySigma
        {
            get => _sensitivitySigma;
            set
            {
                if (Math.Abs(_sensitivitySigma - value) > 0.01)
                {
                    _sensitivitySigma = value;
                    OnPropertyChanged();
                    RecalculateStatistics();
                }
            }
        }

        public double? ManualMinHeight
        {
            get => _manualMinHeight;
            set
            {
                if (_manualMinHeight != value)
                {
                    _manualMinHeight = value;
                    OnPropertyChanged();
                    RecalculateStatistics();
                }
            }
        }

        public double? ManualMaxHeight
        {
            get => _manualMaxHeight;
            set
            {
                if (_manualMaxHeight != value)
                {
                    _manualMaxHeight = value;
                    OnPropertyChanged();
                    RecalculateStatistics();
                }
            }
        }

        public string? SelectedProfileLineName
        {
            get => _selectedProfileLineName;
            set
            {
                if (_selectedProfileLineName != value)
                {
                    _selectedProfileLineName = value;
                    OnPropertyChanged();
                }
            }
        }

        public void RecalculateStatistics()
        {
            var rows = GetFilteredRows();
            if (rows.Count > 0)
            {
                var dtos = rows.Select(r => r.ToDto()).ToList();
                CurrentStatistics = _statisticsService.CalculateStatistics(dtos, SensitivitySigma);
            }
            else
            {
                CurrentStatistics = null;
            }
        }

        private List<TraverseRow> GetFilteredRows()
        {
            var allRows = _calculationViewModel.Rows;

            if (string.IsNullOrWhiteSpace(SelectedProfileLineName))
                return allRows.ToList();

            return allRows
                .Where(r => r.LineName == SelectedProfileLineName)
                .ToList();
        }

        public void DrawProfile(System.Windows.Controls.Canvas canvas)
        {
            var rows = GetFilteredRows();
            var options = new ProfileVisualizationOptions
            {
                ShowZ = ShowZ,
                ShowZ0 = ShowZ0,
                ShowAnomalies = ShowAnomalies,
                ProfileColor = ProfileColor,
                ProfileZ0Color = ProfileZ0Color,
                ManualMinHeight = ManualMinHeight,
                ManualMaxHeight = ManualMaxHeight,
                SensitivitySigma = SensitivitySigma
            };

            var knownHeightPoints = _calculationViewModel.Benchmarks
                .Select(b => b.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _visualizationService.DrawProfile(canvas, rows, options, CurrentStatistics, knownHeightPoints);
        }

        public void DrawSystemVisualization(System.Windows.Controls.Canvas canvas)
        {
            _systemVisualizationService.DrawSystemVisualization(canvas, this);
        }

        private void OnCalculationRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateJournalRows();
            RecalculateStatistics();
        }

        private void OnCalculationViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NetworkViewModel.Rows))
            {
                UpdateJournalRows();
                RecalculateStatistics();
            }
        }

        private void UpdateJournalRows()
        {
            _journalRows.Clear();
            foreach (var traverseRow in _calculationViewModel.Rows)
            {
                _journalRows.Add(new JournalRow
                {
                    LineName = traverseRow.LineName,
                    Index = traverseRow.Index,
                    BackCode = traverseRow.BackCode,
                    ForeCode = traverseRow.ForeCode,
                    Rb_m = traverseRow.Rb_m,
                    Rf_m = traverseRow.Rf_m,
                    HdBack_m = traverseRow.HdBack_m,
                    HdFore_m = traverseRow.HdFore_m,
                    DeltaH = traverseRow.DeltaH,
                    Correction = traverseRow.Correction,
                    AdjustedDeltaH = traverseRow.AdjustedDeltaH,
                    BackHeight = traverseRow.BackHeight,
                    ForeHeight = traverseRow.ForeHeight,
                    BackHeightZ0 = traverseRow.BackHeightZ0,
                    ForeHeightZ0 = traverseRow.ForeHeightZ0,
                    LineSummary = traverseRow.LineSummary
                });
            }

            OnPropertyChanged(nameof(JournalRows));
        }
    }
}
