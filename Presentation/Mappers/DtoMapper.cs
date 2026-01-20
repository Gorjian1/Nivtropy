using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Presentation.Models;
using DomainMeasurementRecord = Nivtropy.Domain.Model.MeasurementRecord;
using PresentationMeasurementRecord = Nivtropy.Presentation.Models.MeasurementRecord;

namespace Nivtropy.Presentation.Mappers;

public static class DtoMapper
{
    public static StationDto ToDto(this TraverseRow row) => new()
    {
        LineName = row.LineName,
        Index = row.Index,
        BackCode = row.BackCode,
        ForeCode = row.ForeCode,
        BackReading = row.Rb_m,
        ForeReading = row.Rf_m,
        BackDistance = row.HdBack_m,
        ForeDistance = row.HdFore_m,
        BackHeight = row.BackHeight,
        ForeHeight = row.ForeHeight,
        BackHeightRaw = row.BackHeightZ0,
        ForeHeightRaw = row.ForeHeightZ0,
        IsBackHeightKnown = row.IsBackHeightKnown,
        IsForeHeightKnown = row.IsForeHeightKnown,
        IsArmDifferenceExceeded = row.IsArmDifferenceExceeded,
        Correction = row.Correction,
        BaselineCorrection = row.BaselineCorrection,
        CorrectionMode = row.CorrectionMode,
        RunSummary = row.LineSummary?.ToDto()
    };

    public static void ApplyFrom(this TraverseRow row, StationDto dto)
    {
        row.Correction = dto.Correction;
        row.BaselineCorrection = dto.BaselineCorrection;
        row.BackHeight = dto.BackHeight;
        row.ForeHeight = dto.ForeHeight;
        row.BackHeightZ0 = dto.BackHeightRaw;
        row.ForeHeightZ0 = dto.ForeHeightRaw;
        row.CorrectionMode = dto.CorrectionMode;
    }

    public static TraverseRow ToModel(this StationDto dto, LineSummary? summary = null)
    {
        return new TraverseRow
        {
            LineName = dto.LineName,
            Index = dto.Index,
            BackCode = dto.BackCode,
            ForeCode = dto.ForeCode,
            Rb_m = dto.BackReading,
            Rf_m = dto.ForeReading,
            HdBack_m = dto.BackDistance,
            HdFore_m = dto.ForeDistance,
            BackHeight = dto.BackHeight,
            ForeHeight = dto.ForeHeight,
            BackHeightZ0 = dto.BackHeightRaw,
            ForeHeightZ0 = dto.ForeHeightRaw,
            IsBackHeightKnown = dto.IsBackHeightKnown,
            IsForeHeightKnown = dto.IsForeHeightKnown,
            IsArmDifferenceExceeded = dto.IsArmDifferenceExceeded,
            Correction = dto.Correction,
            BaselineCorrection = dto.BaselineCorrection,
            CorrectionMode = dto.CorrectionMode,
            LineSummary = summary ?? dto.RunSummary?.ToModel()
        };
    }

    public static RunSummaryDto ToDto(this LineSummary line) => new()
    {
        Index = line.Index,
        OriginalLineNumber = line.OriginalLineNumber,
        StartPointCode = line.StartTarget ?? line.StartStation,
        EndPointCode = line.EndTarget ?? line.EndStation,
        StationCount = line.RecordCount,
        DeltaHSum = line.DeltaHSum,
        TotalDistanceBack = line.TotalDistanceBack,
        TotalDistanceFore = line.TotalDistanceFore,
        ArmDifferenceAccumulation = line.ArmDifferenceAccumulation,
        SystemId = line.SystemId,
        IsActive = line.IsActive,
        KnownPointsCount = line.KnownPointsCount,
        UseLocalAdjustment = line.UseLocalAdjustment,
        IsArmDifferenceAccumulationExceeded = line.IsArmDifferenceAccumulationExceeded,
        Closures = line.Closures.ToList(),
        SharedPointCodes = line.SharedPointCodes.ToList()
    };

    public static LineSummary ToModel(this RunSummaryDto dto)
    {
        var summary = new LineSummary(
            dto.Index,
            startTarget: dto.StartPointCode,
            startStation: null,
            endTarget: dto.EndPointCode,
            endStation: null,
            recordCount: dto.StationCount,
            deltaHSum: dto.DeltaHSum,
            totalDistanceBack: dto.TotalDistanceBack,
            totalDistanceFore: dto.TotalDistanceFore,
            armDifferenceAccumulation: dto.ArmDifferenceAccumulation,
            knownPointsCount: dto.KnownPointsCount,
            systemId: dto.SystemId,
            isActive: dto.IsActive,
            originalLineNumber: dto.OriginalLineNumber);

        summary.UseLocalAdjustment = dto.UseLocalAdjustment;
        summary.IsArmDifferenceAccumulationExceeded = dto.IsArmDifferenceAccumulationExceeded;
        summary.SetClosures(dto.Closures);
        summary.SetSharedPoints(dto.SharedPointCodes);

        return summary;
    }

    public static void ApplyFrom(this LineSummary summary, RunSummaryDto dto)
    {
        summary.TotalDistanceBack = dto.TotalDistanceBack;
        summary.TotalDistanceFore = dto.TotalDistanceFore;
        summary.ArmDifferenceAccumulation = dto.ArmDifferenceAccumulation;
        summary.SystemId = dto.SystemId;
        summary.IsActive = dto.IsActive;
        summary.KnownPointsCount = dto.KnownPointsCount;
        summary.UseLocalAdjustment = dto.UseLocalAdjustment;
        summary.IsArmDifferenceAccumulationExceeded = dto.IsArmDifferenceAccumulationExceeded;
        summary.SetClosures(dto.Closures);
        summary.SetSharedPoints(dto.SharedPointCodes);
    }

    public static SharedPointDto ToDto(this SharedPointLinkItem item, IEnumerable<int> runIndexes) => new()
    {
        Code = item.Code,
        IsEnabled = item.IsEnabled,
        RunIndexes = runIndexes.ToList()
    };

    public static DesignPointDto ToDto(this DesignRow row) => new()
    {
        Index = row.Index,
        Station = row.Station,
        Distance = row.Distance_m,
        OriginalDeltaH = row.OriginalDeltaH,
        Correction = row.Correction,
        AdjustedDeltaH = row.AdjustedDeltaH,
        DesignedHeight = row.DesignedHeight,
        IsEdited = row.IsEdited,
        OriginalHeight = row.OriginalHeight,
        OriginalDistance = row.OriginalDistance
    };

    public static DesignRow ToModel(this DesignPointDto dto)
    {
        return new DesignRow
        {
            Index = dto.Index,
            Station = dto.Station,
            Distance_m = dto.Distance,
            OriginalDeltaH = dto.OriginalDeltaH,
            Correction = dto.Correction,
            AdjustedDeltaH = dto.AdjustedDeltaH,
            DesignedHeight = dto.DesignedHeight,
            IsEdited = dto.IsEdited,
            OriginalHeight = dto.OriginalHeight,
            OriginalDistance = dto.OriginalDistance
        };
    }

    public static GeneratedMeasurementDto ToDto(this GeneratedMeasurement measurement) => new()
    {
        Index = measurement.Index,
        LineName = measurement.LineName,
        PointCode = measurement.PointCode,
        StationCode = measurement.StationCode,
        BackPointCode = measurement.BackPointCode,
        ForePointCode = measurement.ForePointCode,
        Rb_m = measurement.Rb_m,
        Rf_m = measurement.Rf_m,
        HD_Back_m = measurement.HD_Back_m,
        HD_Fore_m = measurement.HD_Fore_m,
        Height_m = measurement.Height_m,
        IsBackSight = measurement.IsBackSight,
        OriginalHeight = measurement.OriginalHeight,
        OriginalHD_Back = measurement.OriginalHD_Back,
        OriginalHD_Fore = measurement.OriginalHD_Fore
    };

    public static PresentationMeasurementRecord ToModel(this MeasurementAnnotationDto dto, LineSummary summary)
    {
        DomainMeasurementRecord record = dto.Measurement;
        return new PresentationMeasurementRecord
        {
            Seq = record.Seq,
            Mode = record.Mode,
            Target = record.Target,
            StationCode = record.StationCode,
            LineMarker = record.LineMarker,
            OriginalLineNumber = record.OriginalLineNumber,
            IsInvalidMeasurement = record.IsInvalidMeasurement,
            Rb_m = record.Rb_m,
            Rf_m = record.Rf_m,
            HdBack_m = record.HdBack_m,
            HdFore_m = record.HdFore_m,
            Z_m = record.Z_m,
            LineSummary = summary,
            ShotIndexWithinLine = dto.ShotIndexWithinLine,
            IsLineStart = dto.IsLineStart,
            IsLineEnd = dto.IsLineEnd
        };
    }
}
