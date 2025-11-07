using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nivtropy.Models;
using Nivtropy.Views;

namespace Nivtropy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public object? CurrentView { get; set; }
        public DataViewModel DataViewModel { get; } = new();

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
                SyncSelectionWithData();
                CurrentView = new DataViewControl { DataContext = DataViewModel };
                OnPropertyChanged(nameof(CurrentView));
            }
        }

        private void Refresh()
        {
            if (!string.IsNullOrEmpty(DataViewModel.SourcePath))
            {
                var previousIndex = SelectedRun?.Index;
                DataViewModel.LoadFromFile(DataViewModel.SourcePath);
                SelectedRun = previousIndex.HasValue
                    ? DataViewModel.Runs.FirstOrDefault(r => r.Index == previousIndex.Value)
                    : null;
                if (SelectedRun == null)
                {
                    SyncSelectionWithData();
                }
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

        private void SyncSelectionWithData()
        {
            SelectedRun = DataViewModel.Runs.FirstOrDefault();
        }
    }
}