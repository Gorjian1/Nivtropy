using System.Collections.Generic;
using Nivtropy.Presentation.Models; // TODO: Remove after migrating to Domain models

namespace Nivtropy.Infrastructure.Export
{
    /// <summary>
    /// Интерфейс сервиса для экспорта данных нивелирования
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Экспортирует данные ходов в CSV файл
        /// </summary>
        /// <param name="rows">Строки данных для экспорта</param>
        /// <param name="filePath">Путь к файлу для сохранения (если null, показывает диалог)</param>
        /// <returns>true если экспорт успешен, false в противном случае</returns>
        bool ExportToCsv(IEnumerable<TraverseRow> rows, string? filePath = null);
    }
}
