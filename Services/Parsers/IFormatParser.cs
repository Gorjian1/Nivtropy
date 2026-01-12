using System.Collections.Generic;
using Nivtropy.Models;

namespace Nivtropy.Services.Parsers
{
    /// <summary>
    /// Интерфейс для парсеров специфичных форматов файлов нивелирования.
    /// Реализует паттерн Strategy для поддержки разных форматов данных.
    /// </summary>
    public interface IFormatParser
    {
        /// <summary>
        /// Парсит строки данных в записи измерений
        /// </summary>
        /// <param name="lines">Строки данных</param>
        /// <param name="filePath">Путь к файлу (для загрузки конфигов)</param>
        /// <param name="synonymsConfigPath">Путь к конфигурации синонимов</param>
        /// <returns>Последовательность записей измерений</returns>
        IEnumerable<MeasurementRecord> Parse(string[] lines, string? filePath = null, string? synonymsConfigPath = null);

        /// <summary>
        /// Проверяет, поддерживает ли парсер данный формат
        /// </summary>
        /// <param name="sampleLines">Образец строк для анализа</param>
        /// <returns>Оценка совместимости (0-100)</returns>
        int GetFormatScore(string[] sampleLines);
    }
}
