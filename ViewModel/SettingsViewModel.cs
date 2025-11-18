using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nivtropy.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        // Общие настройки
        private string _tableFontFamily = "Segoe UI";
        private double _tableFontSize = 13;
        private double _gridLineThickness = 1.0;
        private int _gridLineColorIndex = 0; // 0=светло-серый, 1=серый, 2=темно-серый, 3=голубоватый
        private bool _alternatingRowColors = true;
        private bool _showColumnHeaderIcons = true;

        // Настройки расчётов
        private bool _checkMinimumRayLength = false;
        private double _minimumRayLength = 5.0;
        private bool _checkMaximumRayLength = false;
        private double _maximumRayLength = 100.0;
        private bool _checkMinimumStationLength = false;
        private double _minimumStationLength = 10.0;
        private int _heightDecimalPlaces = 1; // Index: 0=3, 1=4, 2=5
        private int _deltaHDecimalPlaces = 1; // Index: 0=3, 1=4, 2=5

        public event PropertyChangedEventHandler? PropertyChanged;

        // Общие настройки - свойства
        public string TableFontFamily
        {
            get => _tableFontFamily;
            set => SetField(ref _tableFontFamily, value);
        }

        public double TableFontSize
        {
            get => _tableFontSize;
            set => SetField(ref _tableFontSize, value);
        }

        public double GridLineThickness
        {
            get => _gridLineThickness;
            set => SetField(ref _gridLineThickness, value);
        }

        public int GridLineColorIndex
        {
            get => _gridLineColorIndex;
            set => SetField(ref _gridLineColorIndex, value);
        }

        public bool AlternatingRowColors
        {
            get => _alternatingRowColors;
            set => SetField(ref _alternatingRowColors, value);
        }

        public bool ShowColumnHeaderIcons
        {
            get => _showColumnHeaderIcons;
            set => SetField(ref _showColumnHeaderIcons, value);
        }

        public bool CheckMinimumRayLength
        {
            get => _checkMinimumRayLength;
            set => SetField(ref _checkMinimumRayLength, value);
        }

        public double MinimumRayLength
        {
            get => _minimumRayLength;
            set => SetField(ref _minimumRayLength, value);
        }

        public bool CheckMaximumRayLength
        {
            get => _checkMaximumRayLength;
            set => SetField(ref _checkMaximumRayLength, value);
        }

        public double MaximumRayLength
        {
            get => _maximumRayLength;
            set => SetField(ref _maximumRayLength, value);
        }

        public bool CheckMinimumStationLength
        {
            get => _checkMinimumStationLength;
            set => SetField(ref _checkMinimumStationLength, value);
        }

        public double MinimumStationLength
        {
            get => _minimumStationLength;
            set => SetField(ref _minimumStationLength, value);
        }

        public int HeightDecimalPlaces
        {
            get => _heightDecimalPlaces;
            set => SetField(ref _heightDecimalPlaces, value);
        }

        public int DeltaHDecimalPlaces
        {
            get => _deltaHDecimalPlaces;
            set => SetField(ref _deltaHDecimalPlaces, value);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
