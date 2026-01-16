using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nivtropy.Presentation.Models
{
    /// <summary>
    /// UI-модель системы ходов - логическая группа связанных нивелирных ходов
    /// с независимой областью высот.
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

        public string Id { get; }

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

        public List<int> RunIndexes { get; }

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

        public int RunCount => RunIndexes.Count;
        public string DisplayName => $"{Name} ({RunCount})";

        public void AddRun(int runIndex)
        {
            if (!RunIndexes.Contains(runIndex))
            {
                RunIndexes.Add(runIndex);
                OnPropertyChanged(nameof(RunCount));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public void RemoveRun(int runIndex)
        {
            if (RunIndexes.Remove(runIndex))
            {
                OnPropertyChanged(nameof(RunCount));
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public bool ContainsRun(int runIndex) => RunIndexes.Contains(runIndex);

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
