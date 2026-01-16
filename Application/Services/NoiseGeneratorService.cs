using System;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Реализация сервиса генерации нормально распределённого шума
    /// Использует алгоритм Box-Muller transform
    /// </summary>
    public class NoiseGeneratorService : INoiseGeneratorService
    {
        private Random _random;

        public NoiseGeneratorService()
        {
            _random = new Random();
        }

        /// <summary>
        /// Генерирует нормально распределённый шум
        /// </summary>
        public double GenerateNoise(
            int index,
            double stdDevMeasurement,
            double stdDevGrossError,
            int grossErrorFrequency)
        {
            // Проверяем, нужно ли добавить грубую ошибку
            bool isGrossError = grossErrorFrequency > 0 && index % grossErrorFrequency == 0;
            double stdDev = isGrossError ? stdDevGrossError : stdDevMeasurement;

            // Генерируем нормально распределённый шум (Box-Muller transform)
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            return randStdNormal * stdDev; // в мм
        }

        /// <summary>
        /// Устанавливает seed для генератора случайных чисел
        /// </summary>
        public void SetSeed(int seed)
        {
            _random = new Random(seed);
        }
    }
}
