using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Nivtropy.Services;
using Nivtropy.Presentation.ViewModels;

namespace Nivtropy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Управляет Dependency Injection контейнером для приложения
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Глобальный доступ к сервисам приложения
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Настройка Dependency Injection
            var services = new ServiceCollection();

            // Регистрация сервисов, менеджеров и ViewModels
            services.AddApplicationServices();
            services.AddDomainServices(); // Новая архитектура (DDD + граф)
            services.AddManagers();
            services.AddViewModels();

            // Создание контейнера
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Освобождение ресурсов DI контейнера
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnExit(e);
        }
    }
}
