using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nivtropy.Presentation.Converters
{
    /// <summary>
    /// Конвертер для перевода значений из метров в миллиметры
    /// </summary>
    public class MetersToMillimetersConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null || value == DependencyProperty.UnsetValue)
            {
                return "—";
            }

            if (value is double meters)
            {
                var millimeters = meters * 1000.0;
                return millimeters.ToString("+0.00;-0.00;0.00", culture);
            }

            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
