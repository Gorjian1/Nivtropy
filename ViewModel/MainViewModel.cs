using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
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
        private readonly RelayCommand _openFileCommand;
        private readonly RelayCommand _refreshCommand;
        private readonly RelayCommand _exportCsvCommand;
        private readonly RelayCommand _checkToleranceCommand;
        private readonly RelayCommand _importTraverseFromDataCommand;
        private readonly RelayCommand _pushCalculationToDesignCommand;

        private readonly DataViewControl _dataViewControl;
        private readonly TraverseCalculationView _calculationView;
        private readonly TraverseDesignView _designView;

        public MainViewModel()
        {
            DataViewModel.PropertyChanged += DataViewModelOnPropertyChanged;
            CalculationViewModel.Rows.CollectionChanged += OnCalculationRowsChanged;

            _openFileCommand = new RelayCommand(_ => OpenFile());
            _refreshCommand = new RelayCommand(_ => Refresh(), _ => !string.IsNullOrEmpty(DataViewModel.SourcePath));
            _exportCsvCommand = new RelayCommand(_ => ExportCsv(), _ => DataViewModel.Records.Count > 0);
            _checkToleranceCommand = new RelayCommand(_ => CheckTolerance(), _ => DataViewModel.Records.Count > 0);
            _importTraverseFromDataCommand = new RelayCommand(_ => ImportTraverseFromData(), _ => DataViewModel.Records.Count > 0);
            _pushCalculationToDesignCommand = new RelayCommand(_ => PushCalculationToDesign(), _ => CalculationViewModel.Rows.Count > 0);

            _dataViewControl = new DataViewControl { DataContext = DataViewModel };
            _calculationView = new TraverseCalculationView { DataContext = CalculationViewModel };
            _designView = new TraverseDesignView { DataContext = DesignViewModel };

            _selectedRibbonIndex = 0;
            UpdateCurrentView();
        }

        public DataViewModel DataViewModel { get; } = new();
        public TraverseCalculationViewModel CalculationViewModel { get; } = new();
        public TraverseDesignViewModel DesignViewModel { get; } = new();

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            private set => SetField(ref _currentView, value);
        }

        private int _selectedRibbonIndex;
        public int SelectedRibbonIndex
        {
            get => _selectedRibbonIndex;
            set
            {
                if (SetField(ref _selectedRibbonIndex, value))
                {
                    UpdateCurrentView();
                }
            }
        }

        private LineSummary? _selectedRun;
        public LineSummary? SelectedRun
        {
            get => _selectedRun;
            set
            {
                if (!ReferenceEquals(_selectedRun, value))
                {
                    _selectedRun = value;
                    DataViewModel.SelectedRun = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand OpenFileCommand => _openFileCommand;
        public ICommand RefreshCommand => _refreshCommand;
        public ICommand ExportCsvCommand => _exportCsvCommand;
        public ICommand CheckToleranceCommand => _checkToleranceCommand;
        public ICommand ImportTraverseFromDataCommand => _importTraverseFromDataCommand;
        public ICommand PushCalculationToDesignCommand => _pushCalculationToDesignCommand;

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

        private void DataViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun))
            {
                if (!ReferenceEquals(_selectedRun, DataViewModel.SelectedRun))
                {
                    _selectedRun = DataViewModel.SelectedRun;
                    OnPropertyChanged(nameof(SelectedRun));
                }
            }
        }

        private void OnCalculationRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _pushCalculationToDesignCommand.RaiseCanExecuteChanged();
        }

        private void UpdateCurrentView()
        {
            object? nextView = _selectedRibbonIndex switch
            {
                0 => _dataViewControl,
                1 => _calculationView,
                2 => _designView,
                _ => _dataViewControl
            };

            if (!ReferenceEquals(CurrentView, nextView))
            {
                CurrentView = nextView;
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
                SelectedRun = DataViewModel.SelectedRun;
                CalculationViewModel.LoadFromData(DataViewModel);
                DesignViewModel.ApplyFromCalculation(CalculationViewModel);
                SelectedRibbonIndex = 0;
                UpdateCommandStates();
            }
        }

        private void Refresh()
        {
            if (string.IsNullOrEmpty(DataViewModel.SourcePath))
                return;

            var previousIndex = SelectedRun?.Index;
            DataViewModel.LoadFromFile(DataViewModel.SourcePath);
            SelectedRun = previousIndex.HasValue
                ? DataViewModel.Runs.FirstOrDefault(r => r.Index == previousIndex.Value)
                : DataViewModel.SelectedRun;
            CalculationViewModel.LoadFromData(DataViewModel);
            UpdateCommandStates();
        }

        private void ExportCsv()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Экспорт CSV",
                Filter = "CSV (*.csv)|*.csv",
                FileName = Path.ChangeExtension(Path.GetFileName(DataViewModel.SourcePath) ?? "export", ".csv")
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
            MessageBox.Show("Проверка допусков будет реализована на шаге обработки.", "Nivtropy");
        }

        private void ImportTraverseFromData()
        {
            CalculationViewModel.LoadFromData(DataViewModel);
            SelectedRibbonIndex = 1;
            UpdateCommandStates();
        }

        private void PushCalculationToDesign()
        {
            DesignViewModel.ApplyFromCalculation(CalculationViewModel);
            SelectedRibbonIndex = 2;
        }

        private void UpdateCommandStates()
        {
            _refreshCommand.RaiseCanExecuteChanged();
            _exportCsvCommand.RaiseCanExecuteChanged();
            _checkToleranceCommand.RaiseCanExecuteChanged();
            _importTraverseFromDataCommand.RaiseCanExecuteChanged();
            _pushCalculationToDesignCommand.RaiseCanExecuteChanged();
        }
    }
}
