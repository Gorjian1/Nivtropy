using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nivtropy.Models;

namespace Nivtropy.Infrastructure.Export
{
    /// <summary>
    /// Реализация сервиса экспорта в формат Nivelir (Leica FOR формат)
    /// </summary>
    public class NivelorExportService : INivelorExportService
    {
        /// <summary>
        /// Экспортирует измерения в формат Nivelir и сохраняет в файл
        /// </summary>
        public void Export(IEnumerable<GeneratedMeasurement> measurements, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var content = ExportToString(measurements, fileName);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        /// <summary>
        /// Экспортирует измерения в формат Nivelir и возвращает содержимое как строку
        /// </summary>
        public string ExportToString(IEnumerable<GeneratedMeasurement> measurements, string fileName)
        {
            var sb = new StringBuilder();
            int lineNumber = 1;

            // Заголовок файла
            sb.AppendLine($"For M5|Adr     {lineNumber++}|TO  {fileName,-23}|                      |                      |                      | ");
            sb.AppendLine($"For M5|Adr     {lineNumber++}|TO  Start-Line         BF  0214|                      |                      |                      | ");

            // Группируем измерения по ходам
            var traverseGroups = measurements.GroupBy(m => m.LineName).ToList();

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

            return sb.ToString();
        }
    }
}
