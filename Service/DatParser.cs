using System;
using System.Collections.Generic;
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

        public IEnumerable<MeasurementRecord> Parse(string path)
        {
            var text = ReadTextSmart(path);
            foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (!line.TrimStart().StartsWith("For ")) continue;
                var rec = ParseLine(line);
                if (rec != null) yield return rec;
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

        private static MeasurementRecord? ParseLine(string line)
        {
            // Пример сегментов: "For M5|Adr     4|KD1       12|            10214|Rb 1.1700 m   |HD 6.97 m |Z 99.9114 m"
            var segs = line.Split('|');
            if (segs.Length == 0) return null;

            var rec = new MeasurementRecord();

            // Adr
            var mAdr = Regex.Match(segs.ElementAtOrDefault(1) ?? string.Empty, @"Adr\s+(\d+)");
            if (mAdr.Success && int.TryParse(mAdr.Groups[1].Value, out var seq)) rec.Seq = seq;

            // Mode + Target (из seg2)
            var mModeTarget = Regex.Match((segs.ElementAtOrDefault(2) ?? string.Empty).Trim(), @"(?<mode>\S+)\s+(?<target>.+)");
            if (mModeTarget.Success)
            {
                rec.Mode = mModeTarget.Groups["mode"].Value.Trim();
                rec.Target = mModeTarget.Groups["target"].Value.Trim();
            }

            // Station code (цифры в seg3)
            var mStation = Regex.Match(segs.ElementAtOrDefault(3) ?? string.Empty, @"(\d+)");
            if (mStation.Success) rec.StationCode = mStation.Groups[1].Value;

            // Measurements (segs4..end)
            for (int i = 4; i < segs.Length; i++)
            {
                ExtractMeasurements(rec, segs[i]);
            }

            return rec.IsValid ? rec : rec; // даже пустые колонки нам полезны как строки данных
        }

        private static void ExtractMeasurements(MeasurementRecord rec, string segment)
        {
            // Ищем Rb, Rf, HD, Z в произвольном порядке, число — с точкой
            double? TryNum(string pattern)
            {
                var m = Regex.Match(segment, pattern, RegexOptions.IgnoreCase);
                if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CI, out var v)) return v;
                return null;
            }

            rec.Rb_m ??= TryNum(@"\bRb\s*([0-9]+(?:\.[0-9]+)?)");
            rec.Rf_m ??= TryNum(@"\bRf\s*([0-9]+(?:\.[0-9]+)?)");
            rec.HD_m ??= TryNum(@"\bHD\s*([0-9]+(?:\.[0-9]+)?)");
            rec.Z_m ??= TryNum(@"\bZ\s*([0-9]+(?:\.[0-9]+)?)");
        }
    }
}