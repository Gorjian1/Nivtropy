using System.Collections.Generic;
using System.Threading.Tasks;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Интерфейс парсера данных нивелирования из различных форматов файлов
    /// </summary>
    public interface IDataParser
    {
        /// <summary>
        /// Загружает и парсит файл данных нивелирования
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>Список записей измерений</returns>
        Task<List<MeasurementRecord>> LoadFromFileAsync(string filePath);

        /// <summary>
        /// Парсит данные из строк текста
        /// </summary>
        /// <param name="lines">Строки данных</param>
        /// <param name="format">Формат данных (необязательно)</param>
        /// <returns>Список записей измерений</returns>
        List<MeasurementRecord> ParseLines(IEnumerable<string> lines, string? format = null);
    }
}
