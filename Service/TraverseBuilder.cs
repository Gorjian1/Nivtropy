using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    public static class TraverseBuilder
    {
        // Спаривание с учётом режима: BF (Back→Forward) или FB (Forward→Back)
        // Режим определяет порядок точек в паре
        public static List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null)
        {
            var list = new List<TraverseRow>();
            string line = run?.DisplayName ?? "?";
            string mode = "BF"; // По умолчанию BF (Back→Forward)
            TraverseRow? pending = null;
            int idx = 1;
            LineSummary? currentLineSummary = run;
            bool justStartedTraverse = false; // Флаг для отслеживания начала хода
            string? initialPointCode = null; // Код первой точки для создания станции #0

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
                    justStartedTraverse = false;
                    initialPointCode = null;
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

                    justStartedTraverse = true;
                    initialPointCode = null;
                }

                // Если только что начался ход и встретили запись с Z (но без Rb/Rf), это первая точка
                if (justStartedTraverse && r.Z_m.HasValue && !r.Rb_m.HasValue && !r.Rf_m.HasValue && !string.IsNullOrWhiteSpace(r.StationCode))
                {
                    initialPointCode = r.StationCode;
                    // Создаем станцию #0 для начальной точки (только для отображения)
                    var initialStation = new TraverseRow
                    {
                        LineName = line,
                        Index = 0,
                        BackCode = initialPointCode,
                        ForeCode = null,
                        LineSummary = currentLineSummary
                    };
                    list.Add(initialStation);
                    continue; // Пропускаем эту запись, она уже обработана
                }

                bool isBF = mode == "BF";

                if (r.Rb_m.HasValue)
                {
                    // Сбрасываем флаг начала хода, так как начались реальные измерения
                    justStartedTraverse = false;

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
                    // Сбрасываем флаг начала хода, так как начались реальные измерения
                    justStartedTraverse = false;

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
