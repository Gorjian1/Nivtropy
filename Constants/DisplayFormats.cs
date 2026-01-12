namespace Nivtropy.Constants
{
    /// <summary>
    /// Константы форматирования для отображения данных нивелирования
    /// </summary>
    public static class DisplayFormats
    {
        /// <summary>
        /// Формат для отображения превышений (со знаком, 4 десятичных знака)
        /// </summary>
        public const string DeltaH = "+0.0000;-0.0000;0.0000";

        /// <summary>
        /// Формат для отображения высот (4 десятичных знака)
        /// </summary>
        public const string Height = "0.0000";

        /// <summary>
        /// Формат для отображения расстояний (2 десятичных знака)
        /// </summary>
        public const string Distance = "0.00";

        /// <summary>
        /// Заменитель для отсутствующего значения
        /// </summary>
        public const string EmptyValue = "—";
    }

    /// <summary>
    /// Константы для генератора данных
    /// </summary>
    public static class GeneratorDefaults
    {
        /// <summary>
        /// СКО для измерений (мм)
        /// </summary>
        public const double StdDevMeasurement = 0.5;

        /// <summary>
        /// СКО для грубых ошибок (мм)
        /// </summary>
        public const double StdDevGrossError = 2.0;

        /// <summary>
        /// Частота грубых ошибок (каждая N-ная станция)
        /// </summary>
        public const int GrossErrorFrequency = 10;

        /// <summary>
        /// Минимальное расстояние между точками (м)
        /// </summary>
        public const double MinDistance = 5.0;

        /// <summary>
        /// Максимальное расстояние между точками (м)
        /// </summary>
        public const double MaxDistance = 15.0;

        /// <summary>
        /// Коэффициент перевода мм в м
        /// </summary>
        public const double MmToMeters = 1000.0;
    }

    /// <summary>
    /// Константы настроек по умолчанию
    /// </summary>
    public static class SettingsDefaults
    {
        /// <summary>
        /// Минимальная длина луча (м)
        /// </summary>
        public const double MinimumRayLength = 5.0;

        /// <summary>
        /// Максимальная длина луча (м)
        /// </summary>
        public const double MaximumRayLength = 100.0;

        /// <summary>
        /// Минимальная длина станции (м)
        /// </summary>
        public const double MinimumStationLength = 10.0;

        /// <summary>
        /// Чувствительность σ для выбросов
        /// </summary>
        public const double OutlierSensitivitySigma = 2.5;
    }
}
