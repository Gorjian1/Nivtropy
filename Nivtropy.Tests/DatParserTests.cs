using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nivtropy.Services;
using Xunit;

namespace Nivtropy.Tests
{
    public class DatParserTests : IDisposable
    {
        private readonly List<string> _files = new();

        [Fact]
        public void ParsesTokensRegardlessOfSegmentOrder()
        {
            var content = string.Join(Environment.NewLine, new[]
            {
                "KD01|Station 100|HDfore 6.2|Rf 1.3|HDback 6.1|Rb 1.2"
            });

            var records = Parse(content).ToList();

            Assert.Equal(2, records.Count);

            var rb = Assert.Single(records.Where(r => r.Rb_m.HasValue));
            Assert.Equal(1.2, rb.Rb_m!.Value, 6);
            Assert.Equal(6.1, rb.HD_m!.Value, 6);

            var rf = Assert.Single(records.Where(r => r.Rf_m.HasValue));
            Assert.Equal(1.3, rf.Rf_m!.Value, 6);
            Assert.Equal(6.2, rf.HD_m!.Value, 6);
        }

        [Fact]
        public void AllowsMissingHdValues()
        {
            var content = "KD01|Station 100|Rb 1.0";

            var records = Parse(content).ToList();

            var record = Assert.Single(records);
            Assert.Equal(1.0, record.Rb_m!.Value, 6);
            Assert.Null(record.HD_m);
        }

        [Fact]
        public void ParsesCommaDecimals()
        {
            var content = "KD01|Station 100|Rb 1,25|Rf 1,35";

            var records = Parse(content).ToList();

            Assert.Equal(2, records.Count);
            Assert.Contains(records, r => r.Rb_m.HasValue && Math.Abs(r.Rb_m.Value - 1.25) < 1e-6);
            Assert.Contains(records, r => r.Rf_m.HasValue && Math.Abs(r.Rf_m.Value - 1.35) < 1e-6);
        }

        [Fact]
        public void HandlesMultipleStations()
        {
            var content = string.Join(Environment.NewLine, new[]
            {
                "KD01|Station 100|Rb 1.0|Rf 2.0",
                "KD02|Station 200|Rb 3.0|Rf 4.0"
            });

            var records = Parse(content).ToList();

            Assert.Equal(4, records.Count);
            Assert.Contains(records, r => r.Rb_m.HasValue && r.StationCode == "100");
            Assert.Contains(records, r => r.Rf_m.HasValue && r.StationCode == "200");
        }

        private IEnumerable<Nivtropy.Models.MeasurementRecord> Parse(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dat");
            File.WriteAllText(path, content, Encoding.UTF8);
            _files.Add(path);

            var parser = new DatParser();
            return parser.Parse(path);
        }

        public void Dispose()
        {
            foreach (var file in _files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }
}
