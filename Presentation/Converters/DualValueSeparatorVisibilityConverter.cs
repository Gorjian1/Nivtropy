using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nivtropy.Presentation.Converters
{
    /// <summary>
    /// Делает разделитель видимым только когда оба значения присутствуют.
    /// </summary>
    public class DualValueSeparatorVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return Visibility.Collapsed;

            var first = values[0] as string;
            var second = values[1] as string;

            return (!string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second))
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
