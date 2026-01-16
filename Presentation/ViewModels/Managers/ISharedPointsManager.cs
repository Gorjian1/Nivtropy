using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nivtropy.Models;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Presentation.ViewModels.Managers
{
    /// <summary>
    /// Интерфейс менеджера общих точек между ходами
    /// </summary>
    public interface ISharedPointsManager
    {
        /// <summary>
        /// Событие при изменении общих точек
        /// </summary>
        event EventHandler? SharedPointsChanged;

        /// <summary>
        /// Коллекция общих точек
        /// </summary>
        ObservableCollection<SharedPointLinkItem> SharedPoints { get; }

        /// <summary>
        /// Словарь общих точек по индексам ходов
        /// </summary>
        IReadOnlyDictionary<int, List<string>> SharedPointsByRun { get; }

        /// <summary>
        /// Обновляет метаданные общих точек на основе записей измерений
        /// </summary>
        void UpdateSharedPointsMetadata(IReadOnlyCollection<MeasurementRecord> records);

        /// <summary>
        /// Получает общие точки для указанного хода
        /// </summary>
        List<SharedPointLinkItem> GetSharedPointsForRun(LineSummary? run);

        /// <summary>
        /// Получает коды общих точек для указанного индекса хода
        /// </summary>
        List<string> GetSharedPointCodesForRun(int runIndex);

        /// <summary>
        /// Проверяет, является ли точка общей
        /// </summary>
        bool IsSharedPoint(string? pointCode);

        /// <summary>
        /// Получает элемент общей точки по коду
        /// </summary>
        SharedPointLinkItem? GetSharedPoint(string? pointCode);
    }
}
