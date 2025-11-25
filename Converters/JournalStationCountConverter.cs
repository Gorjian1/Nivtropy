using System;
using System.Globalization;
using System.Windows.Data;

namespace Nivtropy.Converters
{
    /// <summary>
    /// Конвертер для отображения количества станций в журнальном представлении.
    /// Так как каждая станция = 3 строки (задняя точка, превышение, передняя точка),
    /// делим ItemCount на 3
    /// </summary>
    public class JournalStationCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int itemCount)
            {
                // Каждая станция = 3 строки
                return itemCount / 3;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
