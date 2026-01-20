using System.Collections.Generic;
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
        ValidationResult Validate(IReadOnlyList<MeasurementDto> records);

        /// <summary>
        /// Валидирует одну запись измерения
        /// </summary>
        ValidationResult ValidateRecord(MeasurementDto record, int lineNumber);
    }
}
