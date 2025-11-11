using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseCalculationView : UserControl
    {
        public TraverseCalculationView()
        {
            InitializeComponent();
        }

        private void PointCodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Обновляем кнопки при изменении кода точки
            if (DataContext is TraverseCalculationViewModel viewModel)
            {
                var pointCode = PointCodeTextBox.Text?.Trim();
                viewModel.UpdateSelectedPoint(pointCode);
            }
        }

        private string? GetPointCode()
        {
            return PointCodeTextBox.Text?.Trim();
        }

        private void SetHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseCalculationViewModel viewModel)
                return;

            var pointCode = GetPointCode();
            if (string.IsNullOrWhiteSpace(pointCode))
            {
                MessageBox.Show("Введите код точки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var heightText = HeightTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(heightText))
            {
                MessageBox.Show("Введите значение высоты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(heightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                MessageBox.Show("Некорректное значение высоты. Используйте формат: 100.0000", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            viewModel.SetKnownHeightForPoint(pointCode, height);
            MessageBox.Show($"Высота {height:F4} м установлена для точки {pointCode}.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            HeightTextBox.Clear();
        }

        private void ClearHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseCalculationViewModel viewModel)
                return;

            var pointCode = GetPointCode();
            if (string.IsNullOrWhiteSpace(pointCode))
            {
                MessageBox.Show("Введите код точки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Удалить известную высоту для точки {pointCode}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                viewModel.ClearKnownHeightForPoint(pointCode);
                MessageBox.Show($"Известная высота для точки {pointCode} удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
