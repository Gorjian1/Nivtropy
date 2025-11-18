using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nivtropy.Models;
using Nivtropy.ViewModels;

namespace Nivtropy
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        /// <summary>
        /// Обработка нажатия Enter в поле ввода высоты репера
        /// </summary>
        private void BenchmarkHeightTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel viewModel)
            {
                // Попытка выполнить команду добавления репера
                if (viewModel.CalculationViewModel.AddBenchmarkCommand.CanExecute(null))
                {
                    viewModel.CalculationViewModel.AddBenchmarkCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Показать/скрыть выпадающий список реперов
        /// </summary>
        private void ToggleBenchmarksButton_Click(object sender, RoutedEventArgs e)
        {
            if (BenchmarksListPopup.Visibility == Visibility.Visible)
            {
                BenchmarksListPopup.Visibility = Visibility.Collapsed;
            }
            else
            {
                BenchmarksListPopup.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Удалить репер из списка
        /// </summary>
        private void RemoveBenchmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.Tag is BenchmarkItem benchmark &&
                DataContext is MainViewModel viewModel)
            {
                viewModel.CalculationViewModel.RemoveBenchmarkCommand.Execute(benchmark);
            }
        }
    }
}
