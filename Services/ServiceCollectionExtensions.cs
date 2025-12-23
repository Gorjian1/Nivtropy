using Microsoft.Extensions.DependencyInjection;
using Nivtropy.Services;
using Nivtropy.Services.Export;
using Nivtropy.Services.Logging;
using Nivtropy.Services.Statistics;
using Nivtropy.Services.Tolerance;
using Nivtropy.Services.Visualization;
using Nivtropy.ViewModels;
using Nivtropy.ViewModels.Managers;

namespace Nivtropy.Services
{
    /// <summary>
    /// Методы расширения для регистрации сервисов приложения в DI контейнере
    /// Следует принципам SOLID - все зависимости регистрируются в одном месте
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрирует все сервисы приложения
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Сервис логирования
            services.AddSingleton<ILoggerService, FileLoggerService>();

            // Сервисы парсинга и построения данных
            services.AddSingleton<IDataParser, DatParser>();
            services.AddSingleton<ITraverseBuilder, TraverseBuilder>();

            // Сервисы визуализации
            services.AddSingleton<IProfileVisualizationService, ProfileVisualizationService>();
            services.AddSingleton<ITraverseSystemVisualizationService, TraverseSystemVisualizationService>();

            // Сервисы статистики и анализа
            services.AddSingleton<IProfileStatisticsService, ProfileStatisticsService>();

            // Сервисы экспорта
            services.AddSingleton<IExportService, TraverseExportService>();

            // Сервисы работы с допусками
            services.AddSingleton<IToleranceService, ToleranceService>();

            return services;
        }

        /// <summary>
        /// Регистрирует ViewModels приложения
        /// </summary>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            // ViewModels регистрируются как Transient, т.к. могут создаваться несколько экземпляров
            services.AddTransient<DataViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<TraverseCalculationViewModel>();
            services.AddTransient<TraverseJournalViewModel>();
            services.AddTransient<MainViewModel>();

            return services;
        }

        /// <summary>
        /// Регистрирует менеджеры приложения
        /// </summary>
        public static IServiceCollection AddManagers(this IServiceCollection services)
        {
            services.AddTransient<BenchmarkManager>();
            services.AddTransient<SharedPointsManager>();
            services.AddTransient<TraverseSystemsManager>();

            return services;
        }
    }
}
