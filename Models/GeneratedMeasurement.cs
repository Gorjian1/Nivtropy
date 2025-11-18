namespace Nivtropy.Models
{
    /// <summary>
    /// Сгенерированное измерение для экспорта
    /// </summary>
    public class GeneratedMeasurement
    {
        public int Index { get; set; }
        public string PointCode { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public double? Rb_m { get; set; }  // Отсчет назад
        public double? Rf_m { get; set; }  // Отсчет вперед
        public double? HD_Back_m { get; set; }  // Расстояние назад
        public double? HD_Fore_m { get; set; }  // Расстояние вперед
        public double? Height_m { get; set; }  // Высота точки
        public bool IsBackSight { get; set; }  // Задний отсчет
    }
}
