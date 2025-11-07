using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Nivtropy;
using Nivtropy.Models;

namespace Nivtropy.ViewModels
{
    public class TraverseDesignViewModel : INotifyPropertyChanged
    {
        private readonly DataViewModel _dataViewModel;

        public TraverseDesignViewModel(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel;
            _dataViewModel.PropertyChanged += OnDataViewPropertyChanged;

            DesignSummary = "Введите исходную и конечную отметку, затем нажмите \"Проектировать\".";

            GenerateDesignCommand = new RelayCommand(_ => GenerateDesign());
            ResetCommand = new RelayCommand(_ => Reset());
        }

        public ObservableCollection<TraverseDesignStep> Steps { get; } = new();

        private string _startElevationText = string.Empty;
        public string StartElevationText
        {
            get => _startElevationText;
            set => SetField(ref _startElevationText, value);
        }

        private string _desiredEndElevationText = string.Empty;
        public string DesiredEndElevationText
        {
            get => _desiredEndElevationText;
            set => SetField(ref _desiredEndElevationText, value);
        }

        private string _designSummary = string.Empty;
        public string DesignSummary
        {
            get => _designSummary;
            private set => SetField(ref _designSummary, value);
        }

        public ICommand GenerateDesignCommand { get; }
        public ICommand ResetCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnDataViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun) || e.PropertyName == nameof(DataViewModel.Records))
            {
                NotifyRunChanged();
            }
        }

        public void NotifyRunChanged()
        {
            Steps.Clear();
            var run = _dataViewModel.SelectedRun;
            if (run == null)
            {
                DesignSummary = "Ход не выбран для проектирования.";
            }
            else
            {
                DesignSummary = $"Проектирование по {run.DisplayName}: {run.RangeDisplay}. Укажите отметки и выполните расчёт.";
            }
        }

        private void GenerateDesign()
        {
            var run = _dataViewModel.SelectedRun;
            if (run == null)
            {
                DesignSummary = "Выберите ход для проектирования.";
                Steps.Clear();
                return;
            }

            if (!TryParseElevation(StartElevationText, out var startElevation) ||
                !TryParseElevation(DesiredEndElevationText, out var endElevation))
            {
                DesignSummary = "Некорректные отметки. Используйте число с точкой или запятой.";
                Steps.Clear();
                return;
            }

            var records = _dataViewModel.Records
                .Where(r => ReferenceEquals(r.LineSummary, run))
                .Where(r => r.DeltaH.HasValue)
                .OrderBy(r => r.ShotIndexWithinLine ?? int.MaxValue)
                .ToList();

            if (records.Count == 0)
            {
                DesignSummary = "В выбранном ходе нет разностей высот для проектирования.";
                Steps.Clear();
                return;
            }

            var actualDelta = records.Sum(r => r.DeltaH!.Value);
            var requiredDelta = endElevation - startElevation;
            var correction = requiredDelta - actualDelta;
            var perShotAdjustment = records.Count > 0 ? correction / records.Count : 0d;

            Steps.Clear();
            var currentElevation = startElevation;
            int index = 1;

            foreach (var record in records)
            {
                var originalDelta = record.DeltaH!.Value;
                var adjustedDelta = originalDelta + perShotAdjustment;
                currentElevation += adjustedDelta;

                Steps.Add(new TraverseDesignStep(
                    index++,
                    record.PointLabel,
                    record.Mode ?? string.Empty,
                    originalDelta,
                    perShotAdjustment,
                    adjustedDelta,
                    currentElevation));
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Стартовая отметка: {0:0.000} м", startElevation));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Требуемая отметка: {0:0.000} м", endElevation));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Фактическая ΣΔh: {0:+0.0000;-0.0000;0.0000} м", actualDelta));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Целевая ΣΔh: {0:+0.0000;-0.0000;0.0000} м", requiredDelta));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Поправка на отсчёт: {0:+0.0000;-0.0000;0.0000} м", perShotAdjustment));
            DesignSummary = sb.ToString();
        }

        private void Reset()
        {
            StartElevationText = string.Empty;
            DesiredEndElevationText = string.Empty;
            Steps.Clear();
            var run = _dataViewModel.SelectedRun;
            DesignSummary = run == null
                ? "Ход не выбран для проектирования."
                : $"Проектирование по {run.DisplayName}: {run.RangeDisplay}. Укажите отметки и выполните расчёт.";
        }

        private static bool TryParseElevation(string text, out double value)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                value = 0;
                return false;
            }

            return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
