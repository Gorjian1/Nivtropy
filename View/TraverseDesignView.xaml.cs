using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseDesignView : UserControl
    {
        public TraverseDesignView()
        {
            InitializeComponent();
        }

        private void SetHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseDesignViewModel viewModel)
                return;

            // Получаем значение из TextBox
            var heightText = HeightTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(heightText))
            {
                MessageBox.Show("Введите значение высоты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Парсим значение
            if (!double.TryParse(heightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                MessageBox.Show("Некорректное значение высоты. Используйте формат: 100.0000", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Устанавливаем высоту
            viewModel.SetKnownHeightForSelectedPoint(height);
            MessageBox.Show($"Высота {height:F4} м установлена для точки {viewModel.SelectedRow?.PointCode}.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearHeightButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TraverseDesignViewModel viewModel)
                return;

            var pointCode = viewModel.SelectedRow?.PointCode;
            if (string.IsNullOrWhiteSpace(pointCode))
                return;

            var result = MessageBox.Show($"Удалить известную высоту для точки {pointCode}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                viewModel.ClearKnownHeightForSelectedPoint();
                MessageBox.Show($"Известная высота для точки {pointCode} удалена.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
