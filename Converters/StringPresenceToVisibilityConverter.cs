using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nivtropy.Converters
{
    /// <summary>
    /// Показывает элемент, если строка не пустая.
    /// </summary>
    public class StringPresenceToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value switch
            {
                null => string.Empty,
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            return string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
