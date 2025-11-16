namespace Nivtropy.Models
{
    public class RawCalculationItem
    {
        public RawCalculationItem(string label, double? value, string units, string description)
        {
            Label = label;
            Value = value;
            Units = units;
            Description = description;
        }

        public string Label { get; }
        public double? Value { get; }
        public string Units { get; }
        public string Description { get; }
    }
}
