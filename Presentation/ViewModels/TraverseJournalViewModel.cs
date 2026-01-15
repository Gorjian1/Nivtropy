using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Nivtropy.Models;
using Nivtropy.Application.Services;
using Nivtropy.Presentation.Services;
using Nivtropy.Presentation.ViewModelss.Base;

namespace Nivtropy.Presentation.ViewModelss
{
    /// <summary>
    /// ViewModel для журнального представления нивелирного хода.
    /// Конвертирует данные из TraverseCalculationViewModel в формат журнала
    /// Использует сервисы для визуализации и статистики (следует SOLID принципам)
    /// </summary>
    public class TraverseJournalViewModel : ViewModelBase
    {
        private readonly TraverseCalculationViewModel _calculationViewModel;
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

        public TraverseJournalViewModel(
            TraverseCalculationViewModel calculationViewModel,
            IProfileVisualizationService visualizationService,
            IProfileStatisticsService statisticsService,
            ITraverseSystemVisualizationService systemVisualizationService)
        {
            _calculationViewModel = calculationViewModel;
            _visualizationService = visualizationService;
            _statisticsService = statisticsService;
            _systemVisualizationService = systemVisualizationService;

            // Подписываемся на изменения в расчётной модели
            ((INotifyCollectionChanged)_calculationViewModel.Rows).CollectionChanged += OnCalculationRowsChanged;
            _calculationViewModel.PropertyChanged += OnCalculationViewModelPropertyChanged;

            // Первоначальное заполнение
            UpdateJournalRows();
            RecalculateStatistics();
        }

        public ObservableCollection<JournalRow> JournalRows => _journalRows;

        /// <summary>
        /// Доступ к расчётной модели для отображения сводных данных
        /// </summary>
        public TraverseCalculationViewModel Calculation => _calculationViewModel;

        /// <summary>
        /// Получаем настройки из расчётной модели
        /// </summary>
        public SettingsViewModel Settings => _calculationViewModel.Settings;

        #region Visualization Properties

        /// <summary>
        /// Текущая статистика профиля
        /// </summary>
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

        /// <summary>
        /// Цвет профиля Z (уравненные высоты)
        /// </summary>
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

        /// <summary>
        /// Цвет профиля Z0 (неуравненные высоты)
        /// </summary>
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

        /// <summary>
        /// Показывать профиль Z
        /// </summary>
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

        /// <summary>
        /// Показывать профиль Z0
        /// </summary>
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

        /// <summary>
        /// Показывать аномалии
        /// </summary>
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

        /// <summary>
        /// Чувствительность обнаружения аномалий (в сигмах)
        /// </summary>
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

        /// <summary>
        /// Ручная минимальная высота для отображения
        /// </summary>
        public double? ManualMinHeight
        {
            get => _manualMinHeight;
            set
            {
                if (_manualMinHeight != value)
                {
                    _manualMinHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Ручная максимальная высота для отображения
        /// </summary>
        public double? ManualMaxHeight
        {
            get => _manualMaxHeight;
            set
            {
                if (_manualMaxHeight != value)
                {
                    _manualMaxHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Имя хода для отображения профиля (null = все ходы)
        /// </summary>
        public string? SelectedProfileLineName
        {
            get => _selectedProfileLineName;
            set
            {
                if (_selectedProfileLineName != value)
                {
                    _selectedProfileLineName = value;
                    OnPropertyChanged();
                    RecalculateStatistics();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Получить опции визуализации профиля
        /// </summary>
        public ProfileVisualizationOptions GetVisualizationOptions()
        {
            return new ProfileVisualizationOptions
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
        }

        /// <summary>
        /// Пересчитать статистику профиля
        /// </summary>
        public void RecalculateStatistics()
        {
            var rows = GetFilteredRows();
            if (rows.Count > 0)
            {
                CurrentStatistics = _statisticsService.CalculateStatistics(rows, SensitivitySigma);
            }
            else
            {
                CurrentStatistics = null;
            }
        }

        /// <summary>
        /// Получить отфильтрованные строки по выбранному ходу
        /// </summary>
        private List<TraverseRow> GetFilteredRows()
        {
            var allRows = _calculationViewModel.Rows;

            if (string.IsNullOrEmpty(SelectedProfileLineName))
            {
                return allRows.ToList();
            }

            return allRows
                .Where(r => string.Equals(r.LineName, SelectedProfileLineName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Получить точки с известными высотами
        /// </summary>
        public HashSet<string> GetKnownHeightPoints()
        {
            var knownPoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var code in _calculationViewModel.Benchmarks.Select(b => b.Code))
            {
                knownPoints.Add(code);
            }

            foreach (var sp in _calculationViewModel.SharedPoints.Where(p => p.IsEnabled).Select(p => p.Code))
            {
                knownPoints.Add(sp);
            }

            return knownPoints;
        }

        /// <summary>
        /// Отрисовать профиль на Canvas
        /// Делегирует вызов сервису визуализации
        /// </summary>
        public void DrawProfile(System.Windows.Controls.Canvas canvas)
        {
            var rows = GetFilteredRows();
            if (rows.Count < 2) return;

            var options = GetVisualizationOptions();
            var knownHeightPoints = GetKnownHeightPoints();

            _visualizationService.DrawProfile(canvas, rows, options, CurrentStatistics, knownHeightPoints);
        }

        /// <summary>
        /// Отрисовать визуализацию системы ходов на Canvas
        /// Делегирует вызов сервису визуализации
        /// </summary>
        public void DrawSystemVisualization(System.Windows.Controls.Canvas canvas)
        {
            _systemVisualizationService.DrawSystemVisualization(canvas, this);
        }

        #endregion

        private void OnCalculationRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateJournalRows();
            RecalculateStatistics();
        }

        private void OnCalculationViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Обновляем журнал при изменении данных расчёта
            if (e.PropertyName == nameof(TraverseCalculationViewModel.Rows))
            {
                UpdateJournalRows();
            }
        }

        /// <summary>
        /// Конвертирует данные из TraverseRow в журнальный формат.
        /// Каждая станция состоит из 3 строк: BackPoint -> Elevation -> ForePoint
        /// Виртуальные станции (только начальная точка без измерений) пропускаются.
        /// </summary>
        private void UpdateJournalRows()
        {
            _journalRows.Clear();

            foreach (var traverseRow in _calculationViewModel.Rows)
            {
                // Пропускаем виртуальные станции (только начальная точка без измерений)
                // Их данные уже содержатся в BackCode/BackHeight первой реальной станции
                bool isVirtualStation = string.IsNullOrWhiteSpace(traverseRow.ForeCode) && !traverseRow.DeltaH.HasValue;
                if (isVirtualStation)
                {
                    continue;
                }

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
