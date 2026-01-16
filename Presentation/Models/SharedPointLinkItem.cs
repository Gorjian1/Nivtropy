using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Nivtropy.Presentation.Models
{
    /// <summary>
    /// UI-модель для общей точки между ходами с возможностью включать/выключать обмен высотами.
    /// </summary>
    public class SharedPointLinkItem : INotifyPropertyChanged
    {
        private readonly Action<string, bool> _onToggle;
        private readonly HashSet<int> _runIndexes = new();
        private bool _isEnabled;

        public SharedPointLinkItem(string code, bool isEnabled, Action<string, bool> onToggle)
        {
            Code = code;
            _isEnabled = isEnabled;
            _onToggle = onToggle;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Code { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    _onToggle?.Invoke(Code, _isEnabled);
                }
            }
        }

        public string RunsDisplay => _runIndexes.Count > 0
            ? string.Join(", ", _runIndexes.OrderBy(i => i).Select(i => $"Ход {i:D2}"))
            : "—";

        public IReadOnlyCollection<int> RunIndexes => _runIndexes;

        public bool IsUsedInRun(int runIndex) => _runIndexes.Contains(runIndex);

        public void SetRunIndexes(IEnumerable<int> indexes)
        {
            _runIndexes.Clear();
            foreach (var index in indexes)
                _runIndexes.Add(index);
            OnPropertyChanged(nameof(RunsDisplay));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
