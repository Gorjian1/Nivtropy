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

    /// <summary>
    /// Коэффициенты допусков нивелирования по ГКИНП 03-010-02
    /// Допуск невязки: коэффициент × √L, где L - длина хода в км
    /// </summary>
    public static class ToleranceCoefficients
    {
        /// <summary>
        /// Класс I: 4 мм · √L
        /// </summary>
        public const double ClassI = 0.004;

        /// <summary>
        /// Класс II: 8 мм · √L
        /// </summary>
        public const double ClassII = 0.008;

        /// <summary>
        /// Класс III: 10 мм · √L
        /// </summary>
        public const double ClassIII = 0.010;

        /// <summary>
        /// Класс IV: 20 мм · √L
        /// </summary>
        public const double ClassIV = 0.020;

        /// <summary>
        /// Техническое нивелирование: 50 мм · √L
        /// </summary>
        public const double Technical = 0.050;

        /// <summary>
        /// Двойной ход BF/FB: 4 мм · √n (n - число станций)
        /// </summary>
        public const double DoubleRun = 0.004;
    }

    /// <summary>
    /// Допуски разности плеч по классам нивелирования (в метрах)
    /// </summary>
    public static class ArmDifferenceLimits
    {
        /// <summary>
        /// Допуск разности плеч на станции (м)
        /// </summary>
        public static class PerStation
        {
            public const double ClassI = 0.5;
            public const double ClassII = 1.0;
            public const double ClassIII = 2.0;
            public const double ClassIV = 5.0;
            public const double Technical = 10.0;
        }

        /// <summary>
        /// Допуск накопления разности плеч за ход (м)
        /// </summary>
        public static class Accumulation
        {
            public const double ClassI = 1.0;
            public const double ClassII = 2.0;
            public const double ClassIII = 5.0;
            public const double ClassIV = 10.0;
            public const double Technical = 20.0;
        }
    }

    /// <summary>
    /// Константы для визуализации профилей
    /// </summary>
    public static class VisualizationDefaults
    {
        /// <summary>
        /// Отступ от края Canvas (px)
        /// </summary>
        public const double Margin = 50;

        /// <summary>
        /// Количество вертикальных линий сетки
        /// </summary>
        public const int VerticalGridLines = 10;

        /// <summary>
        /// Количество горизонтальных линий сетки
        /// </summary>
        public const int HorizontalGridLines = 8;

        /// <summary>
        /// Размер шрифта для подписей сетки
        /// </summary>
        public const double GridFontSize = 9;

        /// <summary>
        /// Толщина линии сетки
        /// </summary>
        public const double GridStrokeThickness = 0.5;

        /// <summary>
        /// Порог разницы плеч для жёлтого выделения (м)
        /// </summary>
        public const double ArmDiffWarningThreshold = 1.5;

        /// <summary>
        /// Порог разницы плеч для красного выделения (м)
        /// </summary>
        public const double ArmDiffErrorThreshold = 3.0;
    }
}
