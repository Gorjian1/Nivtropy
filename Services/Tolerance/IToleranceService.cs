using Nivtropy.ViewModels;

namespace Nivtropy.Services.Tolerance
{
    /// <summary>
    /// Интерфейс сервиса для работы с допусками нивелирования
    /// </summary>
    public interface IToleranceService
    {
        /// <summary>
        /// Вычисляет допуск невязки по выбранному методу или классу
        /// </summary>
        /// <param name="option">Опция допуска (метод или класс)</param>
        /// <param name="stationsCount">Количество станций</param>
        /// <param name="totalLengthKm">Общая длина хода в километрах</param>
        /// <returns>Вычисленный допуск или null если невозможно вычислить</returns>
        double? CalculateTolerance(IToleranceOption? option, int stationsCount, double totalLengthKm);

        /// <summary>
        /// Проверяет, находится ли невязка в пределах допуска
        /// </summary>
        /// <param name="closure">Невязка</param>
        /// <param name="allowableClosure">Допустимая невязка</param>
        /// <returns>true если в пределах допуска, false в противном случае</returns>
        bool IsWithinTolerance(double? closure, double? allowableClosure);

        /// <summary>
        /// Формирует текстовое описание результата проверки допуска
        /// </summary>
        /// <param name="closure">Невязка</param>
        /// <param name="methodTolerance">Допуск по методу</param>
        /// <param name="classTolerance">Допуск по классу</param>
        /// <param name="selectedMethod">Выбранный метод</param>
        /// <param name="selectedClass">Выбранный класс</param>
        /// <returns>Описание результата проверки</returns>
        string BuildToleranceVerdict(double? closure, double? methodTolerance, double? classTolerance,
            LevelingMethodOption? selectedMethod, LevelingClassOption? selectedClass);
    }
}
