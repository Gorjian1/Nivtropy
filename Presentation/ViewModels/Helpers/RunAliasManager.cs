using System;
using System.Collections.Generic;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Presentation.ViewModels.Helpers
{
    /// <summary>
    /// Управляет alias'ами точек для обработки повторяющихся кодов внутри хода.
    /// Используется в расчёте высот для различения визитов одной и той же точки.
    /// </summary>
    internal sealed class RunAliasManager
    {
        private readonly Dictionary<(TraverseRow row, bool isBack), string> _aliasByRowSide;
        private readonly Dictionary<string, string> _aliasToOriginal;
        private readonly Dictionary<string, int> _occurrenceCount;
        private readonly Func<string, bool> _isAnchor;

        private string? _previousForeCode;
        private string? _previousForeAlias;

        public RunAliasManager(Func<string, bool> isAnchor)
        {
            _aliasByRowSide = new Dictionary<(TraverseRow row, bool isBack), string>(new AliasKeyComparer());
            _aliasToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _occurrenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _isAnchor = isAnchor;
        }

        /// <summary>
        /// Регистрирует alias для кода точки с учётом повторных визитов
        /// </summary>
        public string RegisterAlias(string code, bool reusePrevious)
        {
            if (_isAnchor(code))
            {
                _aliasToOriginal[code] = code;
                return code;
            }

            if (reusePrevious && _previousForeAlias != null &&
                string.Equals(_previousForeCode, code, StringComparison.OrdinalIgnoreCase))
            {
                _aliasToOriginal[_previousForeAlias] = code;
                return _previousForeAlias;
            }

            var next = _occurrenceCount.TryGetValue(code, out var count) ? count + 1 : 1;
            _occurrenceCount[code] = next;

            var alias = next == 1 ? code : $"{code} ({next})";
            _aliasToOriginal[alias] = code;
            return alias;
        }

        /// <summary>
        /// Регистрирует alias для строки (Back или Fore)
        /// </summary>
        public void RegisterRowAlias(TraverseRow row, bool isBack, string alias)
        {
            _aliasByRowSide[(row, isBack)] = alias;

            if (!isBack)
            {
                var code = row.ForeCode;
                _previousForeCode = code;
                _previousForeAlias = alias;
            }
        }

        /// <summary>
        /// Сбрасывает предыдущий Fore-код (когда ForeCode пустой)
        /// </summary>
        public void ResetPreviousFore()
        {
            _previousForeCode = null;
            _previousForeAlias = null;
        }

        /// <summary>
        /// Получает alias для строки
        /// </summary>
        public string? GetAlias(TraverseRow row, bool isBack)
        {
            return _aliasByRowSide.TryGetValue((row, isBack), out var value) ? value : null;
        }

        /// <summary>
        /// Проверяет, является ли alias копией (не оригиналом)
        /// </summary>
        public bool IsCopyAlias(string alias)
        {
            return _aliasToOriginal.TryGetValue(alias, out var original)
                && !string.Equals(alias, original, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Компаратор для ключей alias'ов
        /// </summary>
        private sealed class AliasKeyComparer : IEqualityComparer<(TraverseRow row, bool isBack)>
        {
            public bool Equals((TraverseRow row, bool isBack) x, (TraverseRow row, bool isBack) y)
            {
                return ReferenceEquals(x.row, y.row) && x.isBack == y.isBack;
            }

            public int GetHashCode((TraverseRow row, bool isBack) obj)
            {
                return HashCode.Combine(obj.row, obj.isBack);
            }
        }
    }
}
