using System;
using System.Collections.Generic;

namespace Nivtropy.Models
{
    /// <summary>
    /// Представляет известную отметку точки с привязкой к ходу и/или ориентации.
    /// </summary>
    public class KnownHeightEntry
    {
        public KnownHeightEntry(string pointCode, double height, string? lineName = null, int? lineIndex = null, string? orientationCode = null)
        {
            PointCode = pointCode.Trim();
            Height = height;
            LineName = lineName;
            LineIndex = lineIndex;
            OrientationCode = string.IsNullOrWhiteSpace(orientationCode) ? null : orientationCode.Trim();
        }

        /// <summary>
        /// Код точки.
        /// </summary>
        public string PointCode { get; }

        /// <summary>
        /// Известная высота (м).
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Название хода (для отображения).
        /// </summary>
        public string? LineName { get; }

        /// <summary>
        /// Индекс хода.
        /// </summary>
        public int? LineIndex { get; }

        /// <summary>
        /// Код ориентации/метода (например, BF/FB).
        /// </summary>
        public string? OrientationCode { get; }

        public bool HasScope => LineIndex.HasValue || !string.IsNullOrWhiteSpace(LineName) || !string.IsNullOrWhiteSpace(OrientationCode);

        /// <summary>
        /// Проверяет совпадение по ходу и ориентации.
        /// </summary>
        public bool MatchesScope(int? lineIndex, string? orientationCode)
        {
            if (LineIndex.HasValue && lineIndex.HasValue && LineIndex.Value != lineIndex.Value)
                return false;

            if (LineIndex.HasValue && !lineIndex.HasValue)
                return false; // точка привязана к ходу, но запрос без привязки

            if (!string.IsNullOrWhiteSpace(OrientationCode))
            {
                if (string.IsNullOrWhiteSpace(orientationCode))
                    return false;

                if (!string.Equals(OrientationCode, orientationCode, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public string ScopeDisplay
        {
            get
            {
                var parts = new List<string>();

                if (LineIndex.HasValue)
                {
                    parts.Add($"Ход {LineIndex.Value:D2}");
                }
                else if (!string.IsNullOrWhiteSpace(LineName))
                {
                    parts.Add(LineName);
                }

                if (!string.IsNullOrWhiteSpace(OrientationCode))
                {
                    parts.Add($"Ориентация {OrientationCode}");
                }

                return parts.Count == 0 ? "Без привязки" : string.Join(", ", parts);
            }
        }

        public string DisplayName => $"{PointCode}: {Height:F4} м ({ScopeDisplay})";
    }
}
