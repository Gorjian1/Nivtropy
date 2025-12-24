using System;
using System.Globalization;
using System.Windows.Data;

namespace Nivtropy.Converters
{
    /// <summary>
    /// Конвертер для форматирования чисел с динамическим форматом
    /// </summary>
    public class DynamicFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return "—";

            // values[0] - число для форматирования
            // values[1] - строка формата (например, "F4" или "+0.0000;-0.0000;0.0000")

            if (values[0] == null || values[0] is not double number)
                return "—";

            if (values[1] is not string format || string.IsNullOrEmpty(format))
                return number.ToString(culture);

            try
            {
                // Проверяем, это формат для превышения (с плюсом/минусом) или обычный формат
                if (format.Contains(';'))
                {
                    // Формат для превышения: "+0.0000;-0.0000;0.0000"
                    return number.ToString(format, culture);
                }
                else
                {
                    // Обычный формат: "F4"
                    return number.ToString(format, culture);
                }
            }
            catch (FormatException)
            {
                // Неверный формат - возвращаем число без форматирования
                System.Diagnostics.Debug.WriteLine($"DynamicFormatConverter: Invalid format '{format}' for number {number}");
                return number.ToString(culture);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
