using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nivtropy.Models;
using Nivtropy.Services.Logging;

namespace Nivtropy.Infrastructure.Parsers
{
    /// <summary>
    /// Фасад для парсинга данных нивелирования из различных форматов файлов.
    /// Автоматически определяет формат и делегирует работу специализированному парсеру.
    /// Реализует паттерн Strategy для поддержки расширяемости.
    /// </summary>
    public class DatParser : IDataParser
    {
        private readonly ILoggerService? _logger;
        private readonly IReadOnlyList<IFormatParser> _formatParsers;
        private static readonly string[] CandidateEncodings = { "utf-8", "windows-1251", "cp1251", "latin1", "utf-16" };

        public DatParser() : this(null) { }

        public DatParser(ILoggerService? logger)
        {
            _logger = logger;
            _formatParsers = new IFormatParser[]
            {
                new TrimbleDiniParser(logger),
                new ForFormatParser(logger)
            };
        }

        /// <summary>
        /// Создаёт парсер с указанным набором форматных парсеров (для тестирования)
        /// </summary>
        public DatParser(ILoggerService? logger, IReadOnlyList<IFormatParser> formatParsers)
        {
            _logger = logger;
            _formatParsers = formatParsers;
        }

        #region IDataParser Implementation

        /// <summary>
        /// Асинхронно загружает и парсит файл данных нивелирования
        /// </summary>
        public async Task<List<MeasurementRecord>> LoadFromFileAsync(string filePath)
        {
            return await Task.Run(() => Parse(filePath, null).ToList());
        }

        /// <summary>
        /// Парсит данные из строк текста
        /// </summary>
        public List<MeasurementRecord> ParseLines(IEnumerable<string> lines, string? format = null)
        {
            var linesArray = lines.ToArray();
            var parser = SelectParser(linesArray, format);
            return parser.Parse(linesArray, null, null).ToList();
        }

        #endregion

        /// <summary>
        /// Парсит файл данных нивелирования
        /// </summary>
        public IEnumerable<MeasurementRecord> Parse(string path, string? synonymsConfigPath = null)
        {
            var text = ReadTextSmart(path);
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var parser = SelectParser(lines, format: null);
            return parser.Parse(lines, path, synonymsConfigPath);
        }

        /// <summary>
        /// Выбирает наиболее подходящий парсер для данных
        /// </summary>
        private IFormatParser SelectParser(string[] lines, string? format)
        {
            // Если формат указан явно
            if (!string.IsNullOrEmpty(format))
            {
                var byName = _formatParsers.FirstOrDefault(p =>
                    p.GetType().Name.StartsWith(format, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                    return byName;
            }

            // Автоопределение по содержимому
            var bestParser = _formatParsers[0];
            var bestScore = 0;

            foreach (var parser in _formatParsers)
            {
                var score = parser.GetFormatScore(lines);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestParser = parser;
                }
            }

            _logger?.LogDebug($"Выбран парсер {bestParser.GetType().Name} (score: {bestScore})");
            return bestParser;
        }

        private string ReadTextSmart(string path)
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
                    _logger?.LogWarning($"Не удалось прочитать файл в кодировке {encName}: {ex.Message}");
                    last = ex;
                }
            }

            var errorMessage = "Не удалось прочитать файл в известных кодировках.";
            _logger?.LogError(errorMessage, last);
            throw last ?? new IOException(errorMessage);
        }
    }
}
