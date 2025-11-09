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

        private static readonly Regex ForRegex = new(@"(?i)^For\s+(?<mode>\S+)(?:\s+(?<target>\S+))?");
        private static readonly Regex AdrRegex = new(@"(?i)\bAdr\b[^0-9]*?(?<seq>\d+)");
        private static readonly Regex StationCodeRegex = new(@"(?i)\bKD\w*\b");
        private static readonly Regex DigitsRegex = new(@"\d+");

        public IEnumerable<MeasurementRecord> Parse(string path)
        {
            var text = ReadTextSmart(path);
            var synonymMap = BuildSynonymMap(path);
            var measurementRegexes = BuildMeasurementRegexes(synonymMap);

            var autoStation = 1;
            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.TrimStart().StartsWith("For", StringComparison.OrdinalIgnoreCase))
                    continue;

                var record = ParseLine(line, measurementRegexes, ref autoStation);
                if (record != null)
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
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            throw last ?? new IOException("Не удалось прочитать файл в известных кодировках.");
        }

        private static MeasurementRecord? ParseLine(string line, IDictionary<string, Regex> measurementRegexes, ref int autoStation)
        {
            var record = new MeasurementRecord();

            foreach (var rawSegment in SplitSegments(line))
            {
                var segment = rawSegment.Trim();
                if (segment.Length == 0)
                    continue;

                if (record.Mode == null)
                {
                    var m = ForRegex.Match(segment);
                    if (m.Success)
                    {
                        record.Mode = m.Groups["mode"].Value.Trim();
                        var targetFromFor = m.Groups["target"].Value.Trim();
                        if (!string.IsNullOrEmpty(targetFromFor) && !targetFromFor.Equals("Adr", StringComparison.OrdinalIgnoreCase))
                            record.Target = targetFromFor;
                        continue;
                    }
                }

                if (!record.Seq.HasValue)
                {
                    var mAdr = AdrRegex.Match(segment);
                    if (mAdr.Success && int.TryParse(mAdr.Groups["seq"].Value, out var seq))
                    {
                        record.Seq = seq;
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(record.StationCode) || string.IsNullOrEmpty(record.Target))
                    TryPopulateStation(segment, record, measurementRegexes);
            }

            if (string.IsNullOrEmpty(record.StationCode))
                TryPopulateStation(line, record, measurementRegexes);

            if (string.IsNullOrEmpty(record.StationCode))
            {
                record.StationCode = autoStation.ToString(CultureInfo.InvariantCulture);
                autoStation++;
            }
            else if (int.TryParse(record.StationCode, out var stationNumber) && stationNumber >= autoStation)
            {
                autoStation = stationNumber + 1;
            }

            record.Rb_m = ExtractMeasurement(line, "Rb", measurementRegexes);
            record.Rf_m = ExtractMeasurement(line, "Rf", measurementRegexes);
            record.HdBack_m = ExtractMeasurement(line, "HDback", measurementRegexes);
            record.HdFore_m = ExtractMeasurement(line, "HDfore", measurementRegexes);
            record.Z_m = ExtractMeasurement(line, "Z", measurementRegexes);

            record.HD_m = null;
            if (record.Rb_m.HasValue && record.HdBack_m.HasValue)
                record.HD_m = record.HdBack_m;
            else if (record.Rf_m.HasValue && record.HdFore_m.HasValue)
                record.HD_m = record.HdFore_m;
            else if (record.HdBack_m.HasValue)
                record.HD_m = record.HdBack_m;
            else if (record.HdFore_m.HasValue)
                record.HD_m = record.HdFore_m;

            return record;
        }

        private static IEnumerable<string> SplitSegments(string line)
        {
            if (line.IndexOf('|') >= 0)
                return line.Split('|');

            return new[] { line };
        }

        private static void TryPopulateStation(string text, MeasurementRecord record, IDictionary<string, Regex> measurementRegexes)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!string.IsNullOrEmpty(record.StationCode) && !string.IsNullOrEmpty(record.Target))
                return;

            var codeMatch = StationCodeRegex.Match(text);
            if (!codeMatch.Success)
                return;

            var searchStart = codeMatch.Index + codeMatch.Length;
            var searchEnd = text.Length;

            foreach (var regex in measurementRegexes.Values)
            {
                var measurementMatch = regex.Match(text, searchStart);
                if (measurementMatch.Success)
                    searchEnd = Math.Min(searchEnd, measurementMatch.Index);
            }

            if (searchEnd < searchStart)
                searchEnd = searchStart;

            var length = searchEnd - searchStart;
            if (length <= 0)
                return;

            var window = text.Substring(searchStart, length);
            var numbers = DigitsRegex.Matches(window);
            if (numbers.Count == 0)
                return;

            record.Target ??= codeMatch.Value.Trim();
            record.StationCode ??= numbers[numbers.Count - 1].Value.Trim();
        }

        private static double? ExtractMeasurement(string line, string key, IDictionary<string, Regex> measurementRegexes)
        {
            if (!measurementRegexes.TryGetValue(key, out var regex))
                return null;

            var match = regex.Match(line);
            if (!match.Success)
                return null;

            var raw = match.Groups["value"].Value;
            return TryParseInvariant(raw, out var value) ? value : null;
        }

        private static bool TryParseInvariant(string text, out double value)
        {
            var normalized = text.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign, CI, out value);
        }

        private static Dictionary<string, Regex> BuildMeasurementRegexes(Dictionary<string, HashSet<string>> synonymMap)
        {
            var result = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { "Rb", "Rf", "HDback", "HDfore", "Z" })
            {
                if (!synonymMap.TryGetValue(key, out var aliases) || aliases.Count == 0)
                    aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key };

                var pattern = $@"(?i)\b({string.Join("|", aliases.Select(Regex.Escape))})\b[^0-9+-]*?(?<value>[+-]?\d+(?:[.,]\d+)?)";
                result[key] = new Regex(pattern, RegexOptions.Compiled);
            }

            return result;
        }

        private static Dictionary<string, HashSet<string>> BuildSynonymMap(string path)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rb"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Rb", "RB", "RBack", "Back", "B" },
                ["Rf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Rf", "RF", "RFore", "Fore", "F" },
                ["HDback"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "HDback", "HDBack", "HD_B", "HB" },
                ["HDfore"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "HDfore", "HDFore", "HD_F", "HF" },
                ["Z"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Z" }
            };

            var configPath = Path.ChangeExtension(path, ".json");
            if (File.Exists(configPath))
            {
                try
                {
                    using var stream = File.OpenRead(configPath);
                    var config = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream);
                    if (config != null)
                    {
                        foreach (var kvp in config)
                        {
                            if (!map.TryGetValue(kvp.Key, out var set))
                            {
                                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kvp.Key };
                                map[kvp.Key] = set;
                            }

                            foreach (var alias in kvp.Value ?? Array.Empty<string>())
                            {
                                if (!string.IsNullOrWhiteSpace(alias))
                                    set.Add(alias.Trim());
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    throw new InvalidDataException($"Не удалось загрузить конфигурацию синонимов из '{configPath}'.", ex);
                }
            }

            return map;
        }
    }
}
