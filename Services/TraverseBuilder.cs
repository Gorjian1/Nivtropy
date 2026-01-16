using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Построитель структуры хода из записей измерений.
    /// Legacy сервис - будет заменён на Domain сервисы.
    /// </summary>
    public class TraverseBuilder : ITraverseBuilder
    {
        private readonly Dictionary<int, List<TraverseRow>> _cache = new();
        private readonly object _cacheLock = new();

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        public List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null)
        {
            var recordsList = records as IList<MeasurementRecord> ?? records.ToList();
            var hash = ComputeHash(recordsList, run);

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(hash, out var cached))
                    return cached;
            }

            var result = BuildInternal(recordsList, run);

            lock (_cacheLock)
            {
                _cache[hash] = result;
            }

            return result;
        }

        private int ComputeHash(IList<MeasurementRecord> records, LineSummary? run)
        {
            unchecked
            {
                int hash = 17;
                if (run != null)
                {
                    hash = hash * 31 + (run.DisplayName?.GetHashCode() ?? 0);
                    hash = hash * 31 + run.RecordCount;
                    hash = hash * 31 + run.DeltaHSum.GetHashCode();
                }
                hash = hash * 31 + records.Count;

                int step = records.Count <= 100 ? 1 : Math.Max(1, records.Count / 10);
                for (int i = 0; i < records.Count; i += step)
                {
                    var rec = records[i];
                    hash = hash * 31 + i;
                    hash = hash * 31 + (rec.StationCode?.GetHashCode() ?? 0);
                    hash = hash * 31 + (rec.Target?.GetHashCode() ?? 0);
                    hash = hash * 31 + rec.Rb_m.GetHashCode();
                    hash = hash * 31 + rec.Rf_m.GetHashCode();
                }

                if (records.Count > 1)
                {
                    var last = records[records.Count - 1];
                    hash = hash * 31 + (last.StationCode?.GetHashCode() ?? 0);
                    hash = hash * 31 + last.Rb_m.GetHashCode();
                }
                return hash;
            }
        }

        private List<TraverseRow> BuildInternal(IList<MeasurementRecord> records, LineSummary? run)
        {
            var list = new List<TraverseRow>();
            string line = run?.DisplayName ?? "?";
            string mode = "BF";
            TraverseRow? pending = null;
            int idx = 1;
            LineSummary? currentLineSummary = run;
            bool isFirstPointOfLine = false;
            string? firstPointCode = null;

            foreach (var r in records)
            {
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
                    isFirstPointOfLine = true;
                    firstPointCode = null;
                }

                if (currentLineSummary == null && r.LineSummary != null)
                    currentLineSummary = r.LineSummary;

                if (r.LineMarker == "Start-Line" && !string.IsNullOrWhiteSpace(r.Mode))
                {
                    var modeUpper = r.Mode.Trim().ToUpperInvariant();
                    if (modeUpper == "BF" || modeUpper == "FB")
                        mode = modeUpper;
                    isFirstPointOfLine = true;
                }

                bool isBF = mode == "BF";

                if (r.Rb_m.HasValue)
                {
                    if (isFirstPointOfLine && firstPointCode == null)
                    {
                        firstPointCode = r.StationCode;
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
