using System.Collections.Generic;
using Nivtropy.Domain.Model;

namespace Nivtropy.Infrastructure.Parsers
{
    /// <summary>
    /// Интерфейс парсера для конкретного формата файлов нивелирования
    /// </summary>
    public interface IFormatParser
    {
        /// <summary>
        /// Вычисляет оценку (0-100) соответствия данных этому формату
        /// </summary>
        /// <param name="sampleLines">Первые N строк файла для анализа</param>
        /// <returns>Оценка от 0 до 100, где 100 - точное соответствие</returns>
        int GetFormatScore(string[] sampleLines);

        /// <summary>
        /// Парсит строки файла в записи измерений
        /// </summary>
        /// <param name="lines">Строки файла</param>
        /// <param name="filePath">Путь к файлу (для резолва конфигов)</param>
        /// <param name="synonymsConfigPath">Путь к конфигу синонимов</param>
        /// <returns>Перечисление записей измерений</returns>
        IEnumerable<MeasurementRecord> Parse(string[] lines, string? filePath = null, string? synonymsConfigPath = null);
    }
}
