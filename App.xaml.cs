using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Nivtropy.Services;
using Nivtropy.ViewModels;

namespace Nivtropy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Управляет Dependency Injection контейнером для приложения
    /// </summary>
    public partial class App : Application
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

            // Регистрация сервисов и ViewModels
            services.AddApplicationServices();
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
