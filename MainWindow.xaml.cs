using System.Windows;
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
    }
}
