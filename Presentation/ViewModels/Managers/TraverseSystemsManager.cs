using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels.Managers
{
    /// <summary>
    /// Менеджер систем ходов
    /// Отвечает за группировку ходов в независимые системы с отдельными пространствами высот
    /// </summary>
    public class TraverseSystemsManager : ViewModelBase, ITraverseSystemsManager
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ObservableCollection<TraverseSystem> _systems = new();
        private TraverseSystem? _selectedSystem;

        public const string DEFAULT_SYSTEM_ID = "system-default";
        public const string DEFAULT_SYSTEM_NAME = "Основная";

        public TraverseSystemsManager(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel ?? throw new ArgumentNullException(nameof(dataViewModel));

            // Инициализация системы по умолчанию
            var defaultSystem = new TraverseSystem(DEFAULT_SYSTEM_ID, DEFAULT_SYSTEM_NAME, order: 0);
            _systems.Add(defaultSystem);
            _selectedSystem = defaultSystem;
        }

        public event EventHandler? SystemsChanged;
        public event EventHandler? SelectedSystemChanged;

        public ObservableCollection<TraverseSystem> Systems => _systems;

        public TraverseSystem? SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (_selectedSystem != value)
                {
                    _selectedSystem = value;
                    OnPropertyChanged();
                    OnSelectedSystemChanged();
                }
            }
        }

        public TraverseSystem? DefaultSystem =>
            _systems.FirstOrDefault(s => s.Id == DEFAULT_SYSTEM_ID);

        /// <summary>
        /// Создает новую систему ходов
        /// </summary>
        public TraverseSystem CreateSystem(string name)
        {
            var id = Guid.NewGuid().ToString();
            var order = _systems.Count > 0 ? _systems.Max(s => s.Order) + 1 : 0;
            var system = new TraverseSystem(id, name, order);
            _systems.Add(system);
            OnSystemsChanged();
            return system;
        }

        /// <summary>
        /// Удаляет систему ходов (кроме системы по умолчанию)
        /// </summary>
        public bool DeleteSystem(TraverseSystem system)
        {
            if (system == null || system.Id == DEFAULT_SYSTEM_ID)
                return false;

            // Перемещаем все ходы из удаляемой системы в систему по умолчанию
            var defaultSystem = DefaultSystem;
            if (defaultSystem != null)
            {
                foreach (var runIndex in system.RunIndexes.ToList())
                {
                    var run = _dataViewModel.Runs.FirstOrDefault(r => r.Index == runIndex);
                    if (run != null)
                    {
                        run.SystemId = DEFAULT_SYSTEM_ID;
                        defaultSystem.AddRun(runIndex);
                    }
                }
            }

            _systems.Remove(system);

            if (_selectedSystem == system)
            {
                SelectedSystem = defaultSystem;
            }

            OnSystemsChanged();
            return true;
        }

        /// <summary>
        /// Переименовывает систему
        /// </summary>
        public void RenameSystem(TraverseSystem system, string newName)
        {
            if (system == null || string.IsNullOrWhiteSpace(newName))
                return;

            system.Name = newName;
            OnSystemsChanged();
        }

        /// <summary>
        /// Перемещает ход в указанную систему
        /// </summary>
        public void MoveRunToSystem(LineSummary run, TraverseSystem targetSystem)
        {
            if (run == null || targetSystem == null)
                return;

            // Удаляем из текущей системы
            var currentSystem = _systems.FirstOrDefault(s => s.Id == run.SystemId);
            currentSystem?.RemoveRun(run.Index);

            // Добавляем в новую систему
            run.SystemId = targetSystem.Id;
            targetSystem.AddRun(run.Index);

            OnSystemsChanged();
        }

        /// <summary>
        /// Инициализирует системы для новых ходов
        /// </summary>
        public void InitializeRunSystems()
        {
            var defaultSystem = DefaultSystem;
            if (defaultSystem == null)
                return;

            foreach (var run in _dataViewModel.Runs)
            {
                // Если ход еще не привязан к системе, привязываем к системе по умолчанию
                if (string.IsNullOrEmpty(run.SystemId))
                {
                    run.SystemId = DEFAULT_SYSTEM_ID;
                }

                // Добавляем ход в RunIndexes соответствующей системы
                var system = _systems.FirstOrDefault(s => s.Id == run.SystemId);
                if (system != null && !system.ContainsRun(run.Index))
                {
                    system.AddRun(run.Index);
                }
            }
        }

        /// <summary>
        /// Получает систему по ID
        /// </summary>
        public TraverseSystem? GetSystem(string? systemId)
        {
            if (string.IsNullOrEmpty(systemId))
                return DefaultSystem;

            return _systems.FirstOrDefault(s => s.Id == systemId);
        }

        /// <summary>
        /// Получает ходы для указанной системы
        /// </summary>
        public IEnumerable<LineSummary> GetSystemRuns(TraverseSystem? system)
        {
            if (system == null)
                return Enumerable.Empty<LineSummary>();

            return _dataViewModel.Runs.Where(r => r.SystemId == system.Id);
        }

        /// <summary>
        /// Получает активные ходы для указанной системы
        /// </summary>
        public IEnumerable<LineSummary> GetActiveSystemRuns(TraverseSystem? system)
        {
            return GetSystemRuns(system).Where(r => r.IsActive);
        }

        /// <summary>
        /// Проверяет, принадлежит ли ход системе
        /// </summary>
        public bool RunBelongsToSystem(LineSummary run, TraverseSystem system)
        {
            if (run == null || system == null)
                return false;

            return run.SystemId == system.Id;
        }

        private void OnSystemsChanged()
            => SystemsChanged?.Invoke(this, EventArgs.Empty);

        private void OnSelectedSystemChanged()
            => SelectedSystemChanged?.Invoke(this, EventArgs.Empty);
    }
}
