using System.Globalization;

namespace Nivtropy.Utilities
{
    /// <summary>
    /// Утилита для работы с кодами точек
    /// </summary>
    public static class PointCodeHelper
    {
        /// <summary>
        /// Парсит код точки и определяет, является ли он числовым
        /// </summary>
        /// <param name="code">Код точки</param>
        /// <returns>Кортеж (isNumeric, number) - является ли числовым и его значение</returns>
        public static (bool isNumeric, double number) Parse(string code)
        {
            if (double.TryParse(code, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return (true, value);
            }

            return (false, double.NaN);
        }
    }
}
