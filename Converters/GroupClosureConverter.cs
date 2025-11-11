using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Nivtropy.Models;

namespace Nivtropy.Converters
{
    public class GroupClosureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CollectionViewGroup group)
            {
                var items = group.Items.OfType<TraverseRow>().ToList();
                if (items.Count == 0)
                {
                    return null;
                }

                var sum = items
                    .Where(r => r.DeltaH.HasValue)
                    .Sum(r => r.DeltaH!.Value);

                return sum;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
