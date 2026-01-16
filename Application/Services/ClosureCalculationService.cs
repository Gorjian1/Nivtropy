using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Реализация сервиса расчёта невязки и допусков нивелирного хода
    /// </summary>
    public class ClosureCalculationService : IClosureCalculationService
    {
        public double? CalculateClosure(IReadOnlyList<StationDto> rows, double orientationSign)
        {
            if (rows == null || rows.Count == 0)
                return null;

            var deltaHValues = rows
                .Where(r => r.DeltaH.HasValue)
                .Select(r => r.DeltaH!.Value)
                .ToList();

            if (deltaHValues.Count == 0)
                return null;

            return deltaHValues.Sum() * orientationSign;
        }

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

        public ClosureCalculationResult Calculate(
            IReadOnlyList<StationDto> rows,
            double orientationSign,
            int stationsCount,
            double totalLengthKm,
            IToleranceOption? methodOption,
            IToleranceOption? classOption)
        {
            var result = new ClosureCalculationResult();

            // Расчёт невязки
            result.Closure = CalculateClosure(rows, orientationSign);

            if (!result.Closure.HasValue || stationsCount == 0)
            {
                result.Verdict = stationsCount == 0
                    ? "Нет данных для расчёта."
                    : "Выберите параметры расчёта.";
                return result;
            }

            // Расчёт допусков
            result.MethodTolerance = CalculateTolerance(methodOption, stationsCount, totalLengthKm);
            result.ClassTolerance = CalculateTolerance(classOption, stationsCount, totalLengthKm);

            // Определение допустимой невязки (минимум из доступных)
            var toleranceCandidates = new[] { result.MethodTolerance, result.ClassTolerance }
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            result.AllowableClosure = toleranceCandidates.Count > 0
                ? toleranceCandidates.Min()
                : (double?)null;

            // Генерация вердикта
            result.Verdict = GenerateVerdict(
                result.Closure,
                result.AllowableClosure,
                result.MethodTolerance,
                result.ClassTolerance,
                methodOption?.Code,
                classOption?.Code);

            return result;
        }

        public string GenerateVerdict(
            double? closure,
            double? allowableClosure,
            double? methodTolerance,
            double? classTolerance,
            string? methodCode,
            string? classCode)
        {
            if (!closure.HasValue)
                return "Нет данных для расчёта.";

            if (!allowableClosure.HasValue)
                return "Выберите метод или класс для оценки допуска.";

            var absClosure = Math.Abs(closure.Value);

            var verdict = absClosure <= allowableClosure.Value
                ? "Общий вывод: в пределах допуска."
                : "Общий вывод: превышение допуска!";

            var details = new List<string>();

            if (methodTolerance.HasValue && !string.IsNullOrEmpty(methodCode))
            {
                details.Add(absClosure <= methodTolerance.Value
                    ? $"Метод {methodCode}: в норме."
                    : $"Метод {methodCode}: превышение.");
            }

            if (classTolerance.HasValue && !string.IsNullOrEmpty(classCode))
            {
                details.Add(absClosure <= classTolerance.Value
                    ? $"Класс {classCode}: в норме."
                    : $"Класс {classCode}: превышение.");
            }

            return details.Count > 0
                ? string.Join(" ", new[] { verdict }.Concat(details))
                : verdict;
        }
    }
}
