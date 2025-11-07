using System;
using System.Collections.ObjectModel;
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
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            private set
            {
                if (!Equals(_currentView, value))
                {
                    _currentView = value;
                    OnPropertyChanged();
                }
            }
        }
        public DataViewModel DataViewModel { get; } = new();

        private readonly DataViewControl _dataViewControl;

        public MainViewModel()
        {
            _dataViewControl = new DataViewControl { DataContext = DataViewModel };
            CurrentView = _dataViewControl;
        }

        public ObservableCollection<string> Lines { get; } = new();
        private string? _selectedLine;
        public string? SelectedLine
        {
            get => _selectedLine;
            set
            {
                _selectedLine = value;
                OnPropertyChanged();
                DataViewModel.RequestedLineHeader = value;
            }
        }

        public bool ShowZ { get; set; } = true;
        public bool OnlyValid { get; set; } = false;

        public ICommand OpenFileCommand => new RelayCommand(_ => OpenFile());
        public ICommand RefreshCommand => new RelayCommand(_ => Refresh());
        public ICommand ExportCsvCommand => new RelayCommand(_ => ExportCsv(), _ => DataViewModel.Records.Count > 0);
        public ICommand CheckToleranceCommand => new RelayCommand(_ => CheckTolerance(), _ => DataViewModel.Records.Count > 0);

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
                SyncLinesWithData();
                CurrentView = _dataViewControl;
            }
        }

        private void Refresh()
        {
            if (!string.IsNullOrEmpty(DataViewModel.SourcePath))
            {
                DataViewModel.LoadFromFile(DataViewModel.SourcePath);
                SyncLinesWithData();
            }
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
            // Заглушка: здесь позже появится расчёт невязки и допусков
            MessageBox.Show("Проверка допусков будет реализована на шаге обработки.", "Nivtropy");
        }

        private void SyncLinesWithData()
        {
            Lines.Clear();
            foreach (var run in DataViewModel.Runs)
            {
                Lines.Add(run.Header);
            }
            SelectedLine = Lines.FirstOrDefault();
        }
    }
}