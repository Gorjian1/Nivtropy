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
        private double _stdDevMeasurement = 0.5; // СКО для измерений (мм)
        private double _stdDevGrossError = 2.0; // СКО для грубых ошибок (мм)
        private int _grossErrorFrequency = 10; // Частота грубых ошибок (каждая N-ная станция)
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

                // Парсим информационную строку о ходе
                if (line.StartsWith("Станций:"))
                {
                    currentTraverseInfo = ParseTraverseInfo(line, currentLineName);
                    if (!traverses.ContainsKey(currentLineName))
                    {
                        traverses[currentLineName] = (currentTraverseInfo, new System.Collections.Generic.List<GeneratedMeasurement>());
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
                        double? height = !string.IsNullOrWhiteSpace(parts[10]) ? double.Parse(parts[10], System.Globalization.CultureInfo.InvariantCulture) : null;

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
        /// Формат: "Станций: {count}; Длина назад: {back} м; Длина вперёд: {fore} м; Общая длина: {total} м; Накопление плеч: {accum} м"
        /// </summary>
        private TraverseInfo ParseTraverseInfo(string line, string lineName)
        {
            var info = new TraverseInfo { LineName = lineName };

            var parts = line.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                if (trimmed.StartsWith("Станций:"))
                {
                    var value = trimmed.Replace("Станций:", "").Trim();
                    if (int.TryParse(value, out var stationCount))
                        info.StationCount = stationCount;
                }
                else if (trimmed.StartsWith("Длина назад:"))
                {
                    var value = trimmed.Replace("Длина назад:", "").Replace("м", "").Trim();
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var length))
                        info.TotalLengthBack_m = length;
                }
                else if (trimmed.StartsWith("Длина вперёд:"))
                {
                    var value = trimmed.Replace("Длина вперёд:", "").Replace("м", "").Trim();
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var length))
                        info.TotalLengthFore_m = length;
                }
                else if (trimmed.StartsWith("Общая длина:"))
                {
                    var value = trimmed.Replace("Общая длина:", "").Replace("м", "").Trim();
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var length))
                        info.TotalLength_m = length;
                }
                else if (trimmed.StartsWith("Накопление плеч:"))
                {
                    var value = trimmed.Replace("Накопление плеч:", "").Replace("м", "").Trim();
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var accum))
                        info.ArmAccumulation_m = accum;
                }
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

            var stationsWithData = measurements.Where(m => m.Rb_m.HasValue || m.Rf_m.HasValue).ToList();
            if (stationsWithData.Count == 0)
                return;

            // Генерируем базовые расстояния (5-15 метров)
            var baseDistancesBack = new System.Collections.Generic.List<double>();
            var baseDistancesFore = new System.Collections.Generic.List<double>();

            double sumBack = 0;
            double sumFore = 0;

            foreach (var m in stationsWithData)
            {
                double distBack = m.Rb_m.HasValue ? 5.0 + _random.NextDouble() * 10.0 : 0;
                double distFore = m.Rf_m.HasValue ? 5.0 + _random.NextDouble() * 10.0 : 0;

                baseDistancesBack.Add(distBack);
                baseDistancesFore.Add(distFore);

                sumBack += distBack;
                sumFore += distFore;
            }

            // Масштабируем расстояния, чтобы сумма соответствовала целевым значениям
            double scaleBack = sumBack > 0 ? info.TotalLengthBack_m / sumBack : 1.0;
            double scaleFore = sumFore > 0 ? info.TotalLengthFore_m / sumFore : 1.0;

            for (int i = 0; i < stationsWithData.Count; i++)
            {
                var m = stationsWithData[i];

                if (m.Rb_m.HasValue)
                    m.HD_Back_m = baseDistancesBack[i] * scaleBack;

                if (m.Rf_m.HasValue)
                    m.HD_Fore_m = baseDistancesFore[i] * scaleFore;
            }
        }

        /// <summary>
        /// Генерирует отсчеты с шумом, сохраняя исходные превышения
        /// </summary>
        private void GenerateReadingsForTraverse(System.Collections.Generic.List<GeneratedMeasurement> measurements, TraverseInfo info)
        {
            if (measurements.Count == 0)
                return;

            foreach (var m in measurements)
            {
                // Добавляем шум к измерениям
                if (m.Rb_m.HasValue)
                {
                    double noise = GenerateNoise(m.Index);
                    m.Rb_m = m.Rb_m.Value + noise / 1000.0; // Переводим мм в м
                }

                if (m.Rf_m.HasValue)
                {
                    double noise = GenerateNoise(m.Index);
                    m.Rf_m = m.Rf_m.Value + noise / 1000.0; // Переводим мм в м
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
