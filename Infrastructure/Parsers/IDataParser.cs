using System.Collections.Generic;
using System.Threading.Tasks;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Infrastructure.Parsers
{
    /// <summary>
    /// Интерфейс парсера данных нивелирования из различных форматов файлов
    /// </summary>
    public interface IDataParser
    {
        /// <summary>
        /// Парсит файл данных нивелирования
        /// </summary>
        /// <param name="path">Путь к файлу</param>
        /// <param name="synonymsConfigPath">Путь к конфигурации синонимов (необязательно)</param>
        /// <returns>Список записей измерений</returns>
        IEnumerable<MeasurementDto> Parse(string path, string? synonymsConfigPath = null);

        /// <summary>
        /// Загружает и парсит файл данных нивелирования асинхронно
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>Список записей измерений</returns>
        Task<List<MeasurementDto>> LoadFromFileAsync(string filePath);

        /// <summary>
        /// Парсит данные из строк текста
        /// </summary>
        /// <param name="lines">Строки данных</param>
        /// <param name="format">Формат данных (необязательно)</param>
        /// <returns>Список записей измерений</returns>
        List<MeasurementDto> ParseLines(IEnumerable<string> lines, string? format = null);
    }
}
