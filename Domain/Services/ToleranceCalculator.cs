using System;

namespace Nivtropy.Domain.Services
{
    /// <summary>
    /// Калькулятор допусков для нивелирования
    /// Формулы согласно инструкции по нивелированию
    /// </summary>
    public class ToleranceCalculator : IToleranceCalculator
    {
        /// <summary>
        /// Коэффициенты для расчёта допуска по станциям: допуск = K * sqrt(n)
        /// где n - количество станций
        /// </summary>
        private static readonly double[] StationCoefficients = new[]
        {
            0.5,  // ClassI:   ±0.5√n мм
            1.0,  // ClassII:  ±1.0√n мм
            2.0,  // ClassIII: ±2.0√n мм
            5.0,  // ClassIV:  ±5.0√n мм
            10.0  // Technical: ±10.0√n мм
        };

        /// <summary>
        /// Коэффициенты для расчёта допуска по длине: допуск = K * sqrt(L)
        /// где L - длина хода в километрах
        /// </summary>
        private static readonly double[] LengthCoefficients = new[]
        {
            0.5,   // ClassI:   ±0.5√L мм
            2.0,   // ClassII:  ±2.0√L мм
            5.0,   // ClassIII: ±5.0√L мм
            10.0,  // ClassIV:  ±10.0√L мм
            20.0   // Technical: ±20.0√L мм
        };

        /// <inheritdoc/>
        public double CalculateByStationCount(int stationCount, LevelingClass levelingClass)
        {
            if (stationCount < 0)
                throw new ArgumentOutOfRangeException(nameof(stationCount), "Station count cannot be negative");

            var coefficient = StationCoefficients[(int)levelingClass];
            return coefficient * Math.Sqrt(Math.Max(stationCount, 1));
        }

        /// <inheritdoc/>
        public double CalculateByLength(double lengthKm, LevelingClass levelingClass)
        {
            if (lengthKm < 0)
                throw new ArgumentOutOfRangeException(nameof(lengthKm), "Length cannot be negative");

            var coefficient = LengthCoefficients[(int)levelingClass];
            return coefficient * Math.Sqrt(Math.Max(lengthKm, 1e-6));
        }
    }
}
