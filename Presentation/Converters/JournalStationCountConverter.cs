using System;
using System.Globalization;
using System.Windows.Data;

namespace Nivtropy.Presentation.Converters
{
    /// <summary>
    /// Конвертер для отображения количества станций в журнальном представлении.
    /// Используем ItemCount напрямую, так как каждая строка представляет одну станцию.
    /// </summary>
    public class JournalStationCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int itemCount)
                return itemCount;

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
