using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nivtropy.Presentation.Models
{
    public class DesignRow : INotifyPropertyChanged
    {
        private double? _distance_m;
        private double? _originalDeltaH;
        private double _correction;
        private double? _adjustedDeltaH;
        private double _designedHeight;

        public int Index { get; set; }
        public string Station { get; set; } = string.Empty;

        /// <summary>
        /// Средняя горизонтальная длина хода (среднее между задним и передним измерениями)
        /// РЕДАКТИРУЕМОЕ ПОЛЕ: при изменении требуется пересчет связанных параметров
        /// </summary>
        public double? Distance_m
        {
            get => _distance_m;
            set
            {
                if (_distance_m != value)
                {
                    _distance_m = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEdited));
                }
            }
        }

        /// <summary>
        /// Исходное превышение (до уравнивания)
        /// </summary>
        public double? OriginalDeltaH
        {
            get => _originalDeltaH;
            set
            {
                if (_originalDeltaH != value)
                {
                    _originalDeltaH = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Поправка для данного хода (пропорционально длине)
        /// </summary>
        public double Correction
        {
            get => _correction;
            set
            {
                if (_correction != value)
                {
                    _correction = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Уравненное превышение (после распределения невязки)
        /// </summary>
        public double? AdjustedDeltaH
        {
            get => _adjustedDeltaH;
            set
            {
                if (_adjustedDeltaH != value)
                {
                    _adjustedDeltaH = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Проектная высота точки
        /// РЕДАКТИРУЕМОЕ ПОЛЕ: при изменении требуется пересчет связанных параметров
        /// </summary>
        public double DesignedHeight
        {
            get => _designedHeight;
            set
            {
                if (_designedHeight != value)
                {
                    _designedHeight = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEdited));
                }
            }
        }

        /// <summary>
        /// Флаг, указывающий что данная строка была отредактирована пользователем
        /// </summary>
        public bool IsEdited { get; set; }

        /// <summary>
        /// Оригинальная высота (до редактирования) - для возможности сброса
        /// </summary>
        public double? OriginalHeight { get; set; }

        /// <summary>
        /// Оригинальная дистанция (до редактирования) - для возможности сброса
        /// </summary>
        public double? OriginalDistance { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
