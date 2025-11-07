using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nivtropy;
using Nivtropy.Models;
using Nivtropy.Views;

namespace Nivtropy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DataViewControl _dataView;
        private readonly TraverseCalculationView _calculationView;
        private readonly TraverseDesignView _designView;

        public MainViewModel()
        {
            DataViewModel = new DataViewModel();
            DataViewModel.PropertyChanged += OnDataViewModelPropertyChanged;

            CalculationViewModel = new TraverseCalculationViewModel(DataViewModel);
            DesignViewModel = new TraverseDesignViewModel(DataViewModel);

            _dataView = new DataViewControl { DataContext = DataViewModel };
            _calculationView = new TraverseCalculationView { DataContext = CalculationViewModel };
            _designView = new TraverseDesignView { DataContext = DesignViewModel };

            SelectedTab = RibbonTab.Main;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            private set => SetField(ref _currentView, value);
        }

        private RibbonTab _selectedTab;
        public RibbonTab SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetField(ref _selectedTab, value))
                {
                    UpdateCurrentView();
                }
            }
        }

        public DataViewModel DataViewModel { get; }
        public TraverseCalculationViewModel CalculationViewModel { get; }
        public TraverseDesignViewModel DesignViewModel { get; }

        public LineSummary? SelectedRun
        {
            get => DataViewModel.SelectedRun;
            set
            {
                if (!ReferenceEquals(DataViewModel.SelectedRun, value))
                {
                    DataViewModel.SelectedRun = value;
                    OnPropertyChanged();
                    CalculationViewModel.RequestRecalculation();
                    DesignViewModel.NotifyRunChanged();
                }
            }
        }

        public ICommand OpenFileCommand => new RelayCommand(_ => OpenFile());
        public ICommand RefreshCommand => new RelayCommand(_ => Refresh());
        public ICommand ExportCsvCommand => new RelayCommand(_ => ExportCsv(), _ => DataViewModel.Records.Count > 0);
        public ICommand CheckToleranceCommand => new RelayCommand(_ => CheckTolerance(), _ => DataViewModel.Records.Count > 0);

        private void UpdateCurrentView()
        {
            CurrentView = SelectedTab switch
            {
                RibbonTab.Main => _dataView,
                RibbonTab.Calculations => _calculationView,
                RibbonTab.Design => _designView,
                _ => _dataView
            };
        }

        private void OnDataViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun))
            {
                OnPropertyChanged(nameof(SelectedRun));
                CalculationViewModel.RequestRecalculation();
                DesignViewModel.NotifyRunChanged();
            }
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
                OnPropertyChanged(nameof(SelectedRun));
                CalculationViewModel.RequestRecalculation();
                DesignViewModel.NotifyRunChanged();
                UpdateCurrentView();
            }
        }

        private void Refresh()
        {
            if (string.IsNullOrEmpty(DataViewModel.SourcePath))
                return;

            var previousIndex = SelectedRun?.Index;
            DataViewModel.LoadFromFile(DataViewModel.SourcePath);
            if (previousIndex.HasValue)
            {
                var run = DataViewModel.Runs.FirstOrDefault(r => r.Index == previousIndex.Value);
                if (run != null)
                {
                    DataViewModel.SelectedRun = run;
                }
            }

            OnPropertyChanged(nameof(SelectedRun));
            CalculationViewModel.RequestRecalculation();
            DesignViewModel.NotifyRunChanged();
        }

        private void ExportCsv()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Экспорт CSV",
                Filter = "CSV (*.csv)|*.csv",
                FileName = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(DataViewModel.SourcePath) ?? "export", ".csv")
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    DataViewModel.ExportCsv(sfd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка экспорта");
                }
            }
        }

        private void CheckTolerance()
        {
            var report = CalculationViewModel.BuildToleranceReport();
            MessageBox.Show(report, "Расчёт BF/FB", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
