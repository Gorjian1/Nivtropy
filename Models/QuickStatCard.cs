namespace Nivtropy.Models
{
    public class QuickStatCard
    {
        public QuickStatCard(string title, string value, string description, string accentHex)
        {
            Title = title;
            Value = value;
            Description = description;
            AccentHex = accentHex;
        }

        public string Title { get; }
        public string Value { get; }
        public string Description { get; }
        public string AccentHex { get; }
    }
}
