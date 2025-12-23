using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;

namespace Nivtropy.Services
{
    /// <summary>
    /// Построитель структуры хода из записей измерений
    /// Реализует интерфейс ITraverseBuilder для соблюдения принципа инверсии зависимостей (DIP)
    /// </summary>
    public class TraverseBuilder : ITraverseBuilder
    {
        // Спаривание с учётом режима: BF (Back→Forward) или FB (Forward→Back)
        // Режим определяет порядок точек в паре
        public List<TraverseRow> Build(IEnumerable<MeasurementRecord> records, LineSummary? run = null)
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
