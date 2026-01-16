using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nivtropy.Presentation.Views;
using Nivtropy.Services;
using Nivtropy.Services.Dialog;
using Nivtropy.Services.Visualization;
using Nivtropy.Infrastructure.Export;
using Nivtropy.Application.Services;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels
{
    /// <summary>
    /// Главная ViewModel приложения с поддержкой Dependency Injection
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly DataViewControl _dataViewControl;
        private readonly TraverseJournalView _journalView;
        private readonly TraverseDesignView _designView;
        private readonly SettingsView _settingsView;
        private readonly IDialogService _dialogService;

        private object? _currentView;
        private int _selectedRibbonIndex;

        public MainViewModel(
            DataViewModel dataViewModel,
            SettingsViewModel settingsViewModel,
            TraverseCalculationViewModel calculationViewModel,
            TraverseJournalViewModel journalViewModel,
            DataGeneratorViewModel dataGeneratorViewModel,
            IProfileVisualizationService profileVisualizationService,
            IProfileStatisticsService profileStatisticsService,
            ITraverseSystemVisualizationService systemVisualizationService,
            IDialogService dialogService)
        {
            DataViewModel = dataViewModel;
            SettingsViewModel = settingsViewModel;
            SettingsViewModel.Load();
            CalculationViewModel = calculationViewModel;
            JournalViewModel = journalViewModel;
            DataGeneratorViewModel = dataGeneratorViewModel;
            _dialogService = dialogService;

            _dataViewControl = new DataViewControl { DataContext = DataViewModel };
            _journalView = new TraverseJournalView { DataContext = JournalViewModel };
            _designView = new TraverseDesignView { DataContext = DataGeneratorViewModel };
            _settingsView = new SettingsView { DataContext = SettingsViewModel };

            _selectedRibbonIndex = 0;
            CurrentView = _dataViewControl;
            SyncSelectionWithData();
        }

        public DataViewModel DataViewModel { get; }
        public TraverseCalculationViewModel CalculationViewModel { get; }
        public TraverseJournalViewModel JournalViewModel { get; }
        public DataGeneratorViewModel DataGeneratorViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public object? CurrentView
        {
            get => _currentView;
            private set
            {
                if (!ReferenceEquals(_currentView, value))
                {
                    _currentView = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SelectedRibbonIndex
        {
            get => _selectedRibbonIndex;
            set
            {
                if (_selectedRibbonIndex != value)
                {
                    _selectedRibbonIndex = value;
                    OnPropertyChanged();
                    UpdateCurrentView();
                }
            }
        }

        public ICommand OpenFileCommand => new RelayCommand(_ => OpenFile());
        public ICommand RefreshCommand => new RelayCommand(_ => Refresh());
        public ICommand ShowAboutCommand => new RelayCommand(_ => ShowAbout());

        private void UpdateCurrentView()
        {
            CurrentView = SelectedRibbonIndex switch
            {
                0 => _dataViewControl,
                1 => _journalView,
                2 => _designView,
                3 => _settingsView,
                _ => _dataViewControl
            };
        }

        private void OpenFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Открыть файл измерений",
                Filter = "Trimble/CSV (*.dat;*.DAT;*.csv)|*.dat;*.DAT;*.csv|Все файлы (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                DataViewModel.LoadFromFile(dlg.FileName);
                SyncSelectionWithData();
                UpdateCurrentView();

                // Показываем результат валидации при наличии проблем
                ShowValidationResults();
            }
        }

        private void ShowValidationResults()
        {
            var result = DataViewModel.LastValidationResult;
            if (result == null)
                return;

            if (!result.IsValid)
            {
                var sb = new StringBuilder();
                sb.AppendLine("При загрузке данных обнаружены ошибки:");
                sb.AppendLine();
                foreach (var error in result.Errors.Take(10))
                {
                    sb.AppendLine($"• {error}");
                }
                if (result.Errors.Count > 10)
                {
                    sb.AppendLine($"... и ещё {result.Errors.Count - 10} ошибок");
                }
                _dialogService.ShowError(sb.ToString(), "Ошибки валидации");
            }
            else if (result.HasWarnings)
            {
                var sb = new StringBuilder();
                sb.AppendLine("При загрузке данных обнаружены предупреждения:");
                sb.AppendLine();
                foreach (var warning in result.Warnings.Take(10))
                {
                    sb.AppendLine($"• {warning}");
                }
                if (result.Warnings.Count > 10)
                {
                    sb.AppendLine($"... и ещё {result.Warnings.Count - 10} предупреждений");
                }
                _dialogService.ShowWarning(sb.ToString(), "Предупреждения валидации");
            }
        }

        private void Refresh()
        {
            if (!string.IsNullOrEmpty(DataViewModel.SourcePath))
            {
                var previousIndex = DataViewModel.SelectedRun?.Index;
                DataViewModel.LoadFromFile(DataViewModel.SourcePath);
                DataViewModel.SelectedRun = previousIndex.HasValue
                    ? DataViewModel.Runs.FirstOrDefault(r => r.Index == previousIndex.Value)
                    : null;
                if (DataViewModel.SelectedRun == null)
                {
                    SyncSelectionWithData();
                }
            }
        }

        private void SyncSelectionWithData()
        {
            DataViewModel.SelectedRun = DataViewModel.Runs.FirstOrDefault();
        }

        private void ShowAbout()
        {
            var aboutWindow = new Views.AboutWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
        }
    }
}
