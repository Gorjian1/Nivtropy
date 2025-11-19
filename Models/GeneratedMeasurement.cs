namespace Nivtropy.Models
{
    /// <summary>
    /// Сгенерированное измерение для экспорта
    /// </summary>
    public class GeneratedMeasurement
    {
        public int Index { get; set; }
        public string LineName { get; set; } = "?"; // Название хода
        public string PointCode { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public string? BackPointCode { get; set; }  // Код задней точки для Rb
        public string? ForePointCode { get; set; }  // Код передней точки для Rf
        public double? Rb_m { get; set; }  // Отсчет назад
        public double? Rf_m { get; set; }  // Отсчет вперед
        public double? HD_Back_m { get; set; }  // Расстояние назад
        public double? HD_Fore_m { get; set; }  // Расстояние вперед
        public double? Height_m { get; set; }  // Высота точки
        public bool IsBackSight { get; set; }  // Задний отсчет
    }

    /// <summary>
    /// Информация о нивелирном ходе из файла обработки
    /// </summary>
    public class TraverseInfo
    {
        public string LineName { get; set; } = string.Empty;
        public int StationCount { get; set; }
        public double TotalLengthBack_m { get; set; }
        public double TotalLengthFore_m { get; set; }
        public double TotalLength_m { get; set; }
        public double ArmAccumulation_m { get; set; }
    }
}
