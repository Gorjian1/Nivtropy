
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

        private static readonly Regex AdrRegex = new(@"Adr\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ModeTargetRegex = new(@"^For\s+(?<mode>\S+)\s+(?<target>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TrailingIntegerRegex = new(@"(\d+)(?!.*\d)", RegexOptions.Compiled);

        private enum TokenType
        {
            Rb,
            Rf,
            HdBack,
            HdFore,
            Hd,
            Z
        }

        private static readonly IReadOnlyDictionary<TokenType, Regex> TokenPatterns = new Dictionary<TokenType, Regex>
        {
            [TokenType.Rb] = new(@"(?<=\b(Rb|Back|B)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            [TokenType.Rf] = new(@"(?<=\b(Rf|Fore|F)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            [TokenType.HdBack] = new(@"(?<=\b(HDback|HB)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            [TokenType.HdFore] = new(@"(?<=\b(HDfore|HF)\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            [TokenType.Hd] = new(@"(?<=\bHD\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            [TokenType.Z] = new(@"(?<=\bZ\s*[:=]?\s*)([-+]?\d+[.,]?\d*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        public IEnumerable<MeasurementRecord> Parse(string path)
        {
            var text = ReadTextSmart(path);
            using var reader = new StringReader(text);
            string? line;
            RecordBuilder? current = null;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (IsStationHeader(line))
                {
                    if (current != null)
                    {
                        foreach (var record in current.BuildRecords())
                        {
                            yield return record;
                        }
                    }

                    current = RecordBuilder.FromHeader(line);
                    continue;
                }

                current?.AddLine(line);
            }

            if (current != null)
            {
                foreach (var record in current.BuildRecords())
                {
                    yield return record;
                }
            }
        }

        private static bool IsStationHeader(string line)
        {
            return line.TrimStart().StartsWith("KD", StringComparison.OrdinalIgnoreCase);
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

        private sealed class RecordBuilder
        {
            private readonly List<MeasurementToken> _tokens = new();
            private readonly List<double> _zValues = new();

            public int? Seq { get; private set; }
            public string? Mode { get; private set; }
            public string? Target { get; private set; }
            public string? StationCode { get; private set; }

            private RecordBuilder()
            {
            }

            public static RecordBuilder FromHeader(string headerLine)
            {
                var builder = new RecordBuilder();
                var segments = SplitSegments(headerLine).ToList();
                builder.StationCode = ExtractStationCode(segments, headerLine);
                builder.AddSegments(segments);
                return builder;
            }

            public void AddLine(string line)
            {
                var segments = SplitSegments(line);
                AddSegments(segments);
            }

            private void AddSegments(IEnumerable<string> segments)
            {
                foreach (var segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment)) continue;

                    UpdateMeta(segment);
                    CaptureTokens(segment);
                }
            }

            private void UpdateMeta(string segment)
            {
                if (!Seq.HasValue)
                {
                    var mAdr = AdrRegex.Match(segment);
                    if (mAdr.Success && int.TryParse(mAdr.Groups[1].Value, out var seq))
                    {
                        Seq = seq;
                    }
                }

                if (Mode == null || Target == null)
                {
                    var mModeTarget = ModeTargetRegex.Match(segment.Trim());
                    if (mModeTarget.Success)
                    {
                        Mode ??= mModeTarget.Groups["mode"].Value.Trim();
                        Target ??= mModeTarget.Groups["target"].Value.Trim();
                    }
                }

            }

            private void CaptureTokens(string segment)
            {
                var matches = new List<(int Index, TokenType Type, double Value)>();

                foreach (var pair in TokenPatterns)
                {
                    foreach (Match match in pair.Value.Matches(segment))
                    {
                        if (!match.Success) continue;
                        var numberGroup = match.Groups.Count > 2 ? match.Groups[2] : match.Groups[1];
                        if (!numberGroup.Success) continue;
                        if (TryParseDouble(numberGroup.Value, out var value))
                        {
                            matches.Add((match.Index, pair.Key, value));
                        }
                    }
                }

                if (matches.Count == 0) return;

                foreach (var token in matches.OrderBy(m => m.Index))
                {
                    if (token.Type == TokenType.Z)
                    {
                        _zValues.Add(token.Value);
                    }
                    else
                    {
                        _tokens.Add(new MeasurementToken(token.Type, token.Value));
                    }
                }
            }

            public IEnumerable<MeasurementRecord> BuildRecords()
            {
                if (_tokens.Count == 0) yield break;

                var hdBack = new Queue<double>();
                var hdFore = new Queue<double>();
                var hdGeneral = new Queue<double>();
                var zQueue = new Queue<double>(_zValues);

                int rbCount = 0;
                int rfCount = 0;

                foreach (var token in _tokens)
                {
                    switch (token.Type)
                    {
                        case TokenType.HdBack:
                            hdBack.Enqueue(token.Value);
                            break;
                        case TokenType.HdFore:
                            hdFore.Enqueue(token.Value);
                            break;
                        case TokenType.Hd:
                            hdGeneral.Enqueue(token.Value);
                            break;
                        case TokenType.Rb:
                            rbCount++;
                            yield return CreateRecord(token.Value, hdBack, hdFore, hdGeneral, zQueue, isBack: true);
                            break;
                        case TokenType.Rf:
                            rfCount++;
                            yield return CreateRecord(token.Value, hdBack, hdFore, hdGeneral, zQueue, isBack: false);
                            break;
                    }
                }

                if (rbCount != rfCount)
                {
                    var unmatched = Math.Abs(rbCount - rfCount);
                    var kind = rbCount > rfCount ? "Rb" : "Rf";
                    Debug.WriteLine($"DAT parser warning: Station {StationCode ?? "?"} has {unmatched} unmatched {kind} values.");
                }
            }

            private MeasurementRecord CreateRecord(double value, Queue<double> hdBack, Queue<double> hdFore, Queue<double> hdGeneral, Queue<double> zQueue, bool isBack)
            {
                var record = new MeasurementRecord
                {
                    Seq = Seq,
                    Mode = Mode,
                    Target = Target,
                    StationCode = StationCode
                };

                if (isBack)
                {
                    record.Rb_m = value;
                    record.HD_m = Dequeue(hdBack) ?? Dequeue(hdGeneral);
                }
                else
                {
                    record.Rf_m = value;
                    record.HD_m = Dequeue(hdFore) ?? Dequeue(hdGeneral);
                }

                record.Z_m = Dequeue(zQueue);

                return record;
            }

            private static double? Dequeue(Queue<double> queue)
            {
                return queue.Count > 0 ? queue.Dequeue() : null;
            }
        }

        private readonly record struct MeasurementToken(TokenType Type, double Value);

        private static IEnumerable<string> SplitSegments(string line)
        {
            return line.Split('|').Select(seg => seg.Trim()).Where(seg => !string.IsNullOrWhiteSpace(seg));
        }

        private static string? ExtractStationCode(IReadOnlyList<string> segments, string rawLine)
        {
            if (segments.Count > 1)
            {
                var match = TrailingIntegerRegex.Match(segments[1]);
                if (match.Success)
                {
                    return match.Value;
                }
            }

            var fallback = TrailingIntegerRegex.Match(rawLine);
            return fallback.Success ? fallback.Value : null;
        }

        private static bool TryParseDouble(string raw, out double value)
        {
            var normalized = raw.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CI, out value);
        }
    }
}
