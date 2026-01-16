using System;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Сервис для генерации нормально распределённого шума.
    /// Используется для симуляции ошибок измерений и грубых промахов.
    /// </summary>
    public interface INoiseGeneratorService
    {
        /// <summary>
        /// Генерирует нормально распределённый шум
        /// </summary>
        /// <param name="index">Индекс измерения (для определения грубых ошибок)</param>
        /// <param name="stdDevMeasurement">СКО для обычных измерений (мм)</param>
        /// <param name="stdDevGrossError">СКО для грубых ошибок (мм)</param>
        /// <param name="grossErrorFrequency">Частота грубых ошибок (каждая N-ная станция)</param>
        /// <returns>Шум в миллиметрах</returns>
        double GenerateNoise(
            int index,
            double stdDevMeasurement,
            double stdDevGrossError,
            int grossErrorFrequency);

        /// <summary>
        /// Устанавливает seed для генератора случайных чисел
        /// </summary>
        /// <param name="seed">Seed для воспроизводимости</param>
        void SetSeed(int seed);
    }
}
