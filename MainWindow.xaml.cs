using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Nivtropy.Presentation.ViewModels;

namespace Nivtropy
{
    /// <summary>
    /// Главное окно приложения
    /// Использует Dependency Injection для получения ViewModel
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Получаем MainViewModel из DI контейнера
            DataContext = App.Services.GetRequiredService<MainViewModel>();
        }
    }
}
