using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Nivtropy;
using Nivtropy.Models;

namespace Nivtropy.ViewModels
{
    public class TraverseCalculationViewModel : INotifyPropertyChanged
    {
        private readonly RelayCommand _addRowCommand;
        private readonly RelayCommand _removeSelectedCommand;
        private readonly RelayCommand _clearCommand;
        private readonly RelayCommand _calculateCommand;

        public TraverseCalculationViewModel()
        {
            _addRowCommand = new RelayCommand(_ => AddRow());
            _removeSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedRow != null);
            _clearCommand = new RelayCommand(_ => ClearRows(), _ => Rows.Count > 0);
            _calculateCommand = new RelayCommand(_ => Calculate(), _ => Rows.Count > 0);

            SelectedClass = TraverseAccuracyClass.Presets.First();
        }

        public ObservableCollection<TraverseRow> Rows { get; } = new();

        private TraverseRow? _selectedRow;
        public TraverseRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                {
                    _removeSelectedCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private double? _startHeight;
        public double? StartHeight
        {
            get => _startHeight;
            set => SetField(ref _startHeight, value);
        }

        private double? _sumDeltaH;
        public double? SumDeltaH
        {
            get => _sumDeltaH;
            private set => SetField(ref _sumDeltaH, value);
        }

        private double? _averageDelta;
        public double? AverageDelta
        {
            get => _averageDelta;
            private set => SetField(ref _averageDelta, value);
        }

        private double? _finishHeight;
        public double? FinishHeight
        {
            get => _finishHeight;
            private set => SetField(ref _finishHeight, value);
        }

        private double _totalBackLength;
        public double TotalBackLength
        {
            get => _totalBackLength;
            private set => SetField(ref _totalBackLength, value);
        }

        private double _totalForeLength;
        public double TotalForeLength
        {
            get => _totalForeLength;
            private set => SetField(ref _totalForeLength, value);
        }

        private double _totalImbalance;
        public double TotalImbalance
        {
            get => _totalImbalance;
            private set => SetField(ref _totalImbalance, value);
        }

        private double? _allowedMisclosure;
        public double? AllowedMisclosure
        {
            get => _allowedMisclosure;
            private set => SetField(ref _allowedMisclosure, value);
        }

        private string _misclosureVerdict = "Расчёт ещё не выполнялся.";
        public string MisclosureVerdict
        {
            get => _misclosureVerdict;
            private set => SetField(ref _misclosureVerdict, value);
        }

        private string _hdVerdict = "Баланс BF/FB не рассчитан.";
        public string HdVerdict
        {
            get => _hdVerdict;
            private set => SetField(ref _hdVerdict, value);
        }

        private TraverseAccuracyClass _selectedClass = null!;
        public TraverseAccuracyClass SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (SetField(ref _selectedClass, value))
                {
                    if (Rows.Count > 0)
                    {
                        Calculate();
                    }
                }
            }
        }

        public IReadOnlyList<TraverseAccuracyClass> Classes => TraverseAccuracyClass.Presets;

        public ICommand AddRowCommand => _addRowCommand;
        public ICommand RemoveSelectedCommand => _removeSelectedCommand;
        public ICommand ClearCommand => _clearCommand;
        public ICommand CalculateCommand => _calculateCommand;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void LoadFromData(DataViewModel source)
        {
            Rows.Clear();

            var grouped = source.Records.GroupBy(r => r.LineSummary).OrderBy(g => g.Key?.Index ?? 0);
            foreach (var group in grouped)
            {
                var run = group.Key;
                int index = 1;
                foreach (var record in group.OrderBy(r => r.ShotIndexWithinLine ?? index))
                {
                    var row = new TraverseRow
                    {
                        LineName = run?.DisplayName ?? "Ход",
                        Index = record.ShotIndexWithinLine ?? index,
                        BackCode = record.Target,
                        ForeCode = record.StationCode,
                        Rb_m = record.Rb_m,
                        Rf_m = record.Rf_m,
                        HdBack_m = record.HD_m,
                        HdFore_m = record.HD_m
                    };
                    Rows.Add(row);
                    index++;
                }
            }

            ResetResults();
            _clearCommand.RaiseCanExecuteChanged();
            _calculateCommand.RaiseCanExecuteChanged();
            _removeSelectedCommand.RaiseCanExecuteChanged();
            SelectedRow = Rows.FirstOrDefault();
        }

        private void AddRow()
        {
            int nextIndex = Rows.Count > 0 ? Rows.Max(r => r.Index) + 1 : 1;
            Rows.Add(new TraverseRow
            {
                LineName = Rows.LastOrDefault()?.LineName ?? "Ход",
                Index = nextIndex
            });
            _clearCommand.RaiseCanExecuteChanged();
            _calculateCommand.RaiseCanExecuteChanged();
        }

        private void RemoveSelected()
        {
            if (SelectedRow == null)
                return;

            Rows.Remove(SelectedRow);
            SelectedRow = null;
            _clearCommand.RaiseCanExecuteChanged();
            _calculateCommand.RaiseCanExecuteChanged();
        }

        private void ClearRows()
        {
            Rows.Clear();
            ResetResults();
            _clearCommand.RaiseCanExecuteChanged();
            _calculateCommand.RaiseCanExecuteChanged();
            _removeSelectedCommand.RaiseCanExecuteChanged();
        }

        private void Calculate()
        {
            if (Rows.Count == 0)
            {
                ResetResults();
                return;
            }

            foreach (var row in Rows)
            {
                row.HdWithinTolerance = null;
                row.StatusNote = string.Empty;
            }

            var deltaValues = Rows.Where(r => r.DeltaH.HasValue).Select(r => r.DeltaH!.Value).ToList();
            SumDeltaH = deltaValues.Count > 0 ? deltaValues.Sum() : null;
            AverageDelta = deltaValues.Count > 0 ? deltaValues.Average() : null;

            FinishHeight = (StartHeight.HasValue && SumDeltaH.HasValue)
                ? StartHeight.Value + SumDeltaH.Value
                : null;

            TotalBackLength = Rows.Where(r => r.HdBack_m.HasValue).Sum(r => r.HdBack_m!.Value);
            TotalForeLength = Rows.Where(r => r.HdFore_m.HasValue).Sum(r => r.HdFore_m!.Value);
            TotalImbalance = Math.Abs(TotalBackLength - TotalForeLength);

            var tolerance = SelectedClass.MaxHdDifferencePerSetup;
            foreach (var row in Rows)
            {
                if (row.HdImbalance_m.HasValue)
                {
                    var within = row.HdImbalance_m.Value <= tolerance;
                    row.HdWithinTolerance = within;
                    row.StatusNote = within ? "В норме" : "Превышен допуск BF/FB";
                }
                else
                {
                    row.HdWithinTolerance = null;
                    row.StatusNote = "Не указаны длины визир.";
                }
            }

            HdVerdict = TotalImbalance <= tolerance * Math.Max(1, Rows.Count)
                ? "Суммарный баланс BF/FB в норме."
                : "Суммарный баланс BF/FB превышает допуск.";

            var averageLength = (TotalBackLength + TotalForeLength) / 2.0;
            if (averageLength > 0)
            {
                var allowed = SelectedClass.MisclosureCoefficientMm / 1000.0 * Math.Sqrt(averageLength / 1000.0);
                AllowedMisclosure = allowed;
                if (SumDeltaH.HasValue)
                {
                    MisclosureVerdict = Math.Abs(SumDeltaH.Value) <= allowed
                        ? "Невязка в пределах допуска."
                        : "Невязка превышает допускаемую величину!";
                }
                else
                {
                    MisclosureVerdict = "Невязка не рассчитана — заполните Δh.";
                }
            }
            else
            {
                AllowedMisclosure = null;
                MisclosureVerdict = "Нет данных о длинах визир для оценки невязки.";
            }
        }

        private void ResetResults()
        {
            SumDeltaH = null;
            AverageDelta = null;
            FinishHeight = null;
            TotalBackLength = 0;
            TotalForeLength = 0;
            TotalImbalance = 0;
            AllowedMisclosure = null;
            MisclosureVerdict = "Расчёт ещё не выполнялся.";
            HdVerdict = "Баланс BF/FB не рассчитан.";
        }
    }
}
