using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nivtropy.Models
{
    /// <summary>
    /// Сгенерированное измерение для экспорта
    /// РЕДАКТИРУЕМАЯ МОДЕЛЬ: поддерживает изменение Z (высоты) и HD (расстояний)
    /// </summary>
    public class GeneratedMeasurement : INotifyPropertyChanged
    {
        private double? _hdBack_m;
        private double? _hdFore_m;
        private double? _height_m;

        public int Index { get; set; }
        public string LineName { get; set; } = "?"; // Название хода
        public string PointCode { get; set; } = string.Empty;
        public string StationCode { get; set; } = string.Empty;
        public string? BackPointCode { get; set; }  // Код задней точки для Rb
        public string? ForePointCode { get; set; }  // Код передней точки для Rf
        public double? Rb_m { get; set; }  // Отсчет назад
        public double? Rf_m { get; set; }  // Отсчет вперед

        /// <summary>
        /// Расстояние назад (РЕДАКТИРУЕМОЕ)
        /// </summary>
        public double? HD_Back_m
        {
            get => _hdBack_m;
            set
            {
                if (_hdBack_m != value)
                {
                    _hdBack_m = value;
                    OnPropertyChanged();
                    // При изменении HD нужно пересчитать Rb (отсчет назад)
                    RecalculateRb();
                }
            }
        }

        /// <summary>
        /// Расстояние вперед (РЕДАКТИРУЕМОЕ)
        /// </summary>
        public double? HD_Fore_m
        {
            get => _hdFore_m;
            set
            {
                if (_hdFore_m != value)
                {
                    _hdFore_m = value;
                    OnPropertyChanged();
                    // При изменении HD нужно пересчитать Rf (отсчет вперед)
                    RecalculateRf();
                }
            }
        }

        /// <summary>
        /// Высота точки (РЕДАКТИРУЕМАЯ)
        /// </summary>
        public double? Height_m
        {
            get => _height_m;
            set
            {
                if (_height_m != value)
                {
                    _height_m = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBackSight { get; set; }  // Задний отсчет

        /// <summary>
        /// Оригинальная высота (до редактирования)
        /// </summary>
        public double? OriginalHeight { get; set; }

        /// <summary>
        /// Оригинальное расстояние назад (до редактирования)
        /// </summary>
        public double? OriginalHD_Back { get; set; }

        /// <summary>
        /// Оригинальное расстояние вперед (до редактирования)
        /// </summary>
        public double? OriginalHD_Fore { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Пересчитывает Rb при изменении HD_Back_m
        /// Логика: если расстояние изменилось, но высота осталась той же, то отсчет должен измениться
        /// </summary>
        private void RecalculateRb()
        {
            // Пока оставляем Rb без изменений
            // В будущем можно добавить логику пересчета
        }

        /// <summary>
        /// Пересчитывает Rf при изменении HD_Fore_m
        /// Логика: если расстояние изменилось, но высота осталась той же, то отсчет должен измениться
        /// </summary>
        private void RecalculateRf()
        {
            // Пока оставляем Rf без изменений
            // В будущем можно добавить логику пересчета
        }
    }
}
