using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nivtropy.Views;

namespace Nivtropy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DataViewControl _dataViewControl;
        private readonly TraverseCalculationView _calculationView;
        private readonly TraverseDesignView _designView;

        private object? _currentView;
        private int _selectedRibbonIndex;

        public MainViewModel()
        {
            DataViewModel = new DataViewModel();
            CalculationViewModel = new TraverseCalculationViewModel(DataViewModel);
            DesignViewModel = new TraverseDesignViewModel(DataViewModel);

            _dataViewControl = new DataViewControl { DataContext = DataViewModel };
            _calculationView = new TraverseCalculationView { DataContext = CalculationViewModel };
            _designView = new TraverseDesignView { DataContext = DesignViewModel };

            _selectedRibbonIndex = 0;
            CurrentView = _dataViewControl;
            SyncSelectionWithData();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public DataViewModel DataViewModel { get; }
        public TraverseCalculationViewModel CalculationViewModel { get; }
        public TraverseDesignViewModel DesignViewModel { get; }

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

        private void UpdateCurrentView()
        {
            CurrentView = SelectedRibbonIndex switch
            {
                0 => _dataViewControl,
                1 => _calculationView,
                2 => _designView,
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
    }
}
