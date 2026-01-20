using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nivtropy.Domain.Model;
using Nivtropy.Infrastructure.Logging;

namespace Nivtropy.Infrastructure.Parsers
{
    /// <summary>
    /// Парсер для формата "For M5" (старый формат нивелира)
    /// </summary>
    public class ForFormatParser : IFormatParser
    {
        private readonly ILoggerService? _logger;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        // Скомпилированные regex для маркеров линий
        private static readonly Regex StartLineRegex = new(@"\bStart-Line\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EndLineRegex = new(@"\bEnd-Line\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ContLineRegex = new(@"\bCont-Line\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MeasurementRepeatedRegex = new(@"\bMeasurement\s+repeated\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RunNumberRegex = new(@"\b(?:BF|FB)\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Скомпилированные regex для парсинга
        private static readonly Regex ForFormatLineRegex = new(@"^\s*For\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AdrRegex = new(@"\bAdr\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ForKeywordRegex = new(@"\bFor\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TokenSplitRegex = new(@"[|\t ]+", RegexOptions.Compiled);
        private static readonly Regex StationCodeRegex = new(@"(?i)\b([A-Z]{1,3}\d+)\b[^0-9+-]*([+-]?\d+(?:[.,]\d+)?)", RegexOptions.Compiled);
        private static readonly Regex NumberMatchRegex = new(@"(?<![\d.,])([+-]?\d+(?:[.,]\d+)?)(?![\d.,])", RegexOptions.Compiled);

        private static readonly Dictionary<string, string[]> DefaultSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Rb"] = new[] { "RB", "RBack", "Back", "B" },
            ["Rf"] = new[] { "RF", "RFore", "Fore", "F" },
            ["HD"] = new[] { "HD", "HorizontalDistance", "Dist" },
            ["HDback"] = new[] { "HDBack", "HD_B", "HB" },
            ["HDfore"] = new[] { "HDFore", "HD_F", "HF" },
        };

        public ForFormatParser() : this(null) { }

        public ForFormatParser(ILoggerService? logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public int GetFormatScore(string[] sampleLines)
        {
            var nonEmpty = sampleLines.Take(50).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (nonEmpty.Length == 0) return 0;

            int forFormatCount = nonEmpty.Count(line => ForFormatLineRegex.IsMatch(line.TrimStart()));
            return (int)(100.0 * forFormatCount / nonEmpty.Length);
        }

        /// <inheritdoc/>
        public IEnumerable<MeasurementRecord> Parse(string[] lines, string? filePath = null, string? synonymsConfigPath = null)
        {
            var synonyms = LoadSynonyms(filePath, synonymsConfigPath);
            var measurementPatterns = BuildMeasurementPatterns(synonyms);

            int autoStation = 1;
            foreach (var rawLine in lines)
            {
                var line = rawLine ?? string.Empty;
                if (!ForFormatLineRegex.IsMatch(line))
                    continue;

                var record = ParseLine(line, measurementPatterns, ref autoStation);

                // Пропускаем строки с ##### (ошибочные измерения, которые будут заменены)
                if (record.IsInvalidMeasurement)
                    continue;

                // Пропускаем строки с маркерами "Measurement repeated" (не содержат данных)
                if (record.LineMarker == "Measurement-Repeated")
                    continue;

                // Пропускаем строки, которые не содержат никаких измерений
                // НО сохраняем маркеры линий (Start-Line, End-Line, Cont-Line) - они нужны для группировки
                var hasMeasurements = record.Rb_m.HasValue || record.Rf_m.HasValue || record.HdBack_m.HasValue || record.HdFore_m.HasValue || record.Z_m.HasValue;
                var isLineMarker = record.LineMarker == "Start-Line" || record.LineMarker == "End-Line" || record.LineMarker == "Cont-Line";
                if (!hasMeasurements && !isLineMarker)
                    continue;

                yield return record;
            }
        }

        private Dictionary<string, HashSet<string>> LoadSynonyms(string? dataPath, string? synonymsConfigPath)
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
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Не удалось загрузить конфигурацию синонимов из {configPath}: {ex.Message}");
                }
            }

            return map;
        }

        private static string? ResolveConfigPath(string? dataPath, string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
                return explicitPath;

            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                var sameDirectory = Path.ChangeExtension(dataPath, ".json");
                if (File.Exists(sameDirectory))
                    return sameDirectory;
            }

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

            var adrMatch = AdrRegex.Match(line);
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
            var mFor1 = ForKeywordRegex.Match(line);
            int start = adrMatch.Success
                ? adrMatch.Index + adrMatch.Length
                : (mFor1.Success ? mFor1.Index + mFor1.Length : 0);

            int end = measurementIndex ?? line.Length;
            if (end < start) (start, end) = (end, start);

            var segment = line[start..end];
            if (string.IsNullOrWhiteSpace(segment) && adrMatch.Success)
            {
                end = adrMatch.Index;
                var mFor2 = ForKeywordRegex.Match(line);
                start = mFor2.Success ? mFor2.Index + mFor2.Length : 0;
                if (end > start) segment = line[start..end];
            }
            return segment;
        }

        private static string[] PopulateModeAndTarget(MeasurementRecord record, string segment)
        {
            var tokens = TokenSplitRegex.Split(segment ?? string.Empty)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Replace("#####", "").Trim())
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

            var stationMatches = StationCodeRegex.Matches(text);
            if (stationMatches.Count > 0)
                return NormalizeStationCode(stationMatches[^1].Groups[2].Value);

            if (requireLabel)
                return null;

            var numberMatches = NumberMatchRegex.Matches(text);
            if (numberMatches.Count > 0)
                return NormalizeStationCode(numberMatches[^1].Groups[1].Value);

            return null;
        }

        private static string NormalizeStationCode(string raw)
        {
            return raw.Replace(',', '.').Trim();
        }

        private static void DetectLineMarker(MeasurementRecord record, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (StartLineRegex.IsMatch(line))
            {
                record.LineMarker = "Start-Line";
                var runMatch = RunNumberRegex.Match(line);
                if (runMatch.Success)
                {
                    record.OriginalLineNumber = runMatch.Groups[1].Value;
                }
            }
            else if (EndLineRegex.IsMatch(line))
            {
                record.LineMarker = "End-Line";
            }
            else if (ContLineRegex.IsMatch(line))
            {
                record.LineMarker = "Cont-Line";
            }
            else if (MeasurementRepeatedRegex.IsMatch(line))
            {
                record.LineMarker = "Measurement-Repeated";
            }
        }

        private static void DetectInvalidMeasurement(MeasurementRecord record, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

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

            if (patterns.TryGetValue("HD", out var hdRegex))
            {
                var hdValue = TryMatchMeasurement(hdRegex, line);
                if (hdValue.HasValue)
                {
                    if (record.Rb_m.HasValue && !record.HdBack_m.HasValue)
                        record.HdBack_m = hdValue;
                    else if (record.Rf_m.HasValue && !record.HdFore_m.HasValue)
                        record.HdFore_m = hdValue;
                }
            }

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
