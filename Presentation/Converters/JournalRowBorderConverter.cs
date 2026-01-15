using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Nivtropy.Models;

namespace Nivtropy.Presentation.Converters
{
    /// <summary>
    /// Конвертер для создания различных границ между строками журнала.
    /// Между станциями - толстая граница, между точками внутри станции - тонкая.
    /// </summary>
    public class JournalRowBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is JournalRowType rowType)
            {
                // Толстая граница снизу для передней точки (конец станции)
                // Тонкая граница для остальных строк
                if (rowType == JournalRowType.ForePoint)
                {
                    // Толстая граница снизу между станциями
                    return new Thickness(0, 0, 0, 2);
                }
                else
                {
                    // Тонкая граница для задней точки и превышения
                    return new Thickness(0, 0, 0, 0.5);
                }
            }

            return new Thickness(0, 0, 0, 1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
