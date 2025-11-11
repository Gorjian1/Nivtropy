using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    public class DatParser
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly string[] CandidateEncodings = { "utf-8", "windows-1251", "cp1251", "latin1", "utf-16" };

        private static readonly Dictionary<string, string[]> DefaultSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Rb"] = new[] { "RB", "RBack", "Back", "B" },
            ["Rf"] = new[] { "RF", "RFore", "Fore", "F" },
            ["HDback"] = new[] { "HDBack", "HD_B", "HB" },
            ["HDfore"] = new[] { "HDFore", "HD_F", "HF" },
        };

        public IEnumerable<MeasurementRecord> Parse(string path, string? synonymsConfigPath = null)
        {
            var text = ReadTextSmart(path);
            var synonyms = LoadSynonyms(path, synonymsConfigPath);
            var measurementPatterns = BuildMeasurementPatterns(synonyms);

            int autoStation = 1;
            foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine ?? string.Empty;
                if (!Regex.IsMatch(line, @"^\s*For\b", RegexOptions.IgnoreCase))
                    continue;

                var record = ParseLine(line, measurementPatterns, ref autoStation);
                yield return record;
            }
        }

        private static string ReadTextSmart(string path)
        {
            Exception? last = null;
            foreach (var encName in CandidateEncodings)
            {
                try
                {
                    var enc = Encoding.GetEncoding(encName);
                    return File.ReadAllText(path, enc);
                }
                catch (Exception ex) { last = ex; }
            }
            throw last ?? new IOException("Не удалось прочитать файл в известных кодировках.");
        }

        private static Dictionary<string, HashSet<string>> LoadSynonyms(string dataPath, string? synonymsConfigPath)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in DefaultSynonyms)
            {
                var set = new HashSet<string>(kvp.Value.Where(v => !string.IsNullOrWhiteSpace(v)), StringComparer.OrdinalIgnoreCase)
                {
                    kvp.Key
                };
                map[kvp.Key] = set;
            }

            var configPath = ResolveConfigPath(dataPath, synonymsConfigPath);
            if (configPath != null)
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
                    if (parsed != null)
                    {
                        foreach (var pair in parsed)
                        {
                            if (string.IsNullOrWhiteSpace(pair.Key))
                                continue;

                            var key = pair.Key.Trim();
                            var overrides = new HashSet<string>(pair.Value?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())
                                ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                            {
                                key
                            };
                            map[key] = overrides;
                        }
                    }
                }
                catch (Exception)
                {
                    // Неверный конфиг не должен ронять парсер — оставляем значения по умолчанию
                }
            }

            return map;
        }

        private static string? ResolveConfigPath(string dataPath, string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
                return explicitPath;

            var sameDirectory = Path.ChangeExtension(dataPath, ".json");
            if (File.Exists(sameDirectory))
                return sameDirectory;

            return null;
        }

        private static Dictionary<string, Regex> BuildMeasurementPatterns(Dictionary<string, HashSet<string>> synonyms)
        {
            var patterns = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in synonyms)
            {
                if (kvp.Value.Count == 0)
                    continue;

                var keywords = kvp.Value.Select(Regex.Escape);
                var pattern = $@"(?i)\b({string.Join("|", keywords)})\b[^0-9+-]*([+-]?\d+(?:[.,]\d+)?)";
                patterns[kvp.Key] = new Regex(pattern, RegexOptions.Compiled);
            }

            // Z остаётся фиксированным
            patterns["Z"] = new Regex(@"(?i)\bZ\b[^0-9+-]*([+-]?\d+(?:[.,]\d+)?)", RegexOptions.Compiled);

            return patterns;
        }

        private static MeasurementRecord ParseLine(string line, IReadOnlyDictionary<string, Regex> measurementPatterns, ref int autoStation)
        {
            var record = new MeasurementRecord();

            var adrMatch = Regex.Match(line, @"\bAdr\s+(\d+)\b", RegexOptions.IgnoreCase);
            if (adrMatch.Success && int.TryParse(adrMatch.Groups[1].Value, out var seq))
                record.Seq = seq;

            var measurementIndex = FindFirstMeasurementIndex(line, measurementPatterns.Values);
            var modeSegment = ExtractModeSegment(line, adrMatch, measurementIndex);
            var modeTokens = PopulateModeAndTarget(record, modeSegment);

            // Распознавание маркеров хода (Start-Line, End-Line, Cont-Line)
            DetectLineMarker(record, line);

            // Распознавание ошибочных измерений (помечены ##### или Measurement repeated)
            DetectInvalidMeasurement(record, line);

            var stationCode = ExtractStationCode(modeSegment, line);
            if (stationCode != null)
            {
                // Очистка маркера ##### из кода станции
                stationCode = stationCode.Replace("#####", "").Trim();
                record.StationCode = stationCode;
                if (int.TryParse(stationCode, NumberStyles.Integer, CI, out var parsed))
                    autoStation = parsed + 1;
            }
            else
            {
                record.StationCode = autoStation.ToString(CI);
                autoStation++;
            }

            if (!string.IsNullOrWhiteSpace(record.Target) && record.StationCode != null && modeTokens.Length > 1)
            {
                var stationToken = modeTokens.LastOrDefault(t => string.Equals(t, record.StationCode, StringComparison.OrdinalIgnoreCase));
                if (stationToken != null)
                {
                    var targetTokens = modeTokens.Skip(1).Where(t => !string.Equals(t, stationToken, StringComparison.OrdinalIgnoreCase)).ToArray();
                    record.Target = targetTokens.Length == 0 ? null : string.Join(" ", targetTokens);
                }
            }

            PopulateMeasurements(record, line, measurementPatterns);

            return record;
        }

        private static int? FindFirstMeasurementIndex(string line, IEnumerable<Regex> measurementRegexes)
        {
            int? result = null;
            foreach (var regex in measurementRegexes)
            {
                var match = regex.Match(line);
                if (!match.Success)
                    continue;

                if (result == null || match.Index < result)
                    result = match.Index;
            }
            return result;
        }

        private static string ExtractModeSegment(string line, Match adrMatch, int? measurementIndex)
        {
            var mFor1 = Regex.Match(line, @"\bFor\b", RegexOptions.IgnoreCase);
            int start = adrMatch.Success
                ? adrMatch.Index + adrMatch.Length
                : (mFor1.Success ? mFor1.Index + mFor1.Length : 0);

            int end = measurementIndex ?? line.Length;
            if (end < start) (start, end) = (end, start);

            var segment = line[start..end];
            if (string.IsNullOrWhiteSpace(segment) && adrMatch.Success)
            {
                end = adrMatch.Index;
                var mFor2 = Regex.Match(line, @"\bFor\b", RegexOptions.IgnoreCase);
                start = mFor2.Success ? mFor2.Index + mFor2.Length : 0;
                if (end > start) segment = line[start..end];
            }
            return segment;
        }


        private static string[] PopulateModeAndTarget(MeasurementRecord record, string segment)
        {
            var tokens = Regex.Split(segment ?? string.Empty, @"[|\t ]+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Replace("#####", "").Trim()) // Очистка ##### из токенов
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            if (tokens.Length == 0)
                return Array.Empty<string>();

            record.Mode = tokens[0];
            if (tokens.Length > 1)
                record.Target = string.Join(" ", tokens.Skip(1));

            return tokens;
        }

        private static string? ExtractStationCode(string modeSegment, string fullLine)
        {
            var prioritized = FindStationCodeCandidates(modeSegment, requireLabel: false);
            if (prioritized != null)
                return prioritized;

            return FindStationCodeCandidates(fullLine, requireLabel: true);
        }

        private static string? FindStationCodeCandidates(string text, bool requireLabel)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var stationMatches = Regex.Matches(text, @"(?i)\b([A-Z]{1,3}\d+)\b[^0-9+-]*([+-]?\d+)(?![\d.,])");
            if (stationMatches.Count > 0)
                return stationMatches[^1].Groups[2].Value;

            if (requireLabel)
                return null;

            var numberMatches = Regex.Matches(text, @"(?<!\d)([+-]?\d+)(?![\d.,])");
            if (numberMatches.Count > 0)
                return numberMatches[^1].Groups[1].Value;

            return null;
        }

        /// <summary>
        /// Определяет маркеры хода (Start-Line, End-Line, Cont-Line) из строки
        /// </summary>
        private static void DetectLineMarker(MeasurementRecord record, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Проверка на наличие маркеров хода в строке
            if (Regex.IsMatch(line, @"\bStart-Line\b", RegexOptions.IgnoreCase))
            {
                record.LineMarker = "Start-Line";
            }
            else if (Regex.IsMatch(line, @"\bEnd-Line\b", RegexOptions.IgnoreCase))
            {
                record.LineMarker = "End-Line";
            }
            else if (Regex.IsMatch(line, @"\bCont-Line\b", RegexOptions.IgnoreCase))
            {
                record.LineMarker = "Cont-Line";
            }
            else if (Regex.IsMatch(line, @"\bMeasurement\s+repeated\b", RegexOptions.IgnoreCase))
            {
                record.LineMarker = "Measurement-Repeated";
            }
        }

        /// <summary>
        /// Определяет ошибочные измерения (помечены ##### в коде станции)
        /// Такие измерения были повторены и должны игнорироваться при расчётах
        /// </summary>
        private static void DetectInvalidMeasurement(MeasurementRecord record, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Проверка на наличие ##### в строке (маркер ошибочного измерения)
            if (line.Contains("#####"))
            {
                record.IsInvalidMeasurement = true;
            }
        }

        private static void PopulateMeasurements(MeasurementRecord record, string line, IReadOnlyDictionary<string, Regex> patterns)
        {
            if (patterns.TryGetValue("Rb", out var rbRegex))
                record.Rb_m ??= TryMatchMeasurement(rbRegex, line);
            if (patterns.TryGetValue("Rf", out var rfRegex))
                record.Rf_m ??= TryMatchMeasurement(rfRegex, line);
            if (patterns.TryGetValue("HDback", out var hdBackRegex))
                record.HdBack_m ??= TryMatchMeasurement(hdBackRegex, line);
            if (patterns.TryGetValue("HDfore", out var hdForeRegex))
                record.HdFore_m ??= TryMatchMeasurement(hdForeRegex, line);
            if (patterns.TryGetValue("Z", out var zRegex))
                record.Z_m ??= TryMatchMeasurement(zRegex, line);
        }

        private static double? TryMatchMeasurement(Regex regex, string line)
        {
            var match = regex.Match(line);
            if (!match.Success)
                return null;

            var value = match.Groups[^1].Value.Replace(',', '.');
            return double.TryParse(value, NumberStyles.Float, CI, out var parsed) ? parsed : null;
        }
    }
}