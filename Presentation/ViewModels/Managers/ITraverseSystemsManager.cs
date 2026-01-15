using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Presentation.ViewModels.Managers
{
    /// <summary>
    /// Интерфейс менеджера систем ходов
    /// </summary>
    public interface ITraverseSystemsManager
    {
        /// <summary>
        /// ID системы по умолчанию
        /// </summary>
        const string DEFAULT_SYSTEM_ID = "system-default";

        /// <summary>
        /// Название системы по умолчанию
        /// </summary>
        const string DEFAULT_SYSTEM_NAME = "Основная";

        /// <summary>
        /// Событие при изменении систем
        /// </summary>
        event EventHandler? SystemsChanged;

        /// <summary>
        /// Событие при изменении выбранной системы
        /// </summary>
        event EventHandler? SelectedSystemChanged;

        /// <summary>
        /// Коллекция систем
        /// </summary>
        ObservableCollection<TraverseSystem> Systems { get; }

        /// <summary>
        /// Выбранная система
        /// </summary>
        TraverseSystem? SelectedSystem { get; set; }

        /// <summary>
        /// Система по умолчанию
        /// </summary>
        TraverseSystem? DefaultSystem { get; }

        /// <summary>
        /// Создает новую систему ходов
        /// </summary>
        TraverseSystem CreateSystem(string name);

        /// <summary>
        /// Удаляет систему ходов (кроме системы по умолчанию)
        /// </summary>
        bool DeleteSystem(TraverseSystem system);

        /// <summary>
        /// Переименовывает систему
        /// </summary>
        void RenameSystem(TraverseSystem system, string newName);

        /// <summary>
        /// Перемещает ход в указанную систему
        /// </summary>
        void MoveRunToSystem(LineSummary run, TraverseSystem targetSystem);

        /// <summary>
        /// Инициализирует системы для новых ходов
        /// </summary>
        void InitializeRunSystems();

        /// <summary>
        /// Получает систему по ID
        /// </summary>
        TraverseSystem? GetSystem(string? systemId);

        /// <summary>
        /// Получает ходы для указанной системы
        /// </summary>
        IEnumerable<LineSummary> GetSystemRuns(TraverseSystem? system);

        /// <summary>
        /// Получает активные ходы для указанной системы
        /// </summary>
        IEnumerable<LineSummary> GetActiveSystemRuns(TraverseSystem? system);

        /// <summary>
        /// Проверяет, принадлежит ли ход системе
        /// </summary>
        bool RunBelongsToSystem(LineSummary run, TraverseSystem system);
    }
}
