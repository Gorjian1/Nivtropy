using System.Collections.Generic;

namespace Nivtropy.Presentation.ViewModels.Helpers
{
    /// <summary>
    /// Отслеживает историю высот точек внутри хода.
    /// Используется для обработки повторных визитов одной точки.
    /// </summary>
    internal sealed class RunHeightTracker
    {
        private readonly Dictionary<string, List<double>> _runRawHeights;
        private readonly Dictionary<string, double> _rawHeights;
        private readonly Dictionary<string, double> _availableRawHeights;

        public RunHeightTracker(
            Dictionary<string, double> rawHeights,
            Dictionary<string, double> availableRawHeights)
        {
            _runRawHeights = new Dictionary<string, List<double>>(System.StringComparer.OrdinalIgnoreCase);
            _rawHeights = rawHeights;
            _availableRawHeights = availableRawHeights;
        }

        /// <summary>
        /// Получает текущую высоту для alias'а (из истории хода или из raw)
        /// </summary>
        public double? GetHeight(string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return null;

            if (_runRawHeights.TryGetValue(alias!, out var history) && history.Count > 0)
            {
                return history[^1];
            }

            return _rawHeights.TryGetValue(alias!, out var value) ? value : null;
        }

        /// <summary>
        /// Проверяет, есть ли история для alias'а в текущем ходе
        /// </summary>
        public bool HasHistory(string? alias)
        {
            return !string.IsNullOrWhiteSpace(alias)
                && _runRawHeights.TryGetValue(alias!, out var history)
                && history.Count > 0;
        }

        /// <summary>
        /// Записывает высоту для alias'а
        /// </summary>
        public void RecordHeight(string? alias, double value)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return;

            if (!_runRawHeights.TryGetValue(alias!, out var history))
            {
                history = new List<double>();
                _runRawHeights[alias!] = history;
            }

            history.Add(value);

            if (!_rawHeights.ContainsKey(alias!) && !_availableRawHeights.ContainsKey(alias!))
            {
                _rawHeights[alias!] = value;
            }
        }
    }
}
