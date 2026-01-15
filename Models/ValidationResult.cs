using System.Collections.Generic;

namespace Nivtropy.Models
{
    /// <summary>
    /// Результат валидации данных с ошибками и предупреждениями
    /// </summary>
    public class ValidationResult
    {
        public List<ValidationMessage> Errors { get; } = new();
        public List<ValidationMessage> Warnings { get; } = new();

        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;

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
    /// Сообщение валидации (ошибка или предупреждение)
    /// </summary>
    public record ValidationMessage(string Message, int? LineNumber = null, string? Field = null)
    {
        public override string ToString()
        {
            var prefix = LineNumber.HasValue ? $"Строка {LineNumber}: " : "";
            var fieldSuffix = !string.IsNullOrEmpty(Field) ? $" ({Field})" : "";
            return $"{prefix}{Message}{fieldSuffix}";
        }
    }
}
