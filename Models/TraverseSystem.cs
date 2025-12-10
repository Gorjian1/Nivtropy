using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Nivtropy.Models
{
    /// <summary>
    /// Система ходов - логическая группа связанных нивелирных ходов
    /// с независимой областью высот
    /// </summary>
    public class TraverseSystem : INotifyPropertyChanged
    {
        private string _name;
        private int _order;

        public TraverseSystem(string id, string name, int order = 0)
        {
            Id = id;
            _name = name;
            _order = order;
            RunIndexes = new List<int>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Уникальный идентификатор системы (GUID)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Название системы (например, "Основная", "Контроль")
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Индексы ходов, входящих в систему
        /// </summary>
        public List<int> RunIndexes { get; }

        /// <summary>
        /// Порядок отображения системы
        /// </summary>
        public int Order
        {
            get => _order;
            set
            {
                if (_order != value)
                {
                    _order = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Количество ходов в системе
        /// </summary>
        public int RunCount => RunIndexes.Count;

        /// <summary>
        /// Отображаемое имя с количеством ходов
        /// </summary>
        public string DisplayName => $"{Name} ({RunCount})";

        /// <summary>
        /// Добавить ход в систему
        /// </summary>
        public void AddRun(int runIndex)
        {
            if (!RunIndexes.Contains(runIndex))
            {
                RunIndexes.Add(runIndex);
                OnPropertyChanged(nameof(RunCount));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        /// <summary>
        /// Удалить ход из системы
        /// </summary>
        public void RemoveRun(int runIndex)
        {
            if (RunIndexes.Remove(runIndex))
            {
                OnPropertyChanged(nameof(RunCount));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        /// <summary>
        /// Проверить, содержит ли система указанный ход
        /// </summary>
        public bool ContainsRun(int runIndex) => RunIndexes.Contains(runIndex);

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
