using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Построитель структуры хода из записей измерений
    /// Реализует интерфейс ITraverseBuilder для соблюдения принципа инверсии зависимостей (DIP)
    /// Включает кэширование для оптимизации производительности
    /// </summary>
    public class TraverseBuilder : ITraverseBuilder
    {
        private readonly Dictionary<int, List<TraverseRow>> _cache = new();
        private readonly object _cacheLock = new();

        /// <summary>
        /// Очищает кэш результатов. Вызывайте при изменении исходных данных.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        // Спаривание с учётом режима: BF (Back→Forward) или FB (Forward→Back)
        // Режим определяет порядок точек в паре
        public List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null)
        {
            // Материализуем записи для кэширования и подсчёта хэша
            var recordsList = records as IList<MeasurementRecord> ?? records.ToList();

            // Вычисляем хэш для кэширования
            var hash = ComputeHash(recordsList, run);

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(hash, out var cached))
                {
                    return cached;
                }
            }

            // Строим результат
            var result = BuildInternal(recordsList, run);

            lock (_cacheLock)
            {
                _cache[hash] = result;
            }

            return result;
        }

        /// <summary>
        /// Вычисляет хэш для набора записей и хода.
        /// Использует больше данных для предотвращения коллизий на больших файлах.
        /// </summary>
        private int ComputeHash(IList<MeasurementRecord> records, LineSummary? run)
        {
            unchecked
            {
                int hash = 17;

                // Хэш от хода
                if (run != null)
                {
                    hash = hash * 31 + (run.DisplayName?.GetHashCode() ?? 0);
                    hash = hash * 31 + run.RecordCount;
                    hash = hash * 31 + (run.StartTarget?.GetHashCode() ?? 0);
                    hash = hash * 31 + (run.EndTarget?.GetHashCode() ?? 0);
                    hash = hash * 31 + run.DeltaHSum.GetHashCode();
                }

                // Хэш от количества записей
                hash = hash * 31 + records.Count;

                // Хэш от всех записей (или выборки для очень больших файлов)
                // Для файлов до 100 записей - все записи
                // Для больших - каждая 10-я запись + первая/последняя
                int step = records.Count <= 100 ? 1 : Math.Max(1, records.Count / 10);

                for (int i = 0; i < records.Count; i += step)
                {
                    var rec = records[i];
                    hash = hash * 31 + i; // Позиция записи
                    hash = hash * 31 + (rec.StationCode?.GetHashCode() ?? 0);
                    hash = hash * 31 + (rec.Target?.GetHashCode() ?? 0);
                    hash = hash * 31 + rec.Rb_m.GetHashCode();
                    hash = hash * 31 + rec.Rf_m.GetHashCode();
                    hash = hash * 31 + rec.Seq.GetHashCode();
                }

                // Всегда включаем последнюю запись
                if (records.Count > 1)
                {
                    var last = records[records.Count - 1];
                    hash = hash * 31 + (records.Count - 1);
                    hash = hash * 31 + (last.StationCode?.GetHashCode() ?? 0);
                    hash = hash * 31 + (last.Target?.GetHashCode() ?? 0);
                    hash = hash * 31 + last.Rb_m.GetHashCode();
                    hash = hash * 31 + last.Rf_m.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Внутренняя реализация построения хода
        /// </summary>
        private List<TraverseRow> BuildInternal(IList<MeasurementRecord> records, LineSummary? run)
        {
            var list = new List<TraverseRow>();
            string line = run?.DisplayName ?? "?";
            string mode = "BF"; // По умолчанию BF (Back→Forward)
            TraverseRow? pending = null;
            int idx = 1;
            LineSummary? currentLineSummary = run;
            bool isFirstPointOfLine = false; // Флаг для отслеживания первой точки нового хода
            string? firstPointCode = null; // Код первой точки хода

            foreach (var r in records)
            {
                // Обновляем режим при смене линии или явном указании режима
                if (r.LineSummary != null && !ReferenceEquals(r.LineSummary, run) && r.LineSummary.DisplayName != line)
                {
                    if (pending != null)
                    {
                        list.Add(pending);
                        pending = null;
                    }

                    line = r.LineSummary.DisplayName;
                    currentLineSummary = r.LineSummary;
                    idx = 1;
                    isFirstPointOfLine = true; // Начинается новый ход
                    firstPointCode = null;
                }

                // Обновляем currentLineSummary если она еще null
                if (currentLineSummary == null && r.LineSummary != null)
                {
                    currentLineSummary = r.LineSummary;
                }

                // Определяем режим из маркера Start-Line
                if (r.LineMarker == "Start-Line" && !string.IsNullOrWhiteSpace(r.Mode))
                {
                    // Извлекаем BF или FB из Mode (например, "BF" или "FB")
                    var modeUpper = r.Mode.Trim().ToUpperInvariant();
                    if (modeUpper == "BF" || modeUpper == "FB")
                        mode = modeUpper;

                    isFirstPointOfLine = true; // Start-Line также начинает новый ход
                }

                bool isBF = mode == "BF";

                if (r.Rb_m.HasValue)
                {
                    // Если это первая точка нового хода, создаём виртуальную станцию
                    if (isFirstPointOfLine && firstPointCode == null)
                    {
                        firstPointCode = r.StationCode;

                        // Создаём виртуальную станцию для первой точки хода
                        // Эта станция представляет репер, от которого начинается ход
                        var virtualStation = new TraverseRow
                        {
                            LineName = line,
                            Index = idx++,
                            BackCode = r.StationCode,
                            LineSummary = currentLineSummary
                        };
                        list.Add(virtualStation);

                        isFirstPointOfLine = false;
                    }

                    if (pending == null)
                    {
                        // В режиме BF: Rb это задняя точка (Back)
                        // В режиме FB: Rb это передняя точка (Fore)
                        if (isBF)
                            pending = new TraverseRow { LineName = line, Index = idx++, BackCode = r.StationCode, Rb_m = r.Rb_m, HdBack_m = r.HD_m, LineSummary = currentLineSummary };
                        else
                            pending = new TraverseRow { LineName = line, Index = idx++, ForeCode = r.StationCode, Rb_m = r.Rb_m, HdFore_m = r.HD_m, LineSummary = currentLineSummary };
                    }
                    else
                    {
                        if (isBF)
                        {
                            pending.Rb_m ??= r.Rb_m;
                            pending.HdBack_m ??= r.HD_m;
                            pending.BackCode ??= r.StationCode;
                        }
                        else
                        {
                            pending.Rb_m ??= r.Rb_m;
                            pending.HdFore_m ??= r.HD_m;
                            pending.ForeCode ??= r.StationCode;
                        }
                        list.Add(pending);
                        pending = null;
                    }
                    continue;
                }

                if (r.Rf_m.HasValue)
                {
                    if (pending == null)
                    {
                        // В режиме BF: Rf это передняя точка (Fore)
                        // В режиме FB: Rf это задняя точка (Back)
                        if (isBF)
                            pending = new TraverseRow { LineName = line, Index = idx++, ForeCode = r.StationCode, Rf_m = r.Rf_m, HdFore_m = r.HD_m, LineSummary = currentLineSummary };
                        else
                            pending = new TraverseRow { LineName = line, Index = idx++, BackCode = r.StationCode, Rf_m = r.Rf_m, HdBack_m = r.HD_m, LineSummary = currentLineSummary };
                    }
                    else
                    {
                        if (isBF)
                        {
                            pending.Rf_m ??= r.Rf_m;
                            pending.HdFore_m ??= r.HD_m;
                            pending.ForeCode ??= r.StationCode;
                        }
                        else
                        {
                            pending.Rf_m ??= r.Rf_m;
                            pending.HdBack_m ??= r.HD_m;
                            pending.BackCode ??= r.StationCode;
                        }
                        list.Add(pending);
                        pending = null;
                    }
                }
            }
            if (pending != null) list.Add(pending);
            return list;
        }

    }
}
