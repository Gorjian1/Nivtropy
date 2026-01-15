using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Nivtropy.Presentation.Converters
{
    /// <summary>
    /// Конвертер для выбора цвета линий сетки по индексу
    /// </summary>
    public class GridLineColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index switch
                {
                    0 => new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), // Светло-серый
                    1 => new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), // Серый
                    2 => new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70)), // Темно-серый
                    3 => new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9)), // Голубоватый
                    _ => new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
                };
            }

            return new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
