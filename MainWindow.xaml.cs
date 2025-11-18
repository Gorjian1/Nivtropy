using System.Windows;
using System.Windows.Input;
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
    }
}
