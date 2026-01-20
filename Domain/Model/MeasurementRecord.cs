namespace Nivtropy.Domain.Model;

public class MeasurementRecord
{
    public int? Seq { get; set; }
    public string? Mode { get; set; }
    public string? Target { get; set; }
    public string? StationCode { get; set; }

    /// <summary>
    /// Маркер хода: "Start-Line", "End-Line", "Cont-Line" или null для обычных измерений
    /// </summary>
    public string? LineMarker { get; set; }

    /// <summary>
    /// Оригинальный номер хода из файла (из Start-Line, например "BF 3" -> "3")
    /// </summary>
    public string? OriginalLineNumber { get; set; }

    /// <summary>
    /// Флаг ошибочного измерения (помечено ##### в файле, будет заменено повторным)
    /// </summary>
    public bool IsInvalidMeasurement { get; set; }

    public double? Rb_m { get; set; }
    public double? Rf_m { get; set; }
    public double? HdBack_m { get; set; }
    public double? HdFore_m { get; set; }
    public double? HD_m => HdBack_m ?? HdFore_m;
    public double? Z_m { get; set; }

    public double? DeltaH => (Rb_m.HasValue && Rf_m.HasValue) ? Rb_m.Value - Rf_m.Value : null;
    public bool IsValid => (Rb_m.HasValue && Rf_m.HasValue) || (HdBack_m.HasValue && HdFore_m.HasValue);
}
