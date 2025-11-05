using System.Windows;
using Nivtropy.ViewModels;
using Nivtropy.Views;

namespace Nivtropy
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new MainViewModel();
            // Первичный вид: таблица измерений
            vm.CurrentView = new DataViewControl { DataContext = vm.DataViewModel };
            DataContext = vm;
        }
    }
}