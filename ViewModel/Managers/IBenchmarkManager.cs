using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Nivtropy.Presentation.Models;

namespace Nivtropy.ViewModels.Managers
{
    /// <summary>
    /// Интерфейс менеджера реперов (точек с известными высотами)
    /// </summary>
    public interface IBenchmarkManager
    {
        /// <summary>
        /// Событие при изменении списка реперов
        /// </summary>
        event EventHandler? BenchmarksChanged;

        /// <summary>
        /// Коллекция реперов
        /// </summary>
        ObservableCollection<BenchmarkItem> Benchmarks { get; }

        /// <summary>
        /// Словарь привязки реперов к системам
        /// </summary>
        IReadOnlyDictionary<string, string> BenchmarkSystems { get; }

        /// <summary>
        /// Выбранная точка для добавления
        /// </summary>
        PointItem? SelectedPoint { get; set; }

        /// <summary>
        /// Высота нового репера (строковое значение)
        /// </summary>
        string? NewBenchmarkHeight { get; set; }

        /// <summary>
        /// ID выбранной системы для фильтрации
        /// </summary>
        string? SelectedSystemId { get; set; }

        /// <summary>
        /// Можно ли добавить репер
        /// </summary>
        bool CanAddBenchmark { get; }

        /// <summary>
        /// Команда добавления репера
        /// </summary>
        ICommand AddBenchmarkCommand { get; }

        /// <summary>
        /// Команда удаления репера
        /// </summary>
        ICommand RemoveBenchmarkCommand { get; }

        /// <summary>
        /// Добавляет новый репер
        /// </summary>
        void AddBenchmark();

        /// <summary>
        /// Удаляет репер
        /// </summary>
        void RemoveBenchmark(BenchmarkItem? benchmark);

        /// <summary>
        /// Обновляет список реперов из DataViewModel
        /// </summary>
        void UpdateBenchmarks();

        /// <summary>
        /// Получает систему для указанного репера
        /// </summary>
        string GetSystemForBenchmark(string pointCode);

        /// <summary>
        /// Проверяет, принадлежит ли репер указанной системе
        /// </summary>
        bool BelongsToSystem(string pointCode, string systemId);

        /// <summary>
        /// Получает реперы для указанной системы
        /// </summary>
        IEnumerable<KeyValuePair<string, double>> GetBenchmarksForSystem(string systemId);
    }
}
