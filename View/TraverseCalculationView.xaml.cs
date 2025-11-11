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

        private void SetBackHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseCalculationViewModel viewModel)
                return;

            var heightText = BackHeightTextBox.Text?.Trim();
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

            viewModel.SetKnownHeightForBackPoint(height);
            MessageBox.Show($"Высота {height:F4} м установлена для задней точки {viewModel.SelectedRow?.BackCode}.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            BackHeightTextBox.Clear();
        }

        private void SetForeHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseCalculationViewModel viewModel)
                return;

            var heightText = ForeHeightTextBox.Text?.Trim();
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

            viewModel.SetKnownHeightForForePoint(height);
            MessageBox.Show($"Высота {height:F4} м установлена для передней точки {viewModel.SelectedRow?.ForeCode}.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            ForeHeightTextBox.Clear();
        }

        private void ClearBackHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseCalculationViewModel viewModel)
                return;

            var pointCode = viewModel.SelectedRow?.BackCode;
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            var result = MessageBox.Show($"Удалить известную высоту для задней точки {pointCode}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                viewModel.ClearKnownHeightForBackPoint();
                MessageBox.Show($"Известная высота для точки {pointCode} удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearForeHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseCalculationViewModel viewModel)
                return;

            var pointCode = viewModel.SelectedRow?.ForeCode;
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            var result = MessageBox.Show($"Удалить известную высоту для передней точки {pointCode}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                viewModel.ClearKnownHeightForForePoint();
                MessageBox.Show($"Известная высота для точки {pointCode} удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
