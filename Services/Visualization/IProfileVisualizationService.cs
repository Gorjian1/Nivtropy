using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nivtropy.Domain.DTOs;
using Nivtropy.Application.DTOs;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Services.Visualization
{
    /// <summary>
    /// Интерфейс сервиса для визуализации профилей нивелирного хода
    /// </summary>
    public interface IProfileVisualizationService
    {
        /// <summary>
        /// Рисует профиль хода на Canvas
        /// </summary>
        /// <param name="canvas">Canvas для рисования</param>
        /// <param name="rows">Строки хода</param>
        /// <param name="options">Опции отображения</param>
        /// <param name="statistics">Статистика профиля (для отображения аномалий)</param>
        /// <param name="knownHeightPoints">Точки с известной высотой</param>
        void DrawProfile(
            Canvas canvas,
            List<TraverseRow> rows,
            ProfileVisualizationOptions options,
            ProfileStatistics? statistics,
            HashSet<string> knownHeightPoints);
    }

    /// <summary>
    /// Опции визуализации профиля
    /// </summary>
    public class ProfileVisualizationOptions
    {
        public bool ShowZ { get; set; } = true;
        public bool ShowZ0 { get; set; } = true;
        public bool ShowAnomalies { get; set; } = true;
        public Color ProfileColor { get; set; } = Color.FromRgb(0x19, 0x76, 0xD2);
        public Color ProfileZ0Color { get; set; } = Color.FromRgb(0x80, 0x80, 0x80);
        public double? ManualMinHeight { get; set; }
        public double? ManualMaxHeight { get; set; }
        public double SensitivitySigma { get; set; } = 2.5;
    }
}
