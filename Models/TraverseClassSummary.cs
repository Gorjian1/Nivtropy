using System.Globalization;

namespace Nivtropy.Models
{
    public class TraverseClassSummary
    {
        public TraverseClassSummary(string code, int count, double sum)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "—" : code;
            Count = count;
            Sum = sum;
        }

        public string Code { get; }
        public int Count { get; }
        public double Sum { get; }
        public double Average => Count > 0 ? Sum / Count : 0d;

        public string SumDisplay => Sum.ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture);
        public string AverageDisplay => Average.ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture);
    }
}
