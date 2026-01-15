using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.ViewModelss.Base;

namespace Nivtropy.Presentation.ViewModelss
{
    public class SettingsViewModel : ViewModelBase
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Nivtropy",
            "settings.json"
        );

        private string _tableFontFamily = "Segoe UI";
        private double _tableFontSize = 13;
        private int _gridLineColorIndex = 0;
        private RowColoringMode _rowColoringMode = RowColoringMode.Alternating;
        private bool _showColumnHeaderIcons = true;
        private bool _checkMinimumRayLength = false;
        private double _minimumRayLength = 5.0;
        private bool _checkMaximumRayLength = false;
        private double _maximumRayLength = 100.0;
        private bool _checkMinimumStationLength = false;
        private double _minimumStationLength = 10.0;
        private int _heightDecimalPlaces = 1;
        private int _deltaHDecimalPlaces = 1;

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

        public int GridLineColorIndex
        {
            get => _gridLineColorIndex;
            set => SetField(ref _gridLineColorIndex, value);
        }

        public RowColoringMode RowColoringMode
        {
            get => _rowColoringMode;
            set => SetField(ref _rowColoringMode, value);
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
            set
            {
                if (SetField(ref _heightDecimalPlaces, value))
                {
                    OnPropertyChanged(nameof(ActualHeightDecimalPlaces));
                    OnPropertyChanged(nameof(HeightFormatString));
                }
            }
        }

        public int DeltaHDecimalPlaces
        {
            get => _deltaHDecimalPlaces;
            set
            {
                if (SetField(ref _deltaHDecimalPlaces, value))
                {
                    OnPropertyChanged(nameof(ActualDeltaHDecimalPlaces));
                    OnPropertyChanged(nameof(DeltaHFormatString));
                }
            }
        }

        /// <summary>
        /// Возвращает количество знаков после запятой для высот: 3, 4 или 5
        /// </summary>
        public int ActualHeightDecimalPlaces => HeightDecimalPlaces + 3;

        /// <summary>
        /// Возвращает количество знаков после запятой для превышений: 3, 4 или 5
        /// </summary>
        public int ActualDeltaHDecimalPlaces => DeltaHDecimalPlaces + 3;

        /// <summary>
        /// Возвращает формат строки для высот (например, "F4")
        /// </summary>
        public string HeightFormatString => $"F{ActualHeightDecimalPlaces}";

        /// <summary>
        /// Возвращает формат строки для превышений (например, "+0.0000;-0.0000;0.0000")
        /// </summary>
        public string DeltaHFormatString
        {
            get
            {
                var zeros = new string('0', ActualDeltaHDecimalPlaces);
                return $"+0.{zeros};-0.{zeros};0.{zeros}";
            }
        }

        /// <summary>
        /// Устанавливает значение и сохраняет настройки при изменении
        /// </summary>
        private new bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (base.SetField(ref field, value, propertyName))
            {
                Save();
                return true;
            }
            return false;
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Игнорируем ошибки сохранения
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<SettingsViewModel>(json);
                    if (settings != null)
                    {
                        // Напрямую устанавливаем поля, чтобы не вызывать Save()
                        _tableFontFamily = settings.TableFontFamily;
                        _tableFontSize = settings.TableFontSize;
                        _gridLineColorIndex = settings.GridLineColorIndex;
                        _rowColoringMode = settings.RowColoringMode;
                        _showColumnHeaderIcons = settings.ShowColumnHeaderIcons;
                        _checkMinimumRayLength = settings.CheckMinimumRayLength;
                        _minimumRayLength = settings.MinimumRayLength;
                        _checkMaximumRayLength = settings.CheckMaximumRayLength;
                        _maximumRayLength = settings.MaximumRayLength;
                        _checkMinimumStationLength = settings.CheckMinimumStationLength;
                        _minimumStationLength = settings.MinimumStationLength;
                        _heightDecimalPlaces = settings.HeightDecimalPlaces;
                        _deltaHDecimalPlaces = settings.DeltaHDecimalPlaces;

                        // Уведомляем об изменении всех свойств
                        OnPropertyChanged(nameof(TableFontFamily));
                        OnPropertyChanged(nameof(TableFontSize));
                        OnPropertyChanged(nameof(GridLineColorIndex));
                        OnPropertyChanged(nameof(RowColoringMode));
                        OnPropertyChanged(nameof(ShowColumnHeaderIcons));
                        OnPropertyChanged(nameof(CheckMinimumRayLength));
                        OnPropertyChanged(nameof(MinimumRayLength));
                        OnPropertyChanged(nameof(CheckMaximumRayLength));
                        OnPropertyChanged(nameof(MaximumRayLength));
                        OnPropertyChanged(nameof(CheckMinimumStationLength));
                        OnPropertyChanged(nameof(MinimumStationLength));
                        OnPropertyChanged(nameof(HeightDecimalPlaces));
                        OnPropertyChanged(nameof(DeltaHDecimalPlaces));
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки загрузки, используем значения по умолчанию
            }
        }
    }
}
