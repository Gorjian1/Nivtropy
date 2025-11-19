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
        private double _minDistance = 5.0; // Минимальное расстояние до рейки (м)
        private double _maxDistance = 15.0; // Максимальное расстояние до рейки (м)
        private double _instrumentHeightBase = 1.5; // Базовая высота установки прибора (м)
        private double _instrumentHeightVariation = 0.15; // Вариация высоты прибора (±м)
        private double _stationLengthSpread = 0.5; // Разброс длины станции - разница между HD_Back и HD_Fore (м)
        private double _elevationOffset = 0.0; // Смещение превышения - отклонение от исходного deltaH (мм)
        private string _sourceFilePath = string.Empty;
        private Random _random = new Random();
        private int _currentTraverseIndex = 0;
        private string _currentTraverseName = "";
        private double _maxHeight = 0;
        private double _minHeight = 0;

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

        public double MinDistance
        {
            get => _minDistance;
            set => SetField(ref _minDistance, value);
        }

        public double MaxDistance
        {
            get => _maxDistance;
            set => SetField(ref _maxDistance, value);
        }

        public double InstrumentHeightBase
        {
            get => _instrumentHeightBase;
            set => SetField(ref _instrumentHeightBase, value);
        }

        public double InstrumentHeightVariation
        {
            get => _instrumentHeightVariation;
            set => SetField(ref _instrumentHeightVariation, value);
        }

        public double StationLengthSpread
        {
            get => _stationLengthSpread;
            set => SetField(ref _stationLengthSpread, value);
        }

        public double ElevationOffset
        {
            get => _elevationOffset;
            set => SetField(ref _elevationOffset, value);
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set => SetField(ref _sourceFilePath, value);
        }

        public ICommand OpenFileCommand => new RelayCommand(_ => OpenFile());
        public ICommand GenerateCommand => new RelayCommand(_ => Generate(), _ => !string.IsNullOrEmpty(SourceFilePath));
        public ICommand ExportCommand => new RelayCommand(_ => Export(), _ => Measurements.Count > 0);
        public ICommand PreviousTraverseCommand => new RelayCommand(_ => PreviousTraverse(), _ => CanNavigateTraverses());
        public ICommand NextTraverseCommand => new RelayCommand(_ => NextTraverse(), _ => CanNavigateTraverses());

        public string CurrentTraverseName
        {
            get => _currentTraverseName;
            private set => SetField(ref _currentTraverseName, value);
        }

        public double MaxHeight
        {
            get => _maxHeight;
            private set => SetField(ref _maxHeight, value);
        }

        public double MinHeight
        {
            get => _minHeight;
            private set => SetField(ref _minHeight, value);
        }

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
                        // Используем высоту Z0 (без поправки на невязку) из parts[9]
                        double? height = !string.IsNullOrWhiteSpace(parts[9]) ? double.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture) : null;

                        var measurement = new GeneratedMeasurement
                        {
                            Index = globalIndex++,
                            LineName = lineName,
                            PointCode = pointCode,
                            StationCode = station,
                            BackPointCode = backCode,
                            ForePointCode = foreCode,
                            Rb_m = rb,  // Используется только как флаг (HasValue)
                            Rf_m = rf,  // Используется только как флаг (HasValue)
                            HD_Back_m = null, // Будет рассчитано позже
                            HD_Fore_m = null, // Будет рассчитано позже
                            DeltaH_m = deltaH, // Превышение из CSV
                            Height_m = height, // Высота из CSV (колонка "Высота непров. (м)")
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

                // Высоты уже прочитаны из CSV (колонка "Высота непров. (м)")
                // CalculateHeightsForTraverse больше не нужен

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

            // Обновляем профиль для первого хода
            _currentTraverseIndex = 0;
            UpdateCurrentTraverse();
        }

        /// <summary>
        /// Рассчитывает высоты точек на основе превышений
        /// Стартовая высота: 100 м
        /// </summary>
        private void CalculateHeightsForTraverse(System.Collections.Generic.List<GeneratedMeasurement> measurements)
        {
            if (measurements.Count == 0)
                return;

            // Стартовая высота для первой точки
            double currentHeight = 100.0;  // метры

            foreach (var m in measurements)
            {
                // Для задней точки устанавливаем текущую высоту
                if (m.Rb_m.HasValue)
                {
                    m.Height_m = currentHeight;
                }

                // Применяем превышение для передней точки
                if (m.Rf_m.HasValue && m.DeltaH_m.HasValue)
                {
                    currentHeight += m.DeltaH_m.Value;
                    m.Height_m = currentHeight;
                }
                else if (m.Rf_m.HasValue)
                {
                    // Если нет превышения, используем текущую высоту
                    m.Height_m = currentHeight;
                }
            }
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
        /// Расстояния назад и вперед генерируются попарно с учетом разброса длины станции
        /// </summary>
        private void GenerateDistancesForTraverse(System.Collections.Generic.List<GeneratedMeasurement> measurements, TraverseInfo info)
        {
            if (measurements.Count == 0)
                return;

            // Группируем по станциям для попарной генерации
            var stationGroups = measurements.GroupBy(m => m.StationCode).ToList();

            var stationsWithDistances = new System.Collections.Generic.List<(double back, double fore)>();

            // Генерируем базовые расстояния для каждой станции (пара: назад+вперед)
            foreach (var stationGroup in stationGroups)
            {
                var stationMeasurements = stationGroup.ToList();
                var hasBack = stationMeasurements.Any(m => m.Rb_m.HasValue);
                var hasFore = stationMeasurements.Any(m => m.Rf_m.HasValue);

                if (!hasBack && !hasFore)
                    continue;

                // Генерируем базовое расстояние для станции
                double distanceRange = MaxDistance - MinDistance;
                double baseDistance = MinDistance + _random.NextDouble() * distanceRange;

                // Добавляем разброс между назад и вперед
                double spread = (_random.NextDouble() - 0.5) * StationLengthSpread;

                double distBack = hasBack ? baseDistance + spread : 0;
                double distFore = hasFore ? baseDistance - spread : 0;

                stationsWithDistances.Add((distBack, distFore));
            }

            // Рассчитываем суммы для масштабирования
            double sumBack = stationsWithDistances.Where(s => s.back > 0).Sum(s => s.back);
            double sumFore = stationsWithDistances.Where(s => s.fore > 0).Sum(s => s.fore);

            // Масштабируем под целевые длины
            double scaleBack = 1.0;
            double scaleFore = 1.0;

            if (info.TotalLengthBack_m > 0 && sumBack > 0)
                scaleBack = info.TotalLengthBack_m / sumBack;

            if (info.TotalLengthFore_m > 0 && sumFore > 0)
                scaleFore = info.TotalLengthFore_m / sumFore;

            // Применяем масштабированные расстояния к измерениям
            int stationIndex = 0;
            foreach (var stationGroup in stationGroups)
            {
                if (stationIndex >= stationsWithDistances.Count)
                    break;

                var (distBack, distFore) = stationsWithDistances[stationIndex];

                foreach (var m in stationGroup)
                {
                    if (m.Rb_m.HasValue && distBack > 0)
                        m.HD_Back_m = distBack * scaleBack;

                    if (m.Rf_m.HasValue && distFore > 0)
                        m.HD_Fore_m = distFore * scaleFore;
                }

                stationIndex++;
            }
        }

        /// <summary>
        /// Генерирует отсчеты на основе превышений из CSV
        /// Rb и Rf генерируются так, чтобы Rb - Rf = deltaH (с учетом настроек смещения)
        /// </summary>
        private void GenerateReadingsForTraverse(System.Collections.Generic.List<GeneratedMeasurement> measurements, TraverseInfo info)
        {
            if (measurements.Count == 0)
                return;

            // Группируем измерения по станциям
            var stationGroups = measurements.GroupBy(m => m.StationCode).ToList();

            // Если есть смещение превышений, подготавливаем случайные смещения с нулевой суммой
            var elevationOffsets = new System.Collections.Generic.List<double>();

            if (ElevationOffset > 0)
            {
                // Генерируем случайные смещения для каждой станции
                for (int i = 0; i < stationGroups.Count; i++)
                {
                    double offset = (_random.NextDouble() - 0.5) * 2 * ElevationOffset / 1000.0; // мм → м
                    elevationOffsets.Add(offset);
                }

                // Компенсируем сумму смещений, чтобы невязка не изменилась
                double totalOffset = elevationOffsets.Sum();
                double compensation = totalOffset / stationGroups.Count;
                for (int i = 0; i < elevationOffsets.Count; i++)
                {
                    elevationOffsets[i] -= compensation;
                }
            }

            int stationIndex = 0;
            foreach (var stationGroup in stationGroups)
            {
                var stationMeasurements = stationGroup.ToList();

                // Находим измерения с задним и передним отсчетами
                var backMeasurement = stationMeasurements.FirstOrDefault(m => m.Rb_m.HasValue);
                var foreMeasurement = stationMeasurements.FirstOrDefault(m => m.Rf_m.HasValue);

                if (backMeasurement == null || foreMeasurement == null)
                {
                    stationIndex++;
                    continue;
                }

                // Берем превышение из CSV
                double deltaH = backMeasurement.DeltaH_m ?? 0;

                // Применяем смещение, если настроено
                if (ElevationOffset > 0 && stationIndex < elevationOffsets.Count)
                {
                    deltaH += elevationOffsets[stationIndex];
                }

                // Генерируем Rb в разумных пределах (0.5 - 2.5 м на рейке)
                // Добавляем вариацию на основе высоты установки прибора
                double rbBase = InstrumentHeightBase + (_random.NextDouble() - 0.5) * (InstrumentHeightVariation * 2);
                rbBase = Math.Max(0.5, Math.Min(2.5, rbBase)); // ограничиваем разумными пределами

                // Добавляем шум измерения
                double noiseBack = GenerateNoise(backMeasurement.Index);
                backMeasurement.Rb_m = rbBase + noiseBack / 1000.0; // мм → м

                // Rf = Rb - deltaH (чтобы Rb - Rf = deltaH)
                double noiseFore = GenerateNoise(foreMeasurement.Index);
                foreMeasurement.Rf_m = backMeasurement.Rb_m - deltaH + noiseFore / 1000.0; // мм → м

                // Убеждаемся что Rf в разумных пределах
                foreMeasurement.Rf_m = Math.Max(0.3, Math.Min(3.0, foreMeasurement.Rf_m.Value));

                stationIndex++;
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

        private bool CanNavigateTraverses()
        {
            return _measurements.GroupBy(m => m.LineName).Count() > 0;
        }

        private void PreviousTraverse()
        {
            var traverses = _measurements.GroupBy(m => m.LineName).ToList();
            if (traverses.Count == 0) return;

            _currentTraverseIndex = (_currentTraverseIndex - 1 + traverses.Count) % traverses.Count;
            UpdateCurrentTraverse();
        }

        private void NextTraverse()
        {
            var traverses = _measurements.GroupBy(m => m.LineName).ToList();
            if (traverses.Count == 0) return;

            _currentTraverseIndex = (_currentTraverseIndex + 1) % traverses.Count;
            UpdateCurrentTraverse();
        }

        private void UpdateCurrentTraverse()
        {
            var traverses = _measurements.GroupBy(m => m.LineName).ToList();
            if (traverses.Count == 0 || _currentTraverseIndex >= traverses.Count)
            {
                CurrentTraverseName = "Нет данных";
                MaxHeight = 0;
                MinHeight = 0;
                return;
            }

            var currentTraverse = traverses[_currentTraverseIndex];
            CurrentTraverseName = currentTraverse.Key;

            // Вычисляем макс/мин высоты
            var heights = currentTraverse.Where(m => m.Height_m.HasValue).Select(m => m.Height_m!.Value).ToList();
            if (heights.Count > 0)
            {
                MaxHeight = heights.Max();
                MinHeight = heights.Min();
            }
            else
            {
                MaxHeight = 0;
                MinHeight = 0;
            }

            // Событие для перерисовки профиля
            OnPropertyChanged("ProfileDataChanged");
        }

        /// <summary>
        /// Получает измерения текущего хода для отрисовки профиля
        /// </summary>
        public System.Collections.Generic.List<GeneratedMeasurement> GetCurrentTraverseMeasurements()
        {
            var traverses = _measurements.GroupBy(m => m.LineName).ToList();
            if (traverses.Count == 0 || _currentTraverseIndex >= traverses.Count)
                return new System.Collections.Generic.List<GeneratedMeasurement>();

            return traverses[_currentTraverseIndex].ToList();
        }
    }
}
