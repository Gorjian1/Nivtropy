using System;
using System.Collections.Generic;
using Nivtropy.Models;
using Nivtropy.Domain.DTOs;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Services
{
    /// <summary>
    /// Реализация сервиса валидации импортируемых данных нивелирования.
    /// Проверяет корректность значений и выявляет потенциальные проблемы.
    /// </summary>
    public class ImportValidationService : IImportValidationService
    {
        // Допустимые диапазоны значений (в метрах)
        private const double MinReading = 0.0;
        private const double MaxReading = 10.0;  // Типичный максимум для нивелирной рейки
        private const double MaxHorizontalDistance = 200.0;  // Максимальное плечо
        private const double MaxArmDifference = 10.0;  // Предупреждение о большой разности плеч
        private const double MaxElevation = 10000.0;  // Разумный предел высоты
        private const double MinElevation = -1000.0;

        public ValidationResult Validate(IReadOnlyList<MeasurementRecord> records)
        {
            var result = new ValidationResult();

            if (records == null || records.Count == 0)
            {
                result.AddError("Данные для валидации отсутствуют");
                return result;
            }

            int lineNumber = 1;
            string? currentLine = null;
            int stationsInLine = 0;
            double? prevForeReading = null;

            foreach (var record in records)
            {
                // Проверяем отдельную запись
                var recordResult = ValidateRecord(record, lineNumber);
                result.Errors.AddRange(recordResult.Errors);
                result.Warnings.AddRange(recordResult.Warnings);

                // Проверяем контекст хода
                if (record.LineSummary?.Header != currentLine)
                {
                    // Новый ход
                    if (currentLine != null && stationsInLine < 2)
                    {
                        result.AddWarning($"Ход '{currentLine}' содержит менее 2 станций", lineNumber);
                    }
                    currentLine = record.LineSummary?.Header;
                    stationsInLine = 0;
                    prevForeReading = null;
                }

                // Проверяем последовательность измерений
                if (record.Rb_m.HasValue && record.Rf_m.HasValue)
                {
                    stationsInLine++;

                    // Проверка непрерывности (задний отсчёт текущей станции должен быть близок к переднему предыдущей)
                    if (prevForeReading.HasValue && record.Rb_m.HasValue)
                    {
                        // Это не ошибка, но может указывать на пропущенную точку
                        // Здесь можно добавить проверку если нужно
                    }

                    prevForeReading = record.Rf_m;
                }

                lineNumber++;
            }

            // Финальная проверка последнего хода
            if (currentLine != null && stationsInLine < 2)
            {
                result.AddWarning($"Ход '{currentLine}' содержит менее 2 станций");
            }

            // Общая статистика
            if (result.Warnings.Count > 10)
            {
                result.AddWarning($"Обнаружено много предупреждений ({result.Warnings.Count}). Проверьте качество данных.");
            }

            return result;
        }

        public ValidationResult ValidateRecord(MeasurementRecord record, int lineNumber)
        {
            var result = new ValidationResult();

            if (record == null)
            {
                result.AddError("Пустая запись", lineNumber);
                return result;
            }

            // Пропускаем записи помеченные как ошибочные
            if (record.IsInvalidMeasurement)
            {
                return result;
            }

            // Валидация отсчётов
            ValidateReading(record.Rb_m, "Rb (задний)", lineNumber, result);
            ValidateReading(record.Rf_m, "Rf (передний)", lineNumber, result);

            // Валидация горизонтальных расстояний
            ValidateDistance(record.HdBack_m, "HD задний", lineNumber, result);
            ValidateDistance(record.HdFore_m, "HD передний", lineNumber, result);

            // Валидация высоты
            if (record.Z_m.HasValue)
            {
                if (record.Z_m < MinElevation || record.Z_m > MaxElevation)
                {
                    result.AddWarning($"Подозрительная высота: {record.Z_m:F3} м", lineNumber, "Z");
                }
            }

            // Проверка разности плеч на станции
            if (record.HdBack_m.HasValue && record.HdFore_m.HasValue)
            {
                var armDiff = Math.Abs(record.HdBack_m.Value - record.HdFore_m.Value);
                if (armDiff > MaxArmDifference)
                {
                    result.AddWarning($"Большая разность плеч: {armDiff:F2} м", lineNumber, "HD");
                }
            }

            // Проверка кода точки
            if (string.IsNullOrWhiteSpace(record.Target) && string.IsNullOrWhiteSpace(record.StationCode))
            {
                // Не ошибка для некоторых форматов, но предупреждение
                if (record.Rb_m.HasValue || record.Rf_m.HasValue)
                {
                    result.AddWarning("Отсутствует код точки", lineNumber, "Target/StationCode");
                }
            }

            // Проверка парности измерений
            if (record.Rb_m.HasValue != record.Rf_m.HasValue)
            {
                // Одиночные измерения допустимы в некоторых случаях
                // Это не ошибка, а информационное предупреждение
            }

            return result;
        }

        private void ValidateReading(double? value, string fieldName, int lineNumber, ValidationResult result)
        {
            if (!value.HasValue)
                return;

            if (value < MinReading)
            {
                result.AddError($"Отрицательный отсчёт: {value:F4} м", lineNumber, fieldName);
            }
            else if (value > MaxReading)
            {
                result.AddWarning($"Слишком большой отсчёт: {value:F4} м (макс. {MaxReading} м)", lineNumber, fieldName);
            }
        }

        private void ValidateDistance(double? value, string fieldName, int lineNumber, ValidationResult result)
        {
            if (!value.HasValue)
                return;

            if (value < 0)
            {
                result.AddError($"Отрицательное расстояние: {value:F2} м", lineNumber, fieldName);
            }
            else if (value > MaxHorizontalDistance)
            {
                result.AddWarning($"Слишком большое плечо: {value:F2} м (макс. {MaxHorizontalDistance} м)", lineNumber, fieldName);
            }
        }
    }
}
