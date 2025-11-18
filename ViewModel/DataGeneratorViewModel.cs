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
                    var pointCode = headers.ContainsKey("Точка")
                        ? worksheet.Cell(row, headers["Точка"]).GetValue<string>()
                        : "";
                    var station = headers.ContainsKey("Станция")
                        ? worksheet.Cell(row, headers["Станция"]).GetValue<string>()
                        : "";

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
                        PointCode = pointCode,
                        StationCode = station,
                        Rb_m = rb,
                        Rf_m = rf,
                        HD_Back_m = hdBack,
                        HD_Fore_m = hdFore,
                        Height_m = height,
                        IsBackSight = rb.HasValue
                    });
                }

                MessageBox.Show($"Сгенерировано {_measurements.Count} измерений", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            // Группируем по станциям
            string? currentStation = null;
            double? currentHeight = null;

            foreach (var m in _measurements)
            {
                if (m.StationCode != currentStation)
                {
                    // Новая станция - выводим высоту точки
                    if (currentHeight.HasValue && !string.IsNullOrEmpty(currentStation))
                    {
                        sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {currentStation,10}               0214|                      |                      |Z {currentHeight:F4} m   | ");
                    }

                    currentStation = m.StationCode;
                    currentHeight = m.Height_m;
                }

                // Выводим отсчеты
                if (m.IsBackSight && m.Rb_m.HasValue)
                {
                    sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {m.PointCode,10}              10214|Rb {m.Rb_m:F4} m   |HD {m.HD_Back_m:F2} m   |                      | ");
                }
                else if (!m.IsBackSight && m.Rf_m.HasValue)
                {
                    sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {m.PointCode,10}              10214|Rf {m.Rf_m:F4} m   |HD {m.HD_Fore_m:F2} m   |                      | ");
                }
            }

            // Завершающие строки
            if (currentHeight.HasValue && !string.IsNullOrEmpty(currentStation))
            {
                sb.AppendLine($"For M5|Adr {lineNumber++,6}|KD1 {currentStation,10}               0214|                      |                      |Z {currentHeight:F4} m   | ");
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
