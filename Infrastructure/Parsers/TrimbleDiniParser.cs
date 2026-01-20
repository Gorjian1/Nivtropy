using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Nivtropy.Application.DTOs;
using Nivtropy.Services.Logging;

namespace Nivtropy.Infrastructure.Parsers
{
    /// <summary>
    /// Парсер для формата Trimble Dini
    /// Формат: [lineNumber] [pointCode] [session?] [time?] [runNumber] [Rb/Rf] [value] [HD] [value] [Z] [value]
    /// </summary>
    public class TrimbleDiniParser : IFormatParser
    {
        private readonly ILoggerService? _logger;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        // Скомпилированные regex для маркеров линий
        private static readonly Regex StartLineRegex = new(@"\bStart-Line\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EndLineRegex = new(@"\bEnd-Line\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MeasurementRepeatedRegex = new(@"\bMeasurement\s+repeated\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RunNumberRegex = new(@"\b(?:BF|FB)\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Regex для определения формата
        private static readonly Regex TrimbleDiniLineRegex = new(@"^\d+\s+", RegexOptions.Compiled);

        public TrimbleDiniParser() : this(null) { }

        public TrimbleDiniParser(ILoggerService? logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public int GetFormatScore(string[] sampleLines)
        {
            var nonEmpty = sampleLines.Take(50).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (nonEmpty.Length == 0) return 0;

            int trimbleDiniCount = nonEmpty.Count(line => TrimbleDiniLineRegex.IsMatch(line.TrimStart()));
            return (int)(100.0 * trimbleDiniCount / nonEmpty.Length);
        }

        /// <inheritdoc/>
        public IEnumerable<MeasurementDto> Parse(string[] lines, string? filePath = null, string? synonymsConfigPath = null)
        {
            foreach (var rawLine in lines)
            {
                var line = rawLine ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var record = ParseLine(line);

                if (record == null)
                    continue;

                // Пропускаем строки с ##### (ошибочные измерения)
                if (record.IsInvalidMeasurement)
                    continue;

                // Пропускаем строки с маркерами "Measurement repeated" (не содержат данных)
                if (record.LineMarker == "Measurement-Repeated")
                    continue;

                // Пропускаем строки, которые содержат только Z или Sh
                var isLineMarker = record.LineMarker == "Start-Line" || record.LineMarker == "End-Line";
                if (!record.Rb_m.HasValue && !record.Rf_m.HasValue && !isLineMarker)
                    continue;

                yield return record;
            }
        }

        /// <summary>
        /// Парсит одну строку в формате Trimble Dini
        /// </summary>
        private static MeasurementDto? ParseLine(string line)
        {
            var record = new MeasurementDto();

            // Проверка на специальные маркеры
            if (StartLineRegex.IsMatch(line))
            {
                record.LineMarker = "Start-Line";
                var runMatch = RunNumberRegex.Match(line);
                if (runMatch.Success)
                {
                    record.StationCode = runMatch.Groups[1].Value;
                    record.OriginalLineNumber = runMatch.Groups[1].Value;
                }
                return record;
            }

            if (EndLineRegex.IsMatch(line))
            {
                record.LineMarker = "End-Line";
                return record;
            }

            if (MeasurementRepeatedRegex.IsMatch(line))
            {
                record.LineMarker = "Measurement-Repeated";
                return record;
            }

            // Проверка на ошибочные измерения (#####)
            if (line.Contains("#####"))
            {
                record.IsInvalidMeasurement = true;
                return record;
            }

            // Парсим обычную строку данных
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length < 2)
                return null;

            // Пропускаем номер строки (первый токен - всегда число)
            int tokenIndex = 1;

            // Извлекаем код точки (Target)
            if (tokenIndex < tokens.Length && !string.IsNullOrWhiteSpace(tokens[tokenIndex]))
            {
                record.Target = tokens[tokenIndex];
                tokenIndex++;
            }

            // Пропускаем номер сессии и время (если есть)
            while (tokenIndex < tokens.Length && (tokens[tokenIndex].Contains(":") || tokens[tokenIndex].Length <= 2))
            {
                tokenIndex++;
            }

            // Извлекаем номер хода (StationCode)
            if (tokenIndex < tokens.Length)
            {
                record.StationCode = tokens[tokenIndex];
                tokenIndex++;
            }

            // Парсим параметры (Rb, Rf, HD, Z, Sh, dz, Db, Df)
            while (tokenIndex < tokens.Length)
            {
                var param = tokens[tokenIndex];
                tokenIndex++;

                if (tokenIndex >= tokens.Length)
                    break;

                var value = tokens[tokenIndex];
                tokenIndex++;

                var parsedValue = ParseValue(value);

                switch (param.ToUpperInvariant())
                {
                    case "RB":
                        record.Rb_m = parsedValue;
                        break;
                    case "RF":
                        record.Rf_m = parsedValue;
                        break;
                    case "HD":
                        // Определяем, для какого отсчета это расстояние
                        if (record.Rb_m.HasValue && !record.HdBack_m.HasValue)
                            record.HdBack_m = parsedValue;
                        else if (record.Rf_m.HasValue && !record.HdFore_m.HasValue)
                            record.HdFore_m = parsedValue;
                        break;
                    case "Z":
                        record.Z_m = parsedValue;
                        break;
                    // Пропускаем другие параметры (Sh, dz, Db, Df)
                }
            }

            // Устанавливаем Mode
            if (record.Rb_m.HasValue && record.Rf_m.HasValue)
                record.Mode = "BF";
            else if (record.Rb_m.HasValue)
                record.Mode = "B";
            else if (record.Rf_m.HasValue)
                record.Mode = "F";

            return record;
        }

        /// <summary>
        /// Парсит числовое значение из строки
        /// </summary>
        private static double? ParseValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Replace(',', '.');

            if (double.TryParse(value, NumberStyles.Float, CI, out var parsed))
                return parsed;

            return null;
        }
    }
}
