using System.Collections.Concurrent;
using System.Globalization;

namespace Nivtropy.Utilities
{
    /// <summary>
    /// Утилита для работы с кодами точек с кешированием результатов парсинга
    /// </summary>
    public static class PointCodeHelper
    {
        private static readonly ConcurrentDictionary<string, (bool isNumeric, double number)> _cache = new();

        /// <summary>
        /// Парсит код точки и определяет, является ли он числовым.
        /// Результаты кешируются для повторного использования.
        /// </summary>
        /// <param name="code">Код точки</param>
        /// <returns>Кортеж (isNumeric, number) - является ли числовым и его значение</returns>
        public static (bool isNumeric, double number) Parse(string code)
        {
            if (string.IsNullOrEmpty(code))
                return (false, double.NaN);

            return _cache.GetOrAdd(code, ParseInternal);
        }

        /// <summary>
        /// Ключ сортировки для упорядочивания точек: сначала числовые по возрастанию, затем текстовые
        /// </summary>
        public static (int priority, double number, string code) GetSortKey(string code)
        {
            var (isNumeric, number) = Parse(code);
            return (isNumeric ? 0 : 1, number, code ?? "");
        }

        /// <summary>
        /// Очищает кеш (например, при загрузке нового проекта)
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static (bool isNumeric, double number) ParseInternal(string code)
        {
            if (double.TryParse(code, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return (true, value);
            }

            return (false, double.NaN);
        }
    }
}
