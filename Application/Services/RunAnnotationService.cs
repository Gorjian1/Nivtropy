using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Domain.Model;

namespace Nivtropy.Application.Services;

public interface IRunAnnotationService
{
    IReadOnlyList<RunAnnotationGroupDto> AnnotateRuns(IList<MeasurementRecord> records);
}

public class RunAnnotationService : IRunAnnotationService
{
    public IReadOnlyList<RunAnnotationGroupDto> AnnotateRuns(IList<MeasurementRecord> records)
    {
        var summaries = new List<RunAnnotationGroupDto>();
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
            var annotations = BuildAnnotations(group);
            summaries.Add(new RunAnnotationGroupDto
            {
                Summary = summary,
                Records = annotations
            });
        }

        return summaries;
    }

    private static List<MeasurementAnnotationDto> BuildAnnotations(IReadOnlyList<MeasurementRecord> group)
    {
        var result = new List<MeasurementAnnotationDto>(group.Count);
        if (group.Count == 0)
            return result;

        var start = group.FirstOrDefault(gr => gr.Rb_m.HasValue) ?? group.First();
        var end = group.LastOrDefault(gr => gr.Rf_m.HasValue) ?? group.Last();

        for (int i = 0; i < group.Count; i++)
        {
            var rec = group[i];
            result.Add(new MeasurementAnnotationDto
            {
                Measurement = rec,
                ShotIndexWithinLine = i + 1,
                IsLineStart = ReferenceEquals(rec, start),
                IsLineEnd = ReferenceEquals(rec, end)
            });
        }

        return result;
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

    private static RunSummaryDto BuildSummary(int index, IReadOnlyList<MeasurementRecord> group)
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

        return new RunSummaryDto
        {
            Index = index,
            OriginalLineNumber = originalLineNumber,
            StartPointCode = start.Target ?? start.StationCode,
            EndPointCode = end.Target ?? end.StationCode,
            StationCount = group.Count,
            DeltaHSum = deltaSum,
            TotalDistanceBack = totalDistanceBack,
            TotalDistanceFore = totalDistanceFore,
            ArmDifferenceAccumulation = armDiffAccumulation
        };
    }
}
