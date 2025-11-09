using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Nivtropy.Services;
using Xunit;

namespace Nivtropy.Tests
{
    public class DatParserTests
    {
        [Fact]
        public void Parse_HandlesSwappedSegments()
        {
            var content = string.Join("\r\n", new[]
            {
                "KD1|Station number 10214",
                "For ModeA|Rb 1.50 m|HDback 12.30 m|Adr 7|Rf 1.55 m|HDfore 11.10 m"
            });

            var records = Parse(content);

            Assert.Collection(records,
                rb =>
                {
                    Assert.Equal(7, rb.Seq);
                    Assert.Equal("10214", rb.StationCode);
                    Assert.Equal(1.5, rb.Rb_m);
                    Assert.Equal(12.30, rb.HD_m);
                },
                rf =>
                {
                    Assert.Equal(7, rf.Seq);
                    Assert.Equal("10214", rf.StationCode);
                    Assert.Equal(1.55, rf.Rf_m);
                    Assert.Equal(11.10, rf.HD_m);
                });
        }

        [Fact]
        public void Parse_AllowsMissingHdValues()
        {
            var content = string.Join("\r\n", new[]
            {
                "KD2|Info 900",
                "For ModeB|Adr 8|Rb 2.10 m|Rf 2.40 m"
            });

            var records = Parse(content);

            Assert.Equal(2, records.Count);
            Assert.Null(records[0].HD_m);
            Assert.Null(records[1].HD_m);
        }

        [Fact]
        public void Parse_SupportsCommaDecimals()
        {
            var content = string.Join("\r\n", new[]
            {
                "KD3|Station 777",
                "For ModeC|Adr 3|Rb 1,25 m|HDback 4,5|Rf 1,75 m|HDfore 4,7"
            });

            var records = Parse(content);

            Assert.Equal(1.25, records[0].Rb_m);
            Assert.Equal(4.5, records[0].HD_m);
            Assert.Equal(1.75, records[1].Rf_m);
            Assert.Equal(4.7, records[1].HD_m);
        }

        [Fact]
        public void Parse_HandlesMultipleStations()
        {
            var content = string.Join("\r\n", new[]
            {
                "KD10|Segment 100",
                "For ModeD|Adr 1|Rb 0.90|Rf 0.80",
                "KD11|Segment 200",
                "For ModeE|Adr 2|Rb 1.00|Rf 0.95"
            });

            var records = Parse(content);

            Assert.Equal(4, records.Count);
            Assert.Equal("100", records[0].StationCode);
            Assert.Equal("100", records[1].StationCode);
            Assert.Equal("200", records[2].StationCode);
            Assert.Equal("200", records[3].StationCode);
        }

        private static List<Nivtropy.Models.MeasurementRecord> Parse(string content)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".dat");
            File.WriteAllText(tempFile, content, Encoding.UTF8);

            try
            {
                var parser = new DatParser();
                return parser.Parse(tempFile).ToList();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
