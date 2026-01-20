using System.Collections.Generic;
using Nivtropy.Domain.Model;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Интерфейс сервиса валидации импортируемых данных нивелирования
    /// </summary>
    public interface IImportValidationService
    {
        /// <summary>
        /// Валидирует список записей измерений
        /// </summary>
        ValidationResult Validate(IReadOnlyList<MeasurementRecord> records);

        /// <summary>
        /// Валидирует одну запись измерения
        /// </summary>
        ValidationResult ValidateRecord(MeasurementRecord record, int lineNumber);
    }
}
