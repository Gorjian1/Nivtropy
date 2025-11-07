using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Nivtropy.Models;

namespace Nivtropy.ViewModels
{
    public class TraverseCalculationViewModel : INotifyPropertyChanged
    {
        private readonly DataViewModel _dataViewModel;

        public TraverseCalculationViewModel(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel;
            _dataViewModel.PropertyChanged += OnDataPropertyChanged;

            RunCaption = "Нет данных для расчёта.";
            TotalsText = "—";
            Analysis = "Загрузите файл и выберите ход для расчётов.";
        }

        public ObservableCollection<TraverseMeasurementRow> Measurements { get; } = new();
        public ObservableCollection<TraverseClassSummary> ClassSummaries { get; } = new();

        private string _runCaption = string.Empty;
        public string RunCaption
        {
            get => _runCaption;
            private set => SetField(ref _runCaption, value);
        }

        private string _totalsText = string.Empty;
        public string TotalsText
        {
            get => _totalsText;
            private set => SetField(ref _totalsText, value);
        }

        private string _analysis = string.Empty;
        public string Analysis
        {
            get => _analysis;
            private set => SetField(ref _analysis, value);
        }

        private double? _totalDelta;
        public double? TotalDelta
        {
            get => _totalDelta;
            private set => SetField(ref _totalDelta, value);
        }

        private double? _bfSum;
        public double? BfSum
        {
            get => _bfSum;
            private set => SetField(ref _bfSum, value);
        }

        private double? _fbSum;
        public double? FbSum
        {
            get => _fbSum;
            private set => SetField(ref _fbSum, value);
        }

        private double? _closureDifference;
        public double? ClosureDifference
        {
            get => _closureDifference;
            private set => SetField(ref _closureDifference, value);
        }

        private double? _totalDistance;
        public double? TotalDistance
        {
            get => _totalDistance;
            private set => SetField(ref _totalDistance, value);
        }

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

        private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun) ||
                e.PropertyName == nameof(DataViewModel.Records) ||
                e.PropertyName == nameof(DataViewModel.Runs))
            {
                Recalculate();
            }
        }

        public void RequestRecalculation() => Recalculate();

        private void Recalculate()
        {
            Measurements.Clear();
            ClassSummaries.Clear();

            var run = _dataViewModel.SelectedRun;
            if (run == null)
            {
                RunCaption = "Ход не выбран.";
                TotalsText = "—";
                Analysis = "Выберите ход для расчёта.";
                TotalDelta = null;
                BfSum = null;
                FbSum = null;
                ClosureDifference = null;
                TotalDistance = null;
                return;
            }

            var records = _dataViewModel.Records
                .Where(r => ReferenceEquals(r.LineSummary, run))
                .OrderBy(r => r.ShotIndexWithinLine ?? int.MaxValue)
                .ToList();

            if (records.Count == 0)
            {
                RunCaption = $"{run.DisplayName}: нет измерений.";
                TotalsText = "—";
                Analysis = "В выбранном ходе нет строк с отсчётами.";
                TotalDelta = null;
                BfSum = null;
                FbSum = null;
                ClosureDifference = null;
                TotalDistance = null;
                return;
            }

            var accumulators = new Dictionary<string, ClassAccumulator>(StringComparer.OrdinalIgnoreCase);
            double cumulative = 0d;
            double? totalDelta = null;
            double? bfTotal = null;
            double? fbTotal = null;
            double totalDistance = 0d;
            int stepIndex = 0;

            foreach (var record in records)
            {
                var normalized = NormalizeClass(record.Mode);
                var delta = record.DeltaH;
                var hd = record.HD_m;

                if (!accumulators.TryGetValue(normalized, out var accumulator))
                {
                    accumulator = new ClassAccumulator();
                    accumulators[normalized] = accumulator;
                }

                if (delta.HasValue)
                {
                    accumulator.Add(delta.Value);
                    totalDelta = (totalDelta ?? 0d) + delta.Value;
                    cumulative += delta.Value;

                    if (hd.HasValue)
                        totalDistance += hd.Value;

                    if (ContainsBf(record.Mode))
                        bfTotal = (bfTotal ?? 0d) + delta.Value;
                    if (ContainsFb(record.Mode))
                        fbTotal = (fbTotal ?? 0d) + delta.Value;
                }

                var row = new TraverseMeasurementRow(
                    ++stepIndex,
                    record.Seq,
                    record.ShotIndexWithinLine,
                    record.PointLabel,
                    record.Mode ?? string.Empty,
                    normalized,
                    record.Rb_m,
                    record.Rf_m,
                    delta,
                    hd,
                    delta.HasValue ? cumulative : (double?)null);

                Measurements.Add(row);
            }

            foreach (var kvp in accumulators.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                ClassSummaries.Add(new TraverseClassSummary(kvp.Key, kvp.Value.Count, kvp.Value.Sum));
            }

            TotalDelta = totalDelta;
            BfSum = bfTotal;
            FbSum = fbTotal;
            ClosureDifference = (bfTotal.HasValue && fbTotal.HasValue) ? bfTotal - fbTotal : null;
            TotalDistance = totalDistance > 0 ? totalDistance : (double?)null;

            RunCaption = $"{run.DisplayName}: {run.RangeDisplay}";
            TotalsText = BuildTotalsText(totalDelta, totalDistance, records.Count);
            Analysis = BuildAnalysis(totalDelta, bfTotal, fbTotal, ClosureDifference, totalDistance, records.Count);
        }

        private static string NormalizeClass(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return "—";

            var upper = mode.Trim().ToUpperInvariant();
            var containsBf = upper.Contains("BF", StringComparison.Ordinal);
            var containsFb = upper.Contains("FB", StringComparison.Ordinal);

            if (containsBf && containsFb)
                return "BFFB";
            if (containsBf)
                return "BF";
            if (containsFb)
                return "FB";

            return upper;
        }

        private static bool ContainsBf(string? mode)
        {
            return !string.IsNullOrWhiteSpace(mode) && mode.ToUpperInvariant().Contains("BF", StringComparison.Ordinal);
        }

        private static bool ContainsFb(string? mode)
        {
            return !string.IsNullOrWhiteSpace(mode) && mode.ToUpperInvariant().Contains("FB", StringComparison.Ordinal);
        }

        private static string BuildTotalsText(double? totalDelta, double totalDistance, int count)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Отсчётов: {0}", count);
            sb.Append(" • ");
            sb.Append(totalDelta.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "ΣΔh = {0:+0.0000;-0.0000;0.0000} м", totalDelta.Value)
                : "ΣΔh = —");

            if (totalDistance > 0)
            {
                sb.Append(" • ");
                sb.AppendFormat(CultureInfo.InvariantCulture, "Длина = {0:0.0} м", totalDistance);
            }

            return sb.ToString();
        }

        private static string BuildAnalysis(double? totalDelta, double? bf, double? fb, double? closure, double totalDistance, int count)
        {
            if (count == 0)
                return "Нет данных для анализа.";

            var sb = new StringBuilder();

            if (totalDelta.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "Суммарная разность: {0:+0.0000;-0.0000;0.0000} м.", totalDelta.Value);
            }
            else
            {
                sb.Append("Разности высот отсутствуют.");
            }

            if (bf.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " BF = {0:+0.0000;-0.0000;0.0000} м.", bf.Value);
            }

            if (fb.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " FB = {0:+0.0000;-0.0000;0.0000} м.", fb.Value);
            }

            if (closure.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " Разность BF-FB = {0:+0.0000;-0.0000;0.0000} м.", closure.Value);

                if (totalDistance > 0)
                {
                    var tolerance = ComputeTolerance(totalDistance);
                    sb.AppendFormat(CultureInfo.InvariantCulture, " Допуск ±{0:0.0000} м (по формуле 0.007√L).", tolerance);
                }
            }

            return sb.ToString();
        }

        private static double ComputeTolerance(double lengthMeters)
        {
            return 0.007 * Math.Sqrt(Math.Max(lengthMeters, 1));
        }

        public string BuildToleranceReport()
        {
            var run = _dataViewModel.SelectedRun;
            if (run == null)
                return "Ход не выбран.";

            if (Measurements.Count == 0)
                return "Для выбранного хода нет данных.";

            var sb = new StringBuilder();
            sb.AppendLine($"{run.DisplayName}: {run.RangeDisplay}");
            sb.AppendLine(TotalsText);

            if (TotalDelta.HasValue)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "ΣΔh (все) = {0:+0.0000;-0.0000;0.0000} м", TotalDelta.Value));
            }

            if (BfSum.HasValue)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "ΣΔh(BF) = {0:+0.0000;-0.0000;0.0000} м", BfSum.Value));
            }

            if (FbSum.HasValue)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "ΣΔh(FB) = {0:+0.0000;-0.0000;0.0000} м", FbSum.Value));
            }

            if (ClosureDifference.HasValue)
            {
                var closure = ClosureDifference.Value;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Δ(BF-FB) = {0:+0.0000;-0.0000;0.0000} м", closure));

                if (TotalDistance.HasValue)
                {
                    var tolerance = ComputeTolerance(TotalDistance.Value);
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Допуск (0.007√L) = ±{0:0.0000} м", tolerance));
                    var verdict = Math.Abs(closure) <= tolerance
                        ? "Невязка в пределах допуска."
                        : "Невязка превышает допуск!";
                    sb.AppendLine(verdict);
                }
            }

            return sb.ToString();
        }

        private sealed class ClassAccumulator
        {
            public int Count { get; private set; }
            public double Sum { get; private set; }

            public void Add(double value)
            {
                Count++;
                Sum += value;
            }
        }
    }
}
