using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Win32;
using Nivtropy.Models;

namespace Nivtropy.ViewModels
{
    /// <summary>
    /// ViewModel для генерации синтетических данных измерений
    /// </summary>
    public class DataGeneratorViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<GeneratedMeasurement> _measurements = new();
        private readonly ObservableCollection<string> _availableLines = new();
        private readonly HashSet<GeneratedMeasurement> _trackedMeasurements = new();
        private bool _formatNivelir = true;
        private double _stdDevMeasurement = 0.5; // СКО для измерений (мм)
        private double _stdDevGrossError = 2.0; // СКО для грубых ошибок (мм)
        private int _grossErrorFrequency = 10; // Частота грубых ошибок (каждая N-ная станция)
        private string _sourceFilePath = string.Empty;
        private string? _selectedLineName;
        private GeneratedMeasurement? _selectedMeasurement;
        private double? _profileMinHeight;
        private double? _profileMaxHeight;
        private bool _profileRangeCustomized;
        private bool _isUpdatingProfileStats;
        private DragConstraintMode _dragConstraintMode = DragConstraintMode.Free;
        private Random _random = new Random();
        private RelayCommand? _smoothCommand;
        private RelayCommand? _resetEditsCommand;
        private RelayCommand? _moveHorizontalCommand;
        private RelayCommand? _moveVerticalCommand;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DataGeneratorViewModel()
        {
            _measurements.CollectionChanged += Measurements_CollectionChanged;
        }

        public ObservableCollection<GeneratedMeasurement> Measurements => _measurements;

        public ObservableCollection<string> AvailableLines => _availableLines;

        public bool FormatNivelir
        {
            get => _formatNivelir;
            set => SetField(ref _formatNivelir, value);
        }

        public string? SelectedLineName
        {
            get => _selectedLineName;
            set
            {
                if (SetField(ref _selectedLineName, value))
                {
                    UpdateProfileStats();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public GeneratedMeasurement? SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                if (SetField(ref _selectedMeasurement, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public double StdDevMeasurement
        {
            get => _stdDevMeasurement;
            set => SetField(ref _stdDevMeasurement, value);
        }

        public double StdDevGrossError
        {
            get => _stdDevGrossError;
            set => SetField(ref _stdDevGrossError, value);
        }

        public int GrossErrorFrequency
        {
            get => _grossErrorFrequency;
            set => SetField(ref _grossErrorFrequency, value);
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set
            {
                if (SetField(ref _sourceFilePath, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand OpenFileCommand => new RelayCommand(_ => OpenFile());
        public ICommand GenerateCommand => new RelayCommand(_ => Generate(), _ => !string.IsNullOrEmpty(SourceFilePath));
        public ICommand ExportCommand => new RelayCommand(_ => Export(), _ => Measurements.Count > 0);
        public ICommand SmoothCommand => _smoothCommand ??= new RelayCommand(_ => SmoothSelectedMeasurement(), _ => SelectedMeasurement != null);
        public ICommand ResetEditsCommand => _resetEditsCommand ??= new RelayCommand(_ => ResetEdits(), _ => Measurements.Any());
        public ICommand MoveHorizontalCommand => _moveHorizontalCommand ??= new RelayCommand(param => SetHorizontalConstraint(param), _ => Measurements.Any());
        public ICommand MoveVerticalCommand => _moveVerticalCommand ??= new RelayCommand(param => SetVerticalConstraint(param), _ => Measurements.Any());

        public double? ProfileMinHeight
        {
            get => _profileMinHeight;
            set
            {
                if (SetField(ref _profileMinHeight, value) && !_isUpdatingProfileStats)
                {
                    _profileRangeCustomized = true;
                }
            }
        }

        public double? ProfileMaxHeight
        {
            get => _profileMaxHeight;
            set
            {
                if (SetField(ref _profileMaxHeight, value) && !_isUpdatingProfileStats)
                {
                    _profileRangeCustomized = true;
                }
            }
        }

        public DragConstraintMode DragConstraintMode
        {
            get => _dragConstraintMode;
            set
            {
                if (SetField(ref _dragConstraintMode, value))
                {
                    OnPropertyChanged(nameof(IsHorizontalDragConstraint));
                    OnPropertyChanged(nameof(IsVerticalDragConstraint));
                }
            }
        }

        public bool IsHorizontalDragConstraint
        {
            get => DragConstraintMode == DragConstraintMode.LockHorizontal;
            set
            {
                if (value)
                {
                    DragConstraintMode = DragConstraintMode.LockHorizontal;
                }
                else if (DragConstraintMode == DragConstraintMode.LockHorizontal)
                {
                    DragConstraintMode = DragConstraintMode.Free;
                }
            }
        }

        public bool IsVerticalDragConstraint
        {
            get => DragConstraintMode == DragConstraintMode.LockVertical;
            set
            {
                if (value)
                {
                    DragConstraintMode = DragConstraintMode.LockVertical;
                }
                else if (DragConstraintMode == DragConstraintMode.LockVertical)
                {
                    DragConstraintMode = DragConstraintMode.Free;
                }
            }
        }

        private void OpenFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Открыть файл с результатами расчётов",
                Filter = "CSV файлы (*.csv)|*.csv|Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                DefaultExt = ".csv"
            };

            if (dlg.ShowDialog() == true)
            {
                SourceFilePath = dlg.FileName;

                // Автоматически генерируем данные при загрузке файла
                Generate();
            }
        }

        private void Generate()
        {
            try
            {
                _measurements.Clear();

                if (string.IsNullOrEmpty(SourceFilePath) || !File.Exists(SourceFilePath))
                {
                    MessageBox.Show("Файл не выбран или не существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Определяем тип файла по расширению
                var ext = Path.GetExtension(SourceFilePath).ToLowerInvariant();
                if (ext == ".csv")
                {
                    GenerateFromCsv();
                }
                else if (ext == ".xlsx")
                {
                    GenerateFromExcel();
                }
                else
                {
                    MessageBox.Show("Неподдерживаемый формат файла. Используйте CSV или XLSX.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateFromCsv()
        {
            var lines = File.ReadAllLines(SourceFilePath, Encoding.UTF8);
            int index = 1;
            string currentLineName = "Ход 01";
            bool inDataSection = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Пропускаем пустые строки
                if (string.IsNullOrWhiteSpace(line))
                {
                    inDataSection = false;
                    continue;
                }

                // Проверяем начало хода
                if (line.StartsWith("===== НАЧАЛО ХОДА:"))
                {
                    currentLineName = line.Replace("===== НАЧАЛО ХОДА:", "").Replace("=====", "").Trim();
                    inDataSection = false;
                    continue;
                }

                // Проверяем конец хода
                if (line.StartsWith("===== КОНЕЦ ХОДА:"))
                {
                    inDataSection = false;
                    continue;
                }

                // Пропускаем информационную строку
                if (line.StartsWith("Станций:"))
                {
                    continue;
                }

                // Проверяем заголовок таблицы
                if (line.StartsWith("Номер;"))
                {
                    inDataSection = true;
                    continue;
                }

                // Читаем строки данных
                if (inDataSection)
                {
                    var parts = line.Split(';');
                    if (parts.Length >= 12)
                    {
                        var lineName = parts[1];
                        var pointCode = parts[2];
                        var station = parts[3];

                        // Парсим станцию для извлечения BackCode и ForeCode
                        string? backCode = null;
                        string? foreCode = null;
                        if (!string.IsNullOrWhiteSpace(station) && station.Contains("→"))
                        {
                            var stationParts = station.Split(new[] { "→" }, StringSplitOptions.None);
                            if (stationParts.Length == 2)
                            {
                                backCode = stationParts[0].Trim();
                                foreCode = stationParts[1].Trim();
                                if (backCode == "?") backCode = null;
                                if (foreCode == "?") foreCode = null;
                            }
                        }

                        double? rb = ParseNullableDouble(parts[4]);
                        double? rf = ParseNullableDouble(parts[5]);
                        double? height = ParseNullableDouble(parts[10]);

                        // Генерируем расстояния (5-15 метров)
                        double? hdBack = rb.HasValue ? 5.0 + _random.NextDouble() * 10.0 : null;
                        double? hdFore = rf.HasValue ? 5.0 + _random.NextDouble() * 10.0 : null;

                        // Добавляем шум к измерениям
                        if (rb.HasValue)
                        {
                            double noise = GenerateNoise(index);
                            rb = rb.Value + noise / 1000.0;
                        }
                        if (rf.HasValue)
                        {
                            double noise = GenerateNoise(index);
                            rf = rf.Value + noise / 1000.0;
                        }

                        _measurements.Add(new GeneratedMeasurement
                        {
                            Index = index++,
                            LineName = lineName,
                            PointCode = pointCode,
                            StationCode = station,
                            BackPointCode = backCode,
                            ForePointCode = foreCode,
                            Rb_m = rb,
                            Rf_m = rf,
                            HD_Back_m = hdBack,
                            HD_Fore_m = hdFore,
                            Height_m = height,
                            IsBackSight = rb.HasValue,
                            OriginalHeight = height,
                            OriginalHD_Back = hdBack,
                            OriginalHD_Fore = hdFore
                        });
                    }
                }
            }

            // Уведомление убрано - данные генерируются автоматически
        }

        private void GenerateFromExcel()
        {
            // Читаем Excel файл
            using var workbook = new XLWorkbook(SourceFilePath);
            var worksheet = workbook.Worksheets.First();

            // Пропускаем первые 2 строки (сводка и пустая строка)
            int dataStartRow = 3;

            // Находим заголовки в строке 3
            var headers = new System.Collections.Generic.Dictionary<string, int>();
            for (int col = 1; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
            {
                var headerValue = worksheet.Cell(dataStartRow, col).GetValue<string>();
                if (!string.IsNullOrWhiteSpace(headerValue))
                {
                    headers[headerValue] = col;
                }
            }

            // Начинаем читать данные с 4 строки
            int index = 1;
            for (int row = dataStartRow + 1; row <= worksheet.LastRowUsed().RowNumber(); row++)
            {
                var lineName = headers.ContainsKey("Ход")
                    ? worksheet.Cell(row, headers["Ход"]).GetValue<string>()
                    : "Ход 01";
                var pointCode = headers.ContainsKey("Точка")
                    ? worksheet.Cell(row, headers["Точка"]).GetValue<string>()
                    : "";
                var station = headers.ContainsKey("Станция")
                    ? worksheet.Cell(row, headers["Станция"]).GetValue<string>()
                    : "";

                // Парсим станцию для извлечения BackCode и ForeCode
                string? backCode = null;
                string? foreCode = null;
                if (!string.IsNullOrWhiteSpace(station) && station.Contains("→"))
                {
                    var parts = station.Split(new[] { "→" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        backCode = parts[0].Trim();
                        foreCode = parts[1].Trim();
                        if (backCode == "?") backCode = null;
                        if (foreCode == "?") foreCode = null;
                    }
                }

                var rbCell = headers.ContainsKey("Отсчет назад, м")
                    ? worksheet.Cell(row, headers["Отсчет назад, м"])
                    : null;
                var rfCell = headers.ContainsKey("Отсчет вперед, м")
                    ? worksheet.Cell(row, headers["Отсчет вперед, м"])
                    : null;
                var heightCell = headers.ContainsKey("Высота, м")
                    ? worksheet.Cell(row, headers["Высота, м"])
                    : null;

                double? rb = rbCell != null && !rbCell.IsEmpty() ? rbCell.GetValue<double?>() : null;
                double? rf = rfCell != null && !rfCell.IsEmpty() ? rfCell.GetValue<double?>() : null;
                double? height = heightCell != null && !heightCell.IsEmpty() ? heightCell.GetValue<double?>() : null;

                // Генерируем расстояния (5-15 метров)
                double? hdBack = rb.HasValue ? 5.0 + _random.NextDouble() * 10.0 : null;
                double? hdFore = rf.HasValue ? 5.0 + _random.NextDouble() * 10.0 : null;

                // Добавляем шум к измерениям
                if (rb.HasValue)
                {
                    double noise = GenerateNoise(index);
                    rb = rb.Value + noise / 1000.0; // Переводим мм в м
                }
                if (rf.HasValue)
                {
                    double noise = GenerateNoise(index);
                    rf = rf.Value + noise / 1000.0;
                }

                _measurements.Add(new GeneratedMeasurement
                {
                    Index = index++,
                    LineName = lineName,
                    PointCode = pointCode,
                    StationCode = station,
                    BackPointCode = backCode,
                    ForePointCode = foreCode,
                    Rb_m = rb,
                    Rf_m = rf,
                    HD_Back_m = hdBack,
                    HD_Fore_m = hdFore,
                    Height_m = height,
                    IsBackSight = rb.HasValue,
                    OriginalHeight = height,
                    OriginalHD_Back = hdBack,
                    OriginalHD_Fore = hdFore
                });
            }

            // Уведомление убрано - данные генерируются автоматически
        }

        private double GenerateNoise(int index)
        {
            // Проверяем, нужно ли добавить грубую ошибку
            bool isGrossError = GrossErrorFrequency > 0 && index % GrossErrorFrequency == 0;
            double stdDev = isGrossError ? StdDevGrossError : StdDevMeasurement;

            // Генерируем нормально распределённый шум (Box-Muller transform)
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            return randStdNormal * stdDev; // в мм
        }

        private void Export()
        {
            try
            {
                if (FormatNivelir)
                {
                    ExportToNivelir();
                }
                else
                {
                    MessageBox.Show("Формат не выбран", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SmoothSelectedMeasurement()
        {
            if (SelectedMeasurement == null)
                return;

            var target = SelectedMeasurement;
            var lineMeasurements = _measurements
                .Where(m => m.LineName == target.LineName)
                .OrderBy(m => m.Index)
                .ToList();

            var targetIndex = lineMeasurements.IndexOf(target);
            if (targetIndex < 0)
                return;

            void PullHeight(int neighborIndex, double influence)
            {
                var neighbor = lineMeasurements[neighborIndex];
                if (neighbor.Height_m.HasValue && target.Height_m.HasValue)
                {
                    neighbor.Height_m = neighbor.Height_m.Value * (1 - influence) + target.Height_m.Value * influence;
                }
            }

            // Ближайшие точки подтягиваем сильнее
            if (targetIndex > 0)
            {
                PullHeight(targetIndex - 1, 0.5);
            }

            if (targetIndex < lineMeasurements.Count - 1)
            {
                PullHeight(targetIndex + 1, 0.5);
            }

            // Отдалённые точки слегка корректируем для плавности
            if (targetIndex > 1)
            {
                PullHeight(targetIndex - 2, 0.25);
            }

            if (targetIndex < lineMeasurements.Count - 2)
            {
                PullHeight(targetIndex + 2, 0.25);
            }
        }

        private void ResetEdits()
        {
            var targetLine = SelectedLineName;
            var targets = string.IsNullOrWhiteSpace(targetLine)
                ? _measurements.AsEnumerable()
                : _measurements.Where(m => m.LineName == targetLine);

            foreach (var measurement in targets)
            {
                measurement.Height_m = measurement.OriginalHeight;
                measurement.HD_Back_m = measurement.OriginalHD_Back;
                measurement.HD_Fore_m = measurement.OriginalHD_Fore;
            }
        }

        private void SetHorizontalConstraint(object? state)
        {
            if (state is bool isChecked && isChecked)
            {
                DragConstraintMode = DragConstraintMode.LockHorizontal;
            }
            else if (DragConstraintMode == DragConstraintMode.LockHorizontal)
            {
                DragConstraintMode = DragConstraintMode.Free;
            }
        }

        private void SetVerticalConstraint(object? state)
        {
            if (state is bool isChecked && isChecked)
            {
                DragConstraintMode = DragConstraintMode.LockVertical;
            }
            else if (DragConstraintMode == DragConstraintMode.LockVertical)
            {
                DragConstraintMode = DragConstraintMode.Free;
            }
        }

        private void ExportToNivelir()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Файлы Нивелир (*.txt)|*.txt|Все файлы (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"generated_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            var sb = new StringBuilder();
            int lineNumber = 1;

            // Заголовок файла
            sb.AppendLine($"For M5|Adr     {lineNumber++}|TO  {Path.GetFileName(saveFileDialog.FileName)}               |                      |                      |                      | ");
            sb.AppendLine($"For M5|Adr     {lineNumber++}|TO  Start-Line         BF  0214|                      |                      |                      | ");

            // Группируем измерения по ходам
            var traverseGroups = _measurements.GroupBy(m => m.LineName).ToList();

            foreach (var traverse in traverseGroups)
            {
                var traverseMeasurements = traverse.ToList();

                // Вычисляем статистику хода
                double totalLength = 0;
                double totalArmDifference = 0;
                int stationCount = 0;

                foreach (var m in traverseMeasurements)
                {
                    if (m.HD_Back_m.HasValue && m.HD_Fore_m.HasValue)
                    {
                        totalLength += m.HD_Back_m.Value + m.HD_Fore_m.Value;
                        totalArmDifference += Math.Abs(m.HD_Back_m.Value - m.HD_Fore_m.Value);
                        stationCount++;
                    }
                }

                // Разделитель и заголовок хода
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|-- ========================================== | ");
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|-- {traverse.Key,-40} | ");
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|-- Станций: {stationCount,-6}  Длина хода: {totalLength,8:F2} м | ");
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|-- Σ|разность плеч|: {totalArmDifference,8:F4} м          | ");
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|-- ========================================== | ");

                // Данные хода
                string? previousForePoint = null;
                double? previousForeHeight = null;

                foreach (var m in traverseMeasurements)
                {
                    // Выводим высоту предыдущей точки если она есть
                    if (previousForeHeight.HasValue && !string.IsNullOrEmpty(previousForePoint))
                    {
                        sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {previousForePoint,10}               0214|                      |                      |Z {previousForeHeight:F4} m   | ");
                    }

                    // Выводим отсчеты (и Rb и Rf, если оба есть)
                    if (m.Rb_m.HasValue && !string.IsNullOrEmpty(m.BackPointCode))
                    {
                        sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {m.BackPointCode,10}              10214|Rb {m.Rb_m:F4} m   |HD {m.HD_Back_m:F2} m   |                      | ");
                    }

                    if (m.Rf_m.HasValue && !string.IsNullOrEmpty(m.ForePointCode))
                    {
                        sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {m.ForePointCode,10}              10214|Rf {m.Rf_m:F4} m   |HD {m.HD_Fore_m:F2} m   |                      | ");
                    }

                    // Сохраняем переднюю точку для следующей итерации
                    if (m.Rf_m.HasValue)
                    {
                        previousForePoint = m.ForePointCode;
                        previousForeHeight = m.Height_m;
                    }
                }

                // Завершающие строки хода - выводим последнюю точку
                if (previousForeHeight.HasValue && !string.IsNullOrEmpty(previousForePoint))
                {
                    sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {previousForePoint,10}               0214|                      |                      |Z {previousForeHeight:F4} m   | ");
                }

                // Граница хода
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|-- ------------------------------------------ | ");
                sb.AppendLine();
            }

            sb.AppendLine($"For M5|Adr {lineNumber++,6}|TO  End-Line               0214|                      |                      |                      | ");

            File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

            MessageBox.Show($"Данные успешно экспортированы в:\n{saveFileDialog.FileName}",
                "Экспорт завершён",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RefreshAvailableLines()
        {
            var names = _measurements
                .Select(m => m.LineName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            bool changed = names.Count != _availableLines.Count || !_availableLines.SequenceEqual(names);
            if (changed)
            {
                _availableLines.Clear();
                foreach (var name in names)
                {
                    _availableLines.Add(name);
                }
            }

            if (names.Count == 0)
            {
                if (SelectedLineName != null)
                    SelectedLineName = null;
                return;
            }

            if (SelectedLineName == null || !names.Contains(SelectedLineName))
            {
                SelectedLineName = names.First();
            }
        }

        private void Measurements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (GeneratedMeasurement measurement in e.NewItems)
                {
                    TrackMeasurement(measurement);
                }
            }

            if (e.OldItems != null)
            {
                foreach (GeneratedMeasurement measurement in e.OldItems)
                {
                    UntrackMeasurement(measurement);
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ResetTracking();
            }

            RefreshAvailableLines();
            UpdateProfileStats();
            CommandManager.InvalidateRequerySuggested();
        }

        private void Measurement_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GeneratedMeasurement.Height_m)
                || e.PropertyName == nameof(GeneratedMeasurement.HD_Back_m)
                || e.PropertyName == nameof(GeneratedMeasurement.HD_Fore_m))
            {
                UpdateProfileStats();
            }
        }

        private void TrackMeasurement(GeneratedMeasurement measurement)
        {
            if (_trackedMeasurements.Add(measurement))
            {
                measurement.PropertyChanged += Measurement_PropertyChanged;
            }
        }

        private void UntrackMeasurement(GeneratedMeasurement measurement)
        {
            if (_trackedMeasurements.Remove(measurement))
            {
                measurement.PropertyChanged -= Measurement_PropertyChanged;
            }
        }

        private void ResetTracking()
        {
            foreach (var measurement in _trackedMeasurements.ToList())
            {
                measurement.PropertyChanged -= Measurement_PropertyChanged;
            }

            _trackedMeasurements.Clear();

            foreach (var measurement in _measurements)
            {
                TrackMeasurement(measurement);
            }
        }

        private void UpdateProfileStats()
        {
            var filtered = string.IsNullOrWhiteSpace(SelectedLineName)
                ? _measurements
                : _measurements.Where(m => m.LineName == SelectedLineName);

            var heights = filtered
                .Select(m => m.Height_m)
                .Where(h => h.HasValue)
                .Select(h => h!.Value)
                .ToList();

            if (heights.Count == 0)
            {
                _isUpdatingProfileStats = true;
                ProfileMinHeight = null;
                ProfileMaxHeight = null;
                _profileRangeCustomized = false;
                _isUpdatingProfileStats = false;
                return;
            }

            if (!_profileRangeCustomized || !_profileMinHeight.HasValue || !_profileMaxHeight.HasValue)
            {
                _isUpdatingProfileStats = true;
                ProfileMinHeight = heights.Min();
                ProfileMaxHeight = heights.Max();
                _profileRangeCustomized = false;
                _isUpdatingProfileStats = false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private double? ParseNullableDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim()
                .Replace(" ", string.Empty)
                .Replace("\u00A0", string.Empty);

            if (normalized.Contains(',') && !normalized.Contains('.'))
            {
                if (double.TryParse(normalized, NumberStyles.Float, new CultureInfo("ru-RU"), out var ruResult))
                    return ruResult;
            }

            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;

            var swapped = normalized.Replace(',', '.');
            if (double.TryParse(swapped, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return result;

            if (double.TryParse(normalized, NumberStyles.Float, new CultureInfo("ru-RU"), out result))
                return result;

            return null;
        }
    }

    public enum DragConstraintMode
    {
        Free,
        /// <summary>
        /// Ограничивает горизонтальное перемещение (двигаем только высоту).
        /// </summary>
        LockHorizontal,
        /// <summary>
        /// Ограничивает вертикальное перемещение (двигаем только расстояние).
        /// </summary>
        LockVertical
    }
}
