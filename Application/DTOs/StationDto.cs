using Nivtropy.Application.Enums;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO станции нивелирования для расчётов (замена TraverseRow).
/// Не содержит UI-логики (INotifyPropertyChanged, Display-свойства).
/// </summary>
public class StationDto
{
    public string LineName { get; set; } = "";
    public int Index { get; set; }
    public string? BackCode { get; set; }
    public string? ForeCode { get; set; }
    public RunSummaryDto? RunSummary { get; set; }

    // Измерения
    public double? BackReading { get; set; }      // Rb_m
    public double? ForeReading { get; set; }      // Rf_m
    public double? BackDistance { get; set; }     // HdBack_m
    public double? ForeDistance { get; set; }     // HdFore_m

    // Вычисляемые
    public double? DeltaH => (BackReading.HasValue && ForeReading.HasValue)
        ? BackReading.Value - ForeReading.Value
        : null;

    public double? StationLength => (BackDistance.HasValue && ForeDistance.HasValue)
        ? BackDistance.Value + ForeDistance.Value
        : null;

    public double? ArmDifference => (BackDistance.HasValue && ForeDistance.HasValue)
        ? BackDistance.Value - ForeDistance.Value
        : null;

    // Высоты
    public double? BackHeight { get; set; }
    public double? ForeHeight { get; set; }
    public double? BackHeightRaw { get; set; }    // Z0 без поправки
    public double? ForeHeightRaw { get; set; }    // Z0 без поправки
    public bool IsBackHeightKnown { get; set; }
    public bool IsForeHeightKnown { get; set; }
    public bool IsArmDifferenceExceeded { get; set; }

    // Уравнивание
    public double? Correction { get; set; }
    public double? BaselineCorrection { get; set; }
    public CorrectionDisplayMode CorrectionMode { get; set; } = CorrectionDisplayMode.None;

    public double? AdjustedDeltaH => DeltaH.HasValue && Correction.HasValue
        ? DeltaH.Value + Correction.Value
        : DeltaH;

    public bool IsVirtualStation => string.IsNullOrWhiteSpace(ForeCode) && !DeltaH.HasValue;
}
