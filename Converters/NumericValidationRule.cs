using System;
using System.Globalization;
using System.Windows.Controls;

namespace Nivtropy.Converters
{
    /// <summary>
    /// Validates numeric input for editable DataGrid cells.
    /// </summary>
    public class NumericValidationRule : ValidationRule
    {
        /// <summary>
        /// Allows empty values to pass validation, resulting in a null value in the bound property.
        /// </summary>
        public bool AllowNull { get; set; } = true;

        /// <summary>
        /// Maximum number of decimal places permitted. If null, any precision is allowed.
        /// </summary>
        public int? MaxDecimalPlaces { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = (value ?? string.Empty).ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return AllowNull
                    ? ValidationResult.ValidResult
                    : new ValidationResult(false, "Значение обязательно для заполнения");
            }

            if (!double.TryParse(text, NumberStyles.Float, cultureInfo, out _))
            {
                return new ValidationResult(false, "Введите числовое значение");
            }

            if (MaxDecimalPlaces.HasValue)
            {
                var separator = cultureInfo.NumberFormat.NumberDecimalSeparator;
                var parts = text.Split(new[] { separator }, StringSplitOptions.None);
                var decimalsLength = parts.Length > 1 ? parts[1].Length : 0;

                if (decimalsLength > MaxDecimalPlaces.Value)
                {
                    return new ValidationResult(false, $"Допустимо не более {MaxDecimalPlaces.Value} знаков после запятой");
                }
            }

            return ValidationResult.ValidResult;
        }
    }
}
