using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.ViewModels;

namespace Nivtropy.Services.Tolerance
{
    /// <summary>
    /// Сервис для работы с допусками нивелирования
    /// Извлечён из TraverseCalculationViewModel для соблюдения SRP
    /// </summary>
    public class ToleranceService : IToleranceService
    {
        public double? CalculateTolerance(IToleranceOption? option, int stationsCount, double totalLengthKm)
        {
            if (option == null)
                return null;

            return option.Mode switch
            {
                ToleranceMode.SqrtStations => option.Coefficient * Math.Sqrt(Math.Max(stationsCount, 1)),
                ToleranceMode.SqrtLength => option.Coefficient * Math.Sqrt(Math.Max(totalLengthKm, 1e-6)),
                _ => null
            };
        }

        public bool IsWithinTolerance(double? closure, double? allowableClosure)
        {
            if (!closure.HasValue || !allowableClosure.HasValue)
                return false;

            return Math.Abs(closure.Value) <= allowableClosure.Value;
        }

        public string BuildToleranceVerdict(double? closure, double? methodTolerance, double? classTolerance,
            LevelingMethodOption? selectedMethod, LevelingClassOption? selectedClass)
        {
            if (!closure.HasValue)
                return "Нет данных для расчёта.";

            var toleranceCandidates = new[] { methodTolerance, classTolerance }
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (toleranceCandidates.Count == 0)
                return "Выберите метод или класс для оценки допуска.";

            var allowableClosure = toleranceCandidates.Min();
            var absClosure = Math.Abs(closure.Value);

            var verdict = absClosure <= allowableClosure
                ? "Общий вывод: в пределах допуска."
                : "Общий вывод: превышение допуска!";

            var details = new List<string>();

            if (methodTolerance.HasValue && selectedMethod != null)
            {
                details.Add(absClosure <= methodTolerance.Value
                    ? $"Метод {selectedMethod.Code}: в норме."
                    : $"Метод {selectedMethod.Code}: превышение.");
            }

            if (classTolerance.HasValue && selectedClass != null)
            {
                details.Add(absClosure <= classTolerance.Value
                    ? $"Класс {selectedClass.Code}: в норме."
                    : $"Класс {selectedClass.Code}: превышение.");
            }

            return details.Count > 0
                ? string.Join(" ", new[] { verdict }.Concat(details))
                : verdict;
        }
    }
}
