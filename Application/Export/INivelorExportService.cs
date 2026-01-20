using System.Collections.Generic;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Export;

/// <summary>
/// Сервис для экспорта данных в формат Nivelir (Leica FOR формат)
/// </summary>
public interface INivelorExportService
{
    /// <summary>
    /// Экспортирует измерения в формат Nivelir
    /// </summary>
    /// <param name="measurements">Список измерений для экспорта</param>
    /// <param name="filePath">Путь к файлу для сохранения</param>
    void Export(IEnumerable<GeneratedMeasurementDto> measurements, string filePath);

    /// <summary>
    /// Экспортирует измерения в формат Nivelir и возвращает содержимое как строку
    /// </summary>
    /// <param name="measurements">Список измерений для экспорта</param>
    /// <param name="fileName">Имя файла (для заголовка)</param>
    /// <returns>Содержимое файла в формате Nivelir</returns>
    string ExportToString(IEnumerable<GeneratedMeasurementDto> measurements, string fileName);
}
