using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Domain.DTOs;
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
        private readonly Dictionary<int, List<StationDto>> _cache = new();
        private readonly object _cacheLock = new();

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        public List<StationDto> Build(IEnumerable<MeasurementRecord> records, RunSummaryDto? run = null)
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

        private int ComputeHash(IList<MeasurementRecord> records, RunSummaryDto? run)
        {
            unchecked
            {
                int hash = 17;
                if (run != null)
                {
                    hash = hash * 31 + run.Index;
                    hash = hash * 31 + (run.OriginalLineNumber?.GetHashCode() ?? 0);
                    hash = hash * 31 + run.StationCount;
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

        private List<StationDto> BuildInternal(IList<MeasurementRecord> records, RunSummaryDto? run)
        {
            var list = new List<StationDto>();
            string line = run?.OriginalLineNumber ?? "?";
            string mode = "BF";
            StationDto? pending = null;
            int idx = 1;
            RunSummaryDto? currentLineSummary = run;
            bool isFirstPointOfLine = false;
            string? firstPointCode = null;
            var summaryCache = new Dictionary<LineSummary, RunSummaryDto>();

            foreach (var r in records)
            {
                RunSummaryDto? cachedSummary = null;
                if (r.LineSummary != null && !summaryCache.TryGetValue(r.LineSummary, out cachedSummary))
                {
                    cachedSummary = ConvertSummary(r.LineSummary);
                    summaryCache[r.LineSummary] = cachedSummary;
                }

                if (r.LineSummary != null && !ReferenceEquals(r.LineSummary, run) && r.LineSummary.DisplayName != line)
                {
                    if (pending != null)
                    {
                        list.Add(pending);
                        pending = null;
                    }
                    line = r.LineSummary.DisplayName;
                    currentLineSummary = cachedSummary;
                    idx = 1;
                    isFirstPointOfLine = true;
                    firstPointCode = null;
                }

                if (currentLineSummary == null && cachedSummary != null)
                    currentLineSummary = cachedSummary;

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
                        var virtualStation = new StationDto
                        {
                            LineName = line,
                            Index = idx++,
                            BackCode = r.StationCode,
                            RunSummary = currentLineSummary
                        };
                        list.Add(virtualStation);
                        isFirstPointOfLine = false;
                    }

                    if (pending == null)
                    {
                        if (isBF)
                            pending = new StationDto { LineName = line, Index = idx++, BackCode = r.StationCode, BackReading = r.Rb_m, BackDistance = r.HD_m, RunSummary = currentLineSummary };
                        else
                            pending = new StationDto { LineName = line, Index = idx++, ForeCode = r.StationCode, BackReading = r.Rb_m, ForeDistance = r.HD_m, RunSummary = currentLineSummary };
                    }
                    else
                    {
                        if (isBF)
                        {
                            pending.BackReading ??= r.Rb_m;
                            pending.BackDistance ??= r.HD_m;
                            pending.BackCode ??= r.StationCode;
                        }
                        else
                        {
                            pending.BackReading ??= r.Rb_m;
                            pending.ForeDistance ??= r.HD_m;
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
                            pending = new StationDto { LineName = line, Index = idx++, ForeCode = r.StationCode, ForeReading = r.Rf_m, ForeDistance = r.HD_m, RunSummary = currentLineSummary };
                        else
                            pending = new StationDto { LineName = line, Index = idx++, BackCode = r.StationCode, ForeReading = r.Rf_m, BackDistance = r.HD_m, RunSummary = currentLineSummary };
                    }
                    else
                    {
                        if (isBF)
                        {
                            pending.ForeReading ??= r.Rf_m;
                            pending.ForeDistance ??= r.HD_m;
                            pending.ForeCode ??= r.StationCode;
                        }
                        else
                        {
                            pending.ForeReading ??= r.Rf_m;
                            pending.BackDistance ??= r.HD_m;
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

        private static RunSummaryDto ConvertSummary(LineSummary summary)
        {
            return new RunSummaryDto
            {
                Index = summary.Index,
                OriginalLineNumber = summary.OriginalLineNumber,
                StartPointCode = summary.StartTarget ?? summary.StartStation,
                EndPointCode = summary.EndTarget ?? summary.EndStation,
                StationCount = summary.RecordCount,
                DeltaHSum = summary.DeltaHSum,
                TotalDistanceBack = summary.TotalDistanceBack,
                TotalDistanceFore = summary.TotalDistanceFore,
                ArmDifferenceAccumulation = summary.ArmDifferenceAccumulation,
                SystemId = summary.SystemId,
                IsActive = summary.IsActive,
                KnownPointsCount = summary.KnownPointsCount,
                UseLocalAdjustment = summary.UseLocalAdjustment,
                Closures = summary.Closures.ToList(),
                SharedPointCodes = summary.SharedPointCodes.ToList()
            };
        }
    }
}
