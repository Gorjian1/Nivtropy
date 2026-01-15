namespace Nivtropy.Domain.Services
{
    /// <summary>
    /// Интерфейс калькулятора допусков для нивелирования
    /// </summary>
    public interface IToleranceCalculator
    {
        /// <summary>
        /// Рассчитывает допуск по количеству станций
        /// Формула: допуск_мм = K * sqrt(n), где n - количество станций
        /// </summary>
        double CalculateByStationCount(int stationCount, LevelingClass levelingClass);

        /// <summary>
        /// Рассчитывает допуск по длине хода
        /// Формула: допуск_мм = K * sqrt(L), где L - длина в километрах
        /// </summary>
        double CalculateByLength(double lengthKm, LevelingClass levelingClass);
    }

    /// <summary>
    /// Класс точности нивелирования согласно инструкции
    /// </summary>
    public enum LevelingClass
    {
        /// <summary>I класс (высшая точность)</summary>
        ClassI = 0,

        /// <summary>II класс</summary>
        ClassII = 1,

        /// <summary>III класс</summary>
        ClassIII = 2,

        /// <summary>IV класс</summary>
        ClassIV = 3,

        /// <summary>Техническое нивелирование</summary>
        Technical = 4
    }
}
