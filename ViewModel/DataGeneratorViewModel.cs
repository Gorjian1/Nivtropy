using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private bool _formatNivelir = true;
        private double _stdDevMeasurement = 0.3; // СКО для измерений (мм)
        private double _stdDevGrossError = 2.0; // СКО для грубых ошибок (мм)
        private int _grossErrorFrequency = 0; // Частота грубых ошибок (каждая N-ная станция) - 0 = отключено
        private string _sourceFilePath = string.Empty;
        private Random _random = new Random();

        public event PropertyChangedEventHandler? PropertyChanged;

        public DataGeneratorViewModel()
        {
        }

        public ObservableCollection<GeneratedMeasurement> Measurements => _measurements;

        public bool FormatNivelir
        {
            get => _formatNivelir;
            set => SetField(ref _formatNivelir, value);
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
            set => SetField(ref _sourceFilePath, value);
        }

        public ICommand OpenFileCommand => new RelayCommand(_ => OpenFile());
        public ICommand GenerateCommand => new RelayCommand(_ => Generate(), _ => !string.IsNullOrEmpty(SourceFilePath));
        public ICommand ExportCommand => new RelayCommand(_ => Export(), _ => Measurements.Count > 0);

        private void OpenFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Открыть файл с результатами расчётов",
                Filter = "Excel файлы (*.xlsx)|*.xlsx|CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                SourceFilePath = dlg.FileName;
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

            // Сначала парсим все данные и группируем по ходам
            var traverses = new System.Collections.Generic.Dictionary<string, (TraverseInfo Info, System.Collections.Generic.List<GeneratedMeasurement> Measurements)>();

            string currentLineName = "Ход 01";
            TraverseInfo? currentTraverseInfo = null;
            bool inDataSection = false;
            int globalIndex = 1;

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
                    currentTraverseInfo = null;
                    continue;
                }

                // Проверяем конец хода
                if (line.StartsWith("===== КОНЕЦ ХОДА:"))
                {
                    inDataSection = false;
                    currentTraverseInfo = null;
                    continue;
                }

                // Парсим информационную строку о ходе (заголовки)
                if (line.StartsWith("Станций;"))
                {
                    // Следующая строка содержит данные
                    if (i + 1 < lines.Length)
                    {
                        var dataLine = lines[i + 1].Trim();
                        currentTraverseInfo = ParseTraverseInfo(dataLine, currentLineName);
                        if (!traverses.ContainsKey(currentLineName))
                        {
                            traverses[currentLineName] = (currentTraverseInfo, new System.Collections.Generic.List<GeneratedMeasurement>());
                        }
                        i++; // Пропускаем следующую строку, так как уже обработали
                    }
                    continue;
                }

                // Проверяем заголовок таблицы
                if (line.StartsWith("Номер;"))
                {
                    inDataSection = true;
                    continue;
                }

                // Читаем строки данных
                if (inDataSection && currentTraverseInfo != null)
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

                        double? rb = !string.IsNullOrWhiteSpace(parts[4]) ? double.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture) : null;
                        double? rf = !string.IsNullOrWhiteSpace(parts[5]) ? double.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture) : null;
                        double? deltaH = !string.IsNullOrWhiteSpace(parts[6]) ? double.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture) : null;
                        // Используем Z0 (высота без поправки на невязку) из parts[9]
                        double? height = !string.IsNullOrWhiteSpace(parts[9]) ? double.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture) : null;

                        var measurement = new GeneratedMeasurement
                        {
                            Index = globalIndex++,
                            LineName = lineName,
                            PointCode = pointCode,
                            StationCode = station,
                            BackPointCode = backCode,
                            ForePointCode = foreCode,
                            Rb_m = rb,
                            Rf_m = rf,
                            HD_Back_m = null, // Будет рассчитано позже
                            HD_Fore_m = null, // Будет рассчитано позже
                            Height_m = height,
                            IsBackSight = rb.HasValue
                        };

                        traverses[currentLineName].Measurements.Add(measurement);
                    }
                }
            }

            // Теперь обрабатываем каждый ход
            foreach (var kvp in traverses)
            {
                var traverseInfo = kvp.Value.Info;
                var measurements = kvp.Value.Measurements;

                // Генерируем правильные расстояния и отсчеты
                GenerateDistancesForTraverse(measurements, traverseInfo);
                GenerateReadingsForTraverse(measurements, traverseInfo);
            }

            // Добавляем все измерения в коллекцию
            foreach (var kvp in traverses)
            {
                foreach (var m in kvp.Value.Measurements)
                {
                    _measurements.Add(m);
                }
            }

            MessageBox.Show($"Сгенерировано {_measurements.Count} измерений из {traverses.Count} ходов", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Парсит информационную строку о ходе
        /// Формат: "{stationCount};{lengthBack};{lengthFore};{totalLength};{armAccumulation}"
        /// Порядок: Станций, Длина назад (м), Длина вперёд (м), Общая длина (м), Накопление плеч (м)
        /// </summary>
        private TraverseInfo ParseTraverseInfo(string line, string lineName)
        {
            var info = new TraverseInfo { LineName = lineName };

            var parts = line.Split(';');

            // Парсим по позициям
            if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out var stationCount))
            {
                info.StationCount = stationCount;
            }

            if (parts.Length >= 2 && double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lengthBack))
            {
                info.TotalLengthBack_m = lengthBack;
            }

            if (parts.Length >= 3 && double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lengthFore))
            {
                info.TotalLengthFore_m = lengthFore;
            }

            if (parts.Length >= 4 && double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var totalLength))
            {
                info.TotalLength_m = totalLength;
            }

            if (parts.Length >= 5 && double.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var armAccumulation))
            {
                info.ArmAccumulation_m = armAccumulation;
            }

            return info;
        }

        /// <summary>
        /// Генерирует расстояния для всех измерений в ходе так, чтобы сумма соответствовала данным из файла
        /// </summary>
        private void GenerateDistancesForTraverse(System.Collections.Generic.List<GeneratedMeasurement> measurements, TraverseInfo info)
        {
            if (measurements.Count == 0)
                return;

            // Подсчитываем количество отсчетов назад и вперед
            var backMeasurements = measurements.Where(m => m.Rb_m.HasValue).ToList();
            var foreMeasurements = measurements.Where(m => m.Rf_m.HasValue).ToList();

            if (backMeasurements.Count == 0 && foreMeasurements.Count == 0)
                return;

            // Генерируем базовые расстояния (5-15 метров)
            var baseDistancesBack = backMeasurements.Select(_ => 5.0 + _random.NextDouble() * 10.0).ToList();
            var baseDistancesFore = foreMeasurements.Select(_ => 5.0 + _random.NextDouble() * 10.0).ToList();

            double sumBack = baseDistancesBack.Sum();
            double sumFore = baseDistancesFore.Sum();

            // Если данные о длинах есть, масштабируем; иначе используем базовые расстояния
            double scaleBack = 1.0;
            double scaleFore = 1.0;

            if (info.TotalLengthBack_m > 0 && sumBack > 0)
            {
                scaleBack = info.TotalLengthBack_m / sumBack;
            }

            if (info.TotalLengthFore_m > 0 && sumFore > 0)
            {
                scaleFore = info.TotalLengthFore_m / sumFore;
            }

            // Применяем масштабированные расстояния
            for (int i = 0; i < backMeasurements.Count; i++)
            {
                backMeasurements[i].HD_Back_m = baseDistancesBack[i] * scaleBack;
            }

            for (int i = 0; i < foreMeasurements.Count; i++)
            {
                foreMeasurements[i].HD_Fore_m = baseDistancesFore[i] * scaleFore;
            }
        }

        /// <summary>
        /// Генерирует отсчеты на основе высот точек, имитируя измерения по рейкам
        /// </summary>
        private void GenerateReadingsForTraverse(System.Collections.Generic.List<GeneratedMeasurement> measurements, TraverseInfo info)
        {
            if (measurements.Count == 0)
                return;

            // Генерируем базовую высоту инструмента (горизонт инструмента)
            // Обычно это высота точки + высота рейки (1-2 метра)
            double baseInstrumentHeight = 1.5;

            foreach (var m in measurements)
            {
                // Генерируем отсчеты на основе высоты точки
                // Отсчет по рейке = Горизонт инструмента - Высота точки

                if (m.Rb_m.HasValue && m.Height_m.HasValue)
                {
                    // Для задней точки: генерируем случайный горизонт инструмента
                    double instrumentHeight = m.Height_m.Value + baseInstrumentHeight + (_random.NextDouble() - 0.5) * 0.5;

                    // Отсчет = Горизонт - Высота точки
                    double reading = instrumentHeight - m.Height_m.Value;

                    // Добавляем небольшой шум
                    double noise = GenerateNoise(m.Index);
                    m.Rb_m = reading + noise / 1000.0; // Переводим мм в м
                }

                if (m.Rf_m.HasValue && m.Height_m.HasValue)
                {
                    // Для передней точки используем тот же подход
                    double instrumentHeight = m.Height_m.Value + baseInstrumentHeight + (_random.NextDouble() - 0.5) * 0.5;

                    // Отсчет = Горизонт - Высота точки
                    double reading = instrumentHeight - m.Height_m.Value;

                    // Добавляем небольшой шум
                    double noise = GenerateNoise(m.Index);
                    m.Rf_m = reading + noise / 1000.0; // Переводим мм в м
                }
            }
        }

        private void GenerateFromExcel()
        {
            // Excel файлы не поддерживают информацию о ходе в структурированном виде
            // Предлагаем пользователю сохранить Excel как CSV и использовать CSV формат
            MessageBox.Show(
                "Для работы с файлами Excel, пожалуйста, экспортируйте их в формат CSV.\n\n" +
                "CSV формат сохраняет всю информацию о ходах (длины, накопление плеч, невязки),\n" +
                "что необходимо для корректной генерации данных.",
                "Используйте CSV формат",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

        private void ExportToNivelir()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Файлы DAT (*.dat)|*.dat|Все файлы (*.*)|*.*",
                DefaultExt = "dat",
                FileName = $"generated_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.dat"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            var sb = new StringBuilder();
            int lineNumber = 1;

            // Заголовок файла
            var fileName = Path.GetFileName(saveFileDialog.FileName).PadRight(27);
            sb.AppendLine($"For M5|Adr     {lineNumber++}|TO  {fileName}|                      |                      |                      | ");

            // Группируем измерения по ходам
            var traverseGroups = _measurements.GroupBy(m => m.LineName).ToList();
            int traverseIndex = 0;

            foreach (var traverse in traverseGroups)
            {
                var traverseMeasurements = traverse.ToList();
                var lineCode = (214 + traverseIndex).ToString("0000");

                // Start-Line для хода
                sb.AppendLine($"For M5|Adr     {lineNumber++}|TO  Start-Line         BF  {lineCode}|                      |                      |                      | ");

                // Обрабатываем каждое измерение
                string? currentBackPoint = null;
                double? currentBackHeight = null;

                foreach (var m in traverseMeasurements)
                {
                    // Обрабатываем заднюю точку
                    if (m.Rb_m.HasValue && !string.IsNullOrEmpty(m.BackPointCode))
                    {
                        // Если это новая точка (не та же что и предыдущая передняя)
                        if (currentBackPoint != m.BackPointCode)
                        {
                            currentBackPoint = m.BackPointCode;
                            currentBackHeight = m.Height_m;

                            // Выводим высоту задней точки
                            var pointPadded = m.BackPointCode.PadRight(10);
                            sb.AppendLine($"For M5|Adr   {lineNumber++,3}|KD1 {pointPadded}       {lineCode}|                      |                      |Z      {currentBackHeight,10:F4} m   | ");
                        }

                        // Выводим отсчет назад
                        var pointPadded2 = m.BackPointCode.PadRight(10);
                        sb.AppendLine($"For M5|Adr   {lineNumber++,3}|KD1 {pointPadded2}      1{lineCode}|Rb       {m.Rb_m,8:F4} m   |HD         {m.HD_Back_m,6:F2} m   |                      | ");
                    }

                    // Обрабатываем переднюю точку
                    if (m.Rf_m.HasValue && !string.IsNullOrEmpty(m.ForePointCode))
                    {
                        // Выводим отсчет вперед
                        var pointPadded3 = m.ForePointCode.PadRight(10);
                        sb.AppendLine($"For M5|Adr   {lineNumber++,3}|KD1 {pointPadded3}      1{lineCode}|Rf       {m.Rf_m,8:F4} m   |HD         {m.HD_Fore_m,6:F2} m   |                      | ");

                        // Выводим высоту передней точки
                        var height = m.Height_m ?? 0;
                        sb.AppendLine($"For M5|Adr   {lineNumber++,3}|KD1 {pointPadded3}       {lineCode}|                      |                      |Z      {height,10:F4} m   | ");

                        currentBackPoint = m.ForePointCode;
                        currentBackHeight = height;
                    }
                }

                // End-Line для хода
                sb.AppendLine($"For M5|Adr   {lineNumber++,3}|TO  End-Line               {lineCode}|                      |                      |                      | ");

                traverseIndex++;
            }

            File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

            MessageBox.Show($"Данные успешно экспортированы в:\n{saveFileDialog.FileName}",
                "Экспорт завершён",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
    }
}
