using System.Collections.Generic;
using Nivtropy.Models;

namespace Nivtropy.Services.Validation
{
    /// <summary>
    /// Результат валидации импортированных данных
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;
        public List<ValidationMessage> Errors { get; } = new();
        public List<ValidationMessage> Warnings { get; } = new();

        public void AddError(string message, int? lineNumber = null, string? field = null)
        {
            Errors.Add(new ValidationMessage(message, lineNumber, field));
        }

        public void AddWarning(string message, int? lineNumber = null, string? field = null)
        {
            Warnings.Add(new ValidationMessage(message, lineNumber, field));
        }
    }

    /// <summary>
    /// Сообщение валидации
    /// </summary>
    public record ValidationMessage(string Message, int? LineNumber, string? Field)
    {
        public override string ToString()
        {
            var parts = new List<string>();
            if (LineNumber.HasValue)
                parts.Add($"Строка {LineNumber}");
            if (!string.IsNullOrEmpty(Field))
                parts.Add($"[{Field}]");
            parts.Add(Message);
            return string.Join(": ", parts);
        }
    }

    /// <summary>
    /// Сервис валидации импортируемых данных нивелирования
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
