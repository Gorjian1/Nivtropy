using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Presentation.Converters
{
    /// <summary>
    /// Конвертер для вычисления цвета фона строки на основе режима раскраски
    /// Office-like цветовая схема
    /// </summary>
    public class RowBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
                return Brushes.White;

            // values[0] - RowColoringMode
            // values[1] - Индекс строки внутри хода (Index)
            // values[2] - Общее количество строк в ходе (из LineSummary.RecordCount)

            if (values[0] is not RowColoringMode mode)
                return Brushes.White;

            switch (mode)
            {
                case RowColoringMode.None:
                    // Без раскраски - чистый белый
                    return Brushes.White;

                case RowColoringMode.Alternating:
                    // Чередующиеся цвета в стиле Office
                    if (values[1] is int index)
                    {
                        return index % 2 == 1
                            ? new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)) // Очень светлый серый #FAFAFA
                            : Brushes.White;
                    }
                    return Brushes.White;

                case RowColoringMode.Gradient:
                    // Градиент в стиле Office - от белого к светло-голубому
                    if (values[1] is int idx && values[2] is int totalCount && totalCount > 0)
                    {
                        // Плавный переход от белого к светло-голубому
                        double ratio = (double)idx / totalCount;

                        // Белый (255, 255, 255) -> Светло-голубой (#E8F4F8 = 232, 244, 248)
                        byte r = (byte)(255 - (ratio * 23)); // 255 -> 232
                        byte g = (byte)(255 - (ratio * 11)); // 255 -> 244
                        byte b = (byte)(255 - (ratio * 7));  // 255 -> 248

                        return new SolidColorBrush(Color.FromRgb(r, g, b));
                    }
                    return Brushes.White;

                default:
                    return Brushes.White;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
