using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Nivtropy.Presentation.Converters
{
    public class MethodDeltaConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
            {
                return Binding.DoNothing;
            }

            var delta = ExtractNullableDouble(values[0], culture);
            var sign = ExtractNullableDouble(values[1], culture);

            if (!delta.HasValue || !sign.HasValue)
            {
                return null;
            }

            return delta.Value * sign.Value;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            targetTypes.Select(_ => Binding.DoNothing).ToArray();

        private static double? ExtractNullableDouble(object value, CultureInfo culture)
        {
            if (value is null || value == DependencyProperty.UnsetValue)
            {
                return null;
            }

            if (value is double d)
            {
                return d;
            }

            if (value is IFormattable formattable)
            {
                if (double.TryParse(formattable.ToString(null, culture), NumberStyles.Float, culture, out var result))
                {
                    return result;
                }
            }

            if (value is string s && double.TryParse(s, NumberStyles.Float, culture, out var parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
