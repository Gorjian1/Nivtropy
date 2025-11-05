using System.Collections.Generic;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    public static class TraverseBuilder
    {
        // Наивное спаривание: внутри линии Rb и Rf идут парой (порядок любой).
        public static List<TraverseRow> Build(IEnumerable<MeasurementRecord> records)
        {
            var list = new List<TraverseRow>();
            string line = "?";
            TraverseRow pending = null;
            int idx = 1;

            foreach (var r in records)
            {
               // if (r.IsStartLine) { line = r.LineName ?? line; pending = null; idx = 1; continue; }
                //if (r.IsEndLine) { if (pending != null) { list.Add(pending); pending = null; } continue; }

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
