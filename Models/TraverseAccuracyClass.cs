using System.Collections.Generic;

namespace Nivtropy.Models
{
    public class TraverseAccuracyClass
    {
        public TraverseAccuracyClass(string id, string title, double maxHdDifferencePerSetup, double misclosureCoefficientMm, double recommendedSightLength)
        {
            Id = id;
            Title = title;
            MaxHdDifferencePerSetup = maxHdDifferencePerSetup;
            MisclosureCoefficientMm = misclosureCoefficientMm;
            RecommendedSightLength = recommendedSightLength;
        }

        public string Id { get; }
        public string Title { get; }
        /// <summary>
        /// Максимально допустимая разность длин заднего и переднего визиров на одной станции (м).
        /// </summary>
        public double MaxHdDifferencePerSetup { get; }
        /// <summary>
        /// Коэффициент допуска невязки в мм (C в формуле C * sqrt(L)).
        /// </summary>
        public double MisclosureCoefficientMm { get; }
        /// <summary>
        /// Рекомендуемая длина визирования (м) для проектирования.
        /// </summary>
        public double RecommendedSightLength { get; }

        public override string ToString() => Title;

        public static IReadOnlyList<TraverseAccuracyClass> Presets { get; } = new[]
        {
            new TraverseAccuracyClass("class_i", "Класс I (BF/FB ≤ 0,3 м)", 0.3, 4.0, 60.0),
            new TraverseAccuracyClass("class_ii", "Класс II (BF/FB ≤ 0,5 м)", 0.5, 6.0, 80.0),
            new TraverseAccuracyClass("tech", "Технический (BF/FB ≤ 1,0 м)", 1.0, 10.0, 100.0)
        };
    }
}
