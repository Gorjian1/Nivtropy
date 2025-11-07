using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    public static class TraverseBuilder
    {
        // Наивное спаривание: внутри линии Rb и Rf идут парой (порядок любой).
        public static List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null)
        {
            var list = new List<TraverseRow>();
            string line = run?.DisplayName ?? "?";
            TraverseRow? pending = null;
            int idx = 1;

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
                    idx = 1;
                }

                if (r.Rb_m.HasValue)
                {
                    if (pending == null)
                        pending = new TraverseRow { LineName = line, Index = idx++, BackCode = r.StationCode, Rb_m = r.Rb_m, HdBack_m = r.HD_m };
                    else
                    {
                        pending.Rb_m ??= r.Rb_m; pending.HdBack_m ??= r.HD_m; pending.BackCode ??= r.StationCode;
                        list.Add(pending); pending = null;
                    }
                    continue;
                }

                if (r.Rf_m.HasValue)
                {
                    if (pending == null)
                        pending = new TraverseRow { LineName = line, Index = idx++, ForeCode = r.StationCode, Rf_m = r.Rf_m, HdFore_m = r.HD_m };
                    else
                    {
                        pending.Rf_m ??= r.Rf_m; pending.HdFore_m ??= r.HD_m; pending.ForeCode ??= r.StationCode;
                        list.Add(pending); pending = null;
                    }
                }
            }
            if (pending != null) list.Add(pending);
            return list;
        }
    }
}
