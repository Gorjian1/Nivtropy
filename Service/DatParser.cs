using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    public class DatParser
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly string[] CandidateEncodings = { "utf-8", "windows-1251", "cp1251", "latin1", "utf-16" };

        private static readonly (string Key, Regex Pattern)[] MeasurementPatterns =
        {
            ("Rb", new Regex(@"(?<=\b(Rb|Back|B)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Rf", new Regex(@"(?<=\b(Rf|Fore|F)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("HDback", new Regex(@"(?<=\b(HDback|HB)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("HDfore", new Regex(@"(?<=\b(HDfore|HF)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("HD", new Regex(@"(?<=\bHD\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Z", new Regex(@"(?<=\bZ\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
        };

        public IEnumerable<MeasurementRecord> Parse(string path)
        {
            var text = ReadTextSmart(path);
            foreach (var record in SplitIntoRecords(text))
            {
                foreach (var measurement in ParseRecord(record))
                {
                    yield return measurement;
                }
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

        private static IEnumerable<IReadOnlyList<string>> SplitIntoRecords(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            List<string>? current = null;

            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                var trimmed = rawLine.TrimStart();
                if (trimmed.StartsWith("KD", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null && current.Count > 0)
                    {
                        yield return current;
                    }
                    current = new List<string>();
                }

                if (current == null)
                {
                    continue;
                }

                current.Add(rawLine);
            }

            if (current != null && current.Count > 0)
            {
                yield return current;
            }
        }

        private static IEnumerable<MeasurementRecord> ParseRecord(IReadOnlyList<string> recordLines)
        {
            if (recordLines.Count == 0) yield break;

            var header = recordLines[0];
            var stationCode = ExtractStationCode(header);

            var results = new List<MeasurementRecord>();
            var pendingBackRecords = new List<MeasurementRecord>();
            var pendingForeRecords = new List<MeasurementRecord>();
            var pendingGenericHd = new Queue<double>();
            var pendingHdBackValues = new Queue<double>();
            var pendingHdForeValues = new Queue<double>();

            foreach (var line in recordLines.Skip(1))
            {
                var segments = line.Split('|');
                var seq = ExtractSequence(segments);
                var (mode, target) = ExtractModeAndTarget(segments);

                foreach (var token in ExtractMeasurements(segments))
                {
                    switch (token.Key)
                    {
                        case "Rb":
                        {
                            var rec = CreateMeasurementRecord(seq, mode, target, stationCode);
                            rec.Rb_m = token.Value;
                            if (pendingHdBackValues.TryDequeue(out var hdBack))
                            {
                                rec.HD_m = hdBack;
                            }
                            else if (pendingGenericHd.TryDequeue(out var hdGeneric))
                            {
                                rec.HD_m = hdGeneric;
                            }

                            if (!rec.HD_m.HasValue)
                            {
                                pendingBackRecords.Add(rec);
                            }

                            results.Add(rec);
                            break;
                        }

                        case "Rf":
                        {
                            var rec = CreateMeasurementRecord(seq, mode, target, stationCode);
                            rec.Rf_m = token.Value;
                            if (pendingHdForeValues.TryDequeue(out var hdFore))
                            {
                                rec.HD_m = hdFore;
                            }
                            else if (pendingGenericHd.TryDequeue(out var hdGeneric))
                            {
                                rec.HD_m = hdGeneric;
                            }

                            if (!rec.HD_m.HasValue)
                            {
                                pendingForeRecords.Add(rec);
                            }

                            results.Add(rec);
                            break;
                        }

                        case "HDback":
                        {
                            if (TryAssignHd(pendingBackRecords, token.Value))
                            {
                                break;
                            }
                            pendingHdBackValues.Enqueue(token.Value);
                            break;
                        }

                        case "HDfore":
                        {
                            if (TryAssignHd(pendingForeRecords, token.Value))
                            {
                                break;
                            }
                            pendingHdForeValues.Enqueue(token.Value);
                            break;
                        }

                        case "HD":
                        {
                            if (TryAssignHd(pendingBackRecords, token.Value))
                            {
                                break;
                            }

                            if (TryAssignHd(pendingForeRecords, token.Value))
                            {
                                break;
                            }

                            pendingGenericHd.Enqueue(token.Value);
                            break;
                        }

                        case "Z":
                        {
                            var rec = CreateMeasurementRecord(seq, mode, target, stationCode);
                            rec.Z_m = token.Value;
                            results.Add(rec);
                            break;
                        }
                    }
                }
            }

            WarnAboutUnpairedMeasurements(stationCode, results);

            foreach (var item in results)
            {
                yield return item;
            }
        }

        private static void WarnAboutUnpairedMeasurements(string? stationCode, List<MeasurementRecord> results)
        {
            var backCount = results.Count(r => r.Rb_m.HasValue);
            var foreCount = results.Count(r => r.Rf_m.HasValue);

            if (backCount > foreCount)
            {
                Trace.TraceWarning($"Station {stationCode ?? "?"}: {backCount - foreCount} задних визиров без пары.");
            }
            else if (foreCount > backCount)
            {
                Trace.TraceWarning($"Station {stationCode ?? "?"}: {foreCount - backCount} передних визиров без пары.");
            }
        }

        private static bool TryAssignHd(List<MeasurementRecord> pendingRecords, double value)
        {
            for (int i = 0; i < pendingRecords.Count; i++)
            {
                if (!pendingRecords[i].HD_m.HasValue)
                {
                    pendingRecords[i].HD_m = value;
                    pendingRecords.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<(string Key, double Value)> ExtractMeasurements(IEnumerable<string> segments)
        {
            foreach (var segment in segments)
            {
                if (segment == null) continue;
                var matches = new List<(int Index, string Key, double Value)>();

                foreach (var (key, pattern) in MeasurementPatterns)
                {
                    foreach (Match match in pattern.Matches(segment))
                    {
                        if (!match.Success) continue;
                        if (!TryParse(match.Value, out var value)) continue;
                        matches.Add((match.Index, key, value));
                    }
                }

                matches.Sort((a, b) => a.Index.CompareTo(b.Index));
                foreach (var match in matches)
                {
                    yield return (match.Key, match.Value);
                }
            }
        }

        private static MeasurementRecord CreateMeasurementRecord(int? seq, string? mode, string? target, string? stationCode)
        {
            return new MeasurementRecord
            {
                Seq = seq,
                Mode = mode,
                Target = target,
                StationCode = stationCode
            };
        }

        private static string? ExtractStationCode(string headerLine)
        {
            var segments = headerLine.Split('|');
            string? station = null;

            if (segments.Length > 1)
            {
                station = ExtractLastInteger(segments[1]);
            }

            station ??= ExtractLastInteger(headerLine);
            return station;
        }

        private static string? ExtractLastInteger(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            string? last = null;
            foreach (Match match in Regex.Matches(input, @"(\d+)", RegexOptions.Compiled))
            {
                last = match.Groups[1].Value;
            }

            return last;
        }

        private static int? ExtractSequence(IEnumerable<string> segments)
        {
            foreach (var segment in segments)
            {
                if (segment == null) continue;
                var match = Regex.Match(segment, @"Adr\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CI, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static (string? Mode, string? Target) ExtractModeAndTarget(IEnumerable<string> segments)
        {
            foreach (var segment in segments)
            {
                var trimmed = (segment ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (!trimmed.Any(char.IsLetter)) continue;

                if (Regex.IsMatch(trimmed, @"^(Adr|Rb|Rf|HD|HDback|HDfore|HB|HF|Z)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled))
                {
                    continue;
                }

                var tokens = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 2)
                {
                    return (tokens[0], tokens[1]);
                }
            }

            return (null, null);
        }

        private static bool TryParse(string input, out double value)
        {
            var normalized = input.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CI, out value);
        }
    }
}
