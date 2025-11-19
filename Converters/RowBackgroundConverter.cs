using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Nivtropy.Models;

namespace Nivtropy.Converters
{
    /// <summary>
    /// Конвертер для вычисления цвета фона строки на основе режима раскраски
    /// </summary>
    public class RowBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
                return Brushes.Transparent;

            // values[0] - RowColoringMode
            // values[1] - Индекс строки внутри хода (Index)
            // values[2] - Общее количество строк в ходе (из LineSummary.RecordCount)

            if (values[0] is not RowColoringMode mode)
                return Brushes.Transparent;

            switch (mode)
            {
                case RowColoringMode.None:
                    return Brushes.Transparent;

                case RowColoringMode.Alternating:
                    if (values[1] is int index)
                    {
                        return index % 2 == 1
                            ? new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9))
                            : Brushes.Transparent;
                    }
                    return Brushes.Transparent;

                case RowColoringMode.Gradient:
                    if (values[1] is int idx && values[2] is int totalCount && totalCount > 0)
                    {
                        // Вычисляем интенсивность серого от 0 (белый) до ~15 (светло-серый)
                        // Чем больше индекс, тем темнее
                        double ratio = (double)idx / totalCount;
                        byte grayValue = (byte)(255 - (ratio * 15)); // От 255 (белый) до 240 (светло-серый)
                        return new SolidColorBrush(Color.FromRgb(grayValue, grayValue, grayValue));
                    }
                    return Brushes.Transparent;

                default:
                    return Brushes.Transparent;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
