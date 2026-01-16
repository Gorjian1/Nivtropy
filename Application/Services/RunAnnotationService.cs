using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Application.Services;

public interface IRunAnnotationService
{
    IReadOnlyList<LineSummary> AnnotateRuns(IList<MeasurementRecord> records);
}

public class RunAnnotationService : IRunAnnotationService
{
    public IReadOnlyList<LineSummary> AnnotateRuns(IList<MeasurementRecord> records)
    {
        var summaries = new List<LineSummary>();
        if (records.Count == 0)
            return summaries;

        var groups = new List<List<MeasurementRecord>>();
        var current = new List<MeasurementRecord>();
        MeasurementRecord? previous = null;

        foreach (var record in records)
        {
            if (previous != null && ShouldStartNewLine(previous, record))
            {
                if (current.Count > 0)
                {
                    groups.Add(current);
                    current = new List<MeasurementRecord>();
                }
            }

            current.Add(record);
            previous = record;
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            int index = g + 1;

            var summary = BuildSummary(index, group);
            summaries.Add(summary);

            var start = group.FirstOrDefault(gr => gr.Rb_m.HasValue) ?? group.First();
            var end = group.LastOrDefault(gr => gr.Rf_m.HasValue) ?? group.Last();

            for (int i = 0; i < group.Count; i++)
            {
                var rec = group[i];
                rec.LineSummary = summary;
                rec.ShotIndexWithinLine = i + 1;
                rec.IsLineStart = ReferenceEquals(rec, start);
                rec.IsLineEnd = ReferenceEquals(rec, end);
            }
        }

        return summaries;
    }

    private static bool ShouldStartNewLine(MeasurementRecord previous, MeasurementRecord current)
    {
        if (current.LineMarker == "Start-Line")
            return true;

        if (current.LineMarker == "Cont-Line")
            return false;

        if (current.LineMarker == "End-Line")
            return false;

        if (current.LineMarker == null && previous.LineMarker == null)
        {
            if (current.Seq.HasValue && previous.Seq.HasValue)
            {
                if (current.Seq.Value - previous.Seq.Value > 50)
                    return true;
            }

            if (current.Mode != null && current.Mode.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!string.Equals(previous.Mode, current.Mode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static LineSummary BuildSummary(int index, IReadOnlyList<MeasurementRecord> group)
    {
        var start = group.FirstOrDefault(r => r.Rb_m.HasValue) ?? group.First();
        var end = group.LastOrDefault(r => r.Rf_m.HasValue) ?? group.Last();

        var startLineRecord = group.FirstOrDefault(r => r.LineMarker == "Start-Line");
        var originalLineNumber = startLineRecord?.OriginalLineNumber;

        double? deltaSum = null;
        double? totalDistanceBack = null;
        double? totalDistanceFore = null;
        double? armDiffAccumulation = null;

        foreach (var rec in group)
        {
            if (rec.DeltaH.HasValue)
            {
                deltaSum = (deltaSum ?? 0d) + rec.DeltaH.Value;
            }

            if (rec.HdBack_m.HasValue)
            {
                totalDistanceBack = (totalDistanceBack ?? 0d) + rec.HdBack_m.Value;
            }

            if (rec.HdFore_m.HasValue)
            {
                totalDistanceFore = (totalDistanceFore ?? 0d) + rec.HdFore_m.Value;
            }

            if (rec.HdBack_m.HasValue && rec.HdFore_m.HasValue)
            {
                var armDiff = rec.HdBack_m.Value - rec.HdFore_m.Value;
                armDiffAccumulation = (armDiffAccumulation ?? 0d) + armDiff;
            }
        }

        return new LineSummary(
            index,
            start.Target,
            start.StationCode,
            end.Target,
            end.StationCode,
            group.Count,
            deltaSum,
            totalDistanceBack,
            totalDistanceFore,
            armDiffAccumulation,
            originalLineNumber: originalLineNumber);
    }
}
