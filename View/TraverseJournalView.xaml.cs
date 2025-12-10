using System.Windows;
using System.Windows.Controls;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    /// <summary>
    /// Логика взаимодействия для TraverseJournalView.xaml
    /// Минималистичный code-behind - вся логика в ViewModel и Services (MVVM + SOLID)
    /// </summary>
    public partial class TraverseJournalView : UserControl
    {
        private TraverseJournalViewModel? ViewModel => DataContext as TraverseJournalViewModel;

        public TraverseJournalView()
        {
            InitializeComponent();

            // Подписка на изменения размера для перерисовки
            ProfileCanvas.SizeChanged += (s, e) => RedrawProfile();
            SystemVisualizationCanvas.SizeChanged += (s, e) => RedrawSystemVisualization();

            // Подписка на изменения DataContext
            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is TraverseJournalViewModel oldVm)
                {
                    // Отписываемся от старого ViewModel
                    oldVm.PropertyChanged -= ViewModel_PropertyChanged;
                }

                if (e.NewValue is TraverseJournalViewModel newVm)
                {
                    // Подписываемся на новый ViewModel
                    newVm.PropertyChanged += ViewModel_PropertyChanged;

                    // Первоначальная отрисовка
                    RedrawProfile();
                    RedrawSystemVisualization();
                }
            };

            // Keyboard shortcuts
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.R && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    RedrawProfile();
                    e.Handled = true;
                }
            };

            Focusable = true;
        }

        /// <summary>
        /// Обработчик изменений свойств ViewModel для перерисовки
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Перерисовываем профиль при изменении данных или настроек отображения
            switch (e.PropertyName)
            {
                case nameof(TraverseJournalViewModel.ShowZ):
                case nameof(TraverseJournalViewModel.ShowZ0):
                case nameof(TraverseJournalViewModel.ShowAnomalies):
                case nameof(TraverseJournalViewModel.ProfileColor):
                case nameof(TraverseJournalViewModel.ProfileZ0Color):
                case nameof(TraverseJournalViewModel.ManualMinHeight):
                case nameof(TraverseJournalViewModel.ManualMaxHeight):
                case nameof(TraverseJournalViewModel.SensitivitySigma):
                case nameof(TraverseJournalViewModel.CurrentStatistics):
                    RedrawProfile();
                    break;

                case nameof(TraverseJournalViewModel.Calculation):
                    RedrawProfile();
                    RedrawSystemVisualization();
                    break;
            }
        }

        /// <summary>
        /// Перерисовать профиль хода
        /// Вся логика делегирована сервису через ViewModel
        /// </summary>
        private void RedrawProfile()
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Делегируем рисование ViewModel, который использует сервис
            vm.DrawProfile(ProfileCanvas);
        }

        /// <summary>
        /// Перерисовать визуализацию системы ходов
        /// Вся логика делегирована сервису через ViewModel
        /// </summary>
        private void RedrawSystemVisualization()
        {
            var vm = ViewModel;
            if (vm == null) return;

            // Делегируем рисование ViewModel, который использует сервис
            vm.DrawSystemVisualization(SystemVisualizationCanvas);
        }

        #region XAML Event Handlers

        // Эти handlers нужны для связи с XAML и делегируют вызовы к ViewModel

        private void ProfileCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawProfile();
        }

        private void ProfileColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Tag is string colorHex)
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                if (ViewModel != null)
                    ViewModel.ProfileColor = color;
            }
        }

        private void ProfileZ0ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Tag is string colorHex)
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                if (ViewModel != null)
                    ViewModel.ProfileZ0Color = color;
            }
        }

        private void HeightLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && ViewModel != null)
            {
                if (textBox.Name == "MinHeightTextBox")
                {
                    if (double.TryParse(textBox.Text, out var minHeight))
                        ViewModel.ManualMinHeight = minHeight;
                    else
                        ViewModel.ManualMinHeight = null;
                }
                else if (textBox.Name == "MaxHeightTextBox")
                {
                    if (double.TryParse(textBox.Text, out var maxHeight))
                        ViewModel.ManualMaxHeight = maxHeight;
                    else
                        ViewModel.ManualMaxHeight = null;
                }
            }
        }

        private void AutoScaleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ManualMinHeight = null;
                ViewModel.ManualMaxHeight = null;
                RedrawProfile();
            }
        }

        private void CloseProfilePopup_Click(object sender, RoutedEventArgs e)
        {
            // Закрываем popup (обработка в XAML через Popup.IsOpen binding)
        }

        private void BenchmarkHeightTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // Логика добавления репера уже в ViewModel через Command
                e.Handled = true;
            }
        }

        private void RemoveBenchmark_Click(object sender, RoutedEventArgs e)
        {
            // Логика удаления репера через Command в ViewModel
        }

        private void SystemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RedrawSystemVisualization();
        }

        private void ShowTraverseDetails_Click(object sender, RoutedEventArgs e)
        {
            // Логика отображения деталей хода
        }

        private void RunActivationChanged(object sender, RoutedEventArgs e)
        {
            // Логика изменения активации хода - перерисовываем систему
            RedrawSystemVisualization();
        }

        #endregion
    }
}
