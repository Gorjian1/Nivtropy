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
            if (value is System.Windows.Data.CollectionViewGroup group)
            {
                // Поддержка как строк расчёта, так и журнальных строк
                var traverseRows = group.Items.OfType<TraverseRow>().ToList();
                if (traverseRows.Count > 0)
                {
                    var summary = traverseRows.First().LineSummary;
                    if (summary?.Closures.Count > 0)
                        return summary.ClosuresDisplay;

                    var fallback = traverseRows
                        .Where(r => r.DeltaH.HasValue)
                        .Sum(r => r.DeltaH!.Value);

                    return fallback.ToString("+0.0000;-0.0000;0.0000");
                }

                var journalRows = group.Items.OfType<JournalRow>().ToList();
                if (journalRows.Count > 0)
                {
                    var summary = journalRows.First().LineSummary;
                    if (summary?.Closures.Count > 0)
                        return summary.ClosuresDisplay;

                    var fallback = journalRows
                        .Where(r => r.RowType == JournalRowType.Elevation && r.DeltaH.HasValue)
                        .Sum(r => r.DeltaH!.Value);

                    return fallback.ToString("+0.0000;-0.0000;0.0000");
                }

                return "—";
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
