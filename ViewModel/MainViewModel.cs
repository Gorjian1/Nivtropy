using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nivtropy.Views;
using Nivtropy.Services;
using Nivtropy.Services.Visualization;
using Nivtropy.Services.Statistics;

namespace Nivtropy.ViewModels
{
    /// <summary>
    /// Главная ViewModel приложения с поддержкой Dependency Injection
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DataViewControl _dataViewControl;
        private readonly TraverseJournalView _journalView;
        private readonly TraverseDesignView _designView;
        private readonly SettingsView _settingsView;

        private object? _currentView;
        private int _selectedRibbonIndex;

        public MainViewModel(
            IProfileVisualizationService profileVisualizationService,
            IProfileStatisticsService profileStatisticsService,
            ITraverseSystemVisualizationService systemVisualizationService,
            ITraverseBuilder traverseBuilder)
        {
            DataViewModel = new DataViewModel();
            SettingsViewModel = new SettingsViewModel();
            SettingsViewModel.Load();
            CalculationViewModel = new TraverseCalculationViewModel(DataViewModel, SettingsViewModel, traverseBuilder);

            // Создаём JournalViewModel с сервисами из DI
            JournalViewModel = new TraverseJournalViewModel(
                CalculationViewModel,
                profileVisualizationService,
                profileStatisticsService,
                systemVisualizationService);

            DataGeneratorViewModel = new DataGeneratorViewModel();

            _dataViewControl = new DataViewControl { DataContext = DataViewModel };
            _journalView = new TraverseJournalView { DataContext = JournalViewModel };
            _designView = new TraverseDesignView { DataContext = DataGeneratorViewModel };
            _settingsView = new SettingsView { DataContext = SettingsViewModel };

            _selectedRibbonIndex = 0;
            CurrentView = _dataViewControl;
            SyncSelectionWithData();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
                Owner = Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
        }
    }
}
