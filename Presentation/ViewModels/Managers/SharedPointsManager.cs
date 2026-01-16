using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Nivtropy.Presentation.Models;
using InputModels = Nivtropy.Models;
using Nivtropy.Utilities;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels.Managers
{
    /// <summary>
    /// Менеджер общих точек между ходами
    /// Отвечает за отслеживание и управление точками, используемыми в нескольких ходах
    /// </summary>
    public class SharedPointsManager : ViewModelBase, ISharedPointsManager
    {
        private readonly DataViewModel _dataViewModel;
        private readonly ObservableCollection<SharedPointLinkItem> _sharedPoints = new();
        private readonly Dictionary<string, SharedPointLinkItem> _sharedPointLookup = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<int, List<string>> _sharedPointsByRun = new();

        public SharedPointsManager(DataViewModel dataViewModel)
        {
            _dataViewModel = dataViewModel ?? throw new ArgumentNullException(nameof(dataViewModel));
        }

        public event EventHandler? SharedPointsChanged;

        public ObservableCollection<SharedPointLinkItem> SharedPoints => _sharedPoints;
        public IReadOnlyDictionary<int, List<string>> SharedPointsByRun => _sharedPointsByRun;

        /// <summary>
        /// Обновляет метаданные общих точек на основе записей измерений
        /// </summary>
        public void UpdateSharedPointsMetadata(IReadOnlyCollection<InputModels.MeasurementRecord> records)
        {
            var usage = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            void AddUsage(string? code, int runIndex)
            {
                if (runIndex == 0 || string.IsNullOrWhiteSpace(code))
                    return;

                var trimmed = code.Trim();
                if (!usage.TryGetValue(trimmed, out var set))
                {
                    set = new HashSet<int>();
                    usage[trimmed] = set;
                }

                set.Add(runIndex);
            }

            foreach (var record in records)
            {
                if (!string.IsNullOrWhiteSpace(record.LineMarker))
                    continue;

                var runIndex = record.LineSummary?.Index ?? 0;
                AddUsage(record.Target, runIndex);
                AddUsage(record.StationCode, runIndex);
            }

            var sharedCodes = usage
                .Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _sharedPointsByRun = sharedCodes
                .SelectMany(kvp => kvp.Value.Select(run => (run, kvp.Key)))
                .GroupBy(x => x.run)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Key)
                        .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                        .ToList());

            // Удаляем точки, которые больше не являются общими
            var codesToRemove = _sharedPointLookup.Keys.Where(code => !sharedCodes.ContainsKey(code)).ToList();
            foreach (var code in codesToRemove)
            {
                if (_sharedPointLookup.TryGetValue(code, out var item))
                {
                    _sharedPoints.Remove(item);
                    _sharedPointLookup.Remove(code);
                }
            }

            // Добавляем или обновляем общие точки
            foreach (var kvp in sharedCodes)
            {
                if (!_sharedPointLookup.TryGetValue(kvp.Key, out var item))
                {
                    var enabled = _dataViewModel.IsSharedPointEnabled(kvp.Key);
                    item = new SharedPointLinkItem(kvp.Key, enabled, (code, state) => _dataViewModel.SetSharedPointEnabled(code, state));
                    _sharedPointLookup[kvp.Key] = item;
                    _sharedPoints.Add(item);
                }

                item.SetRunIndexes(kvp.Value);
            }

            // Сортируем точки
            var ordered = _sharedPoints
                .OrderBy(p => PointCodeHelper.GetSortKey(p.Code))
                .ToList();

            _sharedPoints.Clear();
            foreach (var item in ordered)
            {
                _sharedPoints.Add(item);
            }

            OnSharedPointsChanged();
        }

        /// <summary>
        /// Получает общие точки для указанного хода
        /// </summary>
        public List<SharedPointLinkItem> GetSharedPointsForRun(LineSummary? run)
        {
            if (run == null)
                return new List<SharedPointLinkItem>();

            return _sharedPoints
                .Where(p => p.IsUsedInRun(run.Index))
                .OrderBy(p => PointCodeHelper.GetSortKey(p.Code))
                .ToList();
        }

        /// <summary>
        /// Получает коды общих точек для указанного индекса хода
        /// </summary>
        public List<string> GetSharedPointCodesForRun(int runIndex)
        {
            return _sharedPointsByRun.TryGetValue(runIndex, out var codes)
                ? codes
                : new List<string>();
        }

        /// <summary>
        /// Проверяет, является ли точка общей
        /// </summary>
        public bool IsSharedPoint(string? pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return false;

            return _sharedPointLookup.ContainsKey(pointCode);
        }

        /// <summary>
        /// Получает элемент общей точки по коду
        /// </summary>
        public SharedPointLinkItem? GetSharedPoint(string? pointCode)
        {
            if (string.IsNullOrWhiteSpace(pointCode))
                return null;

            return _sharedPointLookup.TryGetValue(pointCode, out var item) ? item : null;
        }

        private void OnSharedPointsChanged()
            => SharedPointsChanged?.Invoke(this, EventArgs.Empty);
    }
}
