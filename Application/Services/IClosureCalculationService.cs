using System;
using System.Collections.Generic;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Режим расчёта допуска невязки
    /// </summary>
    public enum ToleranceMode
    {
        /// <summary>Допуск = коэффициент × √n (число станций)</summary>
        SqrtStations,
        /// <summary>Допуск = коэффициент × √L (длина в км)</summary>
        SqrtLength
    }

    /// <summary>
    /// Опция для расчёта допуска (метод или класс нивелирования)
    /// </summary>
    public interface IToleranceOption
    {
        string Code { get; }
        string Description { get; }
        ToleranceMode Mode { get; }
        double Coefficient { get; }
    }

    /// <summary>
    /// Результат расчёта невязки и допуска
    /// </summary>
    public class ClosureCalculationResult
    {
        /// <summary>Невязка хода (мм)</summary>
        public double? Closure { get; set; }

        /// <summary>Допустимая невязка (мм)</summary>
        public double? AllowableClosure { get; set; }

        /// <summary>Допуск по методу (мм)</summary>
        public double? MethodTolerance { get; set; }

        /// <summary>Допуск по классу (мм)</summary>
        public double? ClassTolerance { get; set; }

        /// <summary>Текстовый вердикт</summary>
        public string Verdict { get; set; } = "Нет данных для расчёта.";

        /// <summary>Невязка в пределах допуска</summary>
        public bool IsWithinTolerance => Closure.HasValue && AllowableClosure.HasValue &&
                                         Math.Abs(Closure.Value) <= AllowableClosure.Value;
    }

    /// <summary>
    /// Сервис расчёта невязки и допусков нивелирного хода
    /// </summary>
    public interface IClosureCalculationService
    {
        /// <summary>
        /// Рассчитывает невязку хода
        /// </summary>
        /// <param name="rows">Строки хода</param>
        /// <param name="orientationSign">Знак ориентации (+1 для BF, -1 для FB)</param>
        double? CalculateClosure(IReadOnlyList<TraverseRow> rows, double orientationSign);

        /// <summary>
        /// Рассчитывает допуск по заданной опции
        /// </summary>
        /// <param name="option">Опция (метод или класс)</param>
        /// <param name="stationsCount">Число станций</param>
        /// <param name="totalLengthKm">Длина хода в км</param>
        double? CalculateTolerance(IToleranceOption? option, int stationsCount, double totalLengthKm);

        /// <summary>
        /// Выполняет полный расчёт невязки и допусков
        /// </summary>
        ClosureCalculationResult Calculate(
            IReadOnlyList<TraverseRow> rows,
            double orientationSign,
            int stationsCount,
            double totalLengthKm,
            IToleranceOption? methodOption,
            IToleranceOption? classOption);

        /// <summary>
        /// Генерирует текстовый вердикт по результатам расчёта
        /// </summary>
        string GenerateVerdict(
            double? closure,
            double? allowableClosure,
            double? methodTolerance,
            double? classTolerance,
            string? methodCode,
            string? classCode);
    }
}
