using Microsoft.Extensions.DependencyInjection;
using Nivtropy.Infrastructure.Parsers;
using Nivtropy.Infrastructure.Export;
using Nivtropy.Services.Logging;
using Nivtropy.Application.Services;
using Nivtropy.Services.Dialog;
using Nivtropy.Services.IO;
using Nivtropy.Domain.Services;
using Nivtropy.Services.Visualization;
using Nivtropy.Presentation.ViewModels;
using Nivtropy.Presentation.ViewModels.Managers;
using Nivtropy.Application.Mappers;
using Nivtropy.Application.Commands.Handlers;
using Nivtropy.Application.Queries;
using Nivtropy.Infrastructure.Persistence;
using Nivtropy.Services.Calculation;

namespace Nivtropy.Services
{
    /// <summary>
    /// Методы расширения для регистрации сервисов приложения в DI контейнере.
    /// Организованы по слоям DDD архитектуры:
    /// - Domain Services (бизнес-логика)
    /// - Application Services (use cases, orchestration)
    /// - Infrastructure Services (parsers, persistence, export)
    /// - Presentation Services (UI-specific)
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Регистрирует Infrastructure и Application сервисы (legacy + new)
        /// TODO: Разделить на InfrastructureServices и ApplicationServices после полной миграции
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Сервис логирования
            services.AddSingleton<ILoggerService, FileLoggerService>();

            // Сервис диалогов
            services.AddSingleton<IDialogService, DialogService>();

            // Сервис файловых операций
            services.AddSingleton<IFileService, FileService>();

            // Сервисы парсинга и построения данных
            services.AddSingleton<IDataParser, DatParser>();
            services.AddSingleton<ITraverseBuilder, TraverseBuilder>();

            // Сервисы визуализации
            services.AddSingleton<IProfileVisualizationService, ProfileVisualizationService>();
            services.AddSingleton<ITraverseSystemVisualizationService, TraverseSystemVisualizationService>();
            services.AddSingleton<IGeneratedProfileVisualizationService, GeneratedProfileVisualizationService>();

            // Сервисы статистики и анализа
            services.AddSingleton<IProfileStatisticsService, ProfileStatisticsService>();

            // Сервисы экспорта
            services.AddSingleton<IExportService, TraverseExportService>();

            // Сервисы работы с допусками (moved to Domain)
            services.AddSingleton<IToleranceCalculator, ToleranceCalculator>();

            // Сервис расчёта невязки и допусков (Application layer)
            services.AddSingleton<IClosureCalculationService, ClosureCalculationService>();

            // Legacy calculation services (temporary - will migrate to Domain services)
            services.AddSingleton<ISystemConnectivityService, SystemConnectivityService>();
            services.AddSingleton<ITraverseCorrectionService, TraverseCorrectionService>();

            // Сервис валидации импорта
            services.AddSingleton<IImportValidationService, ImportValidationService>();

            return services;
        }

        /// <summary>
        /// Регистрирует Domain Services и Application Layer (новая архитектура)
        /// </summary>
        public static IServiceCollection AddDomainServices(this IServiceCollection services)
        {
            // Infrastructure: Repository
            services.AddSingleton<INetworkRepository, InMemoryNetworkRepository>();

            // Domain Services (чистая бизнес-логика)
            services.AddSingleton<IHeightPropagator, HeightPropagator>();
            services.AddSingleton<IClosureDistributor, ProportionalClosureDistributor>();

            // Application Layer: Mappers
            services.AddSingleton<INetworkMapper, NetworkMapper>();

            // Application Layer: Handlers (CQRS)
            services.AddSingleton<CalculateHeightsHandler>();
            services.AddSingleton<GetNetworkSummaryHandler>();

            return services;
        }

        /// <summary>
        /// Регистрирует ViewModels приложения
        /// </summary>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            // ViewModels регистрируются как Singleton для обеспечения единого состояния
            services.AddSingleton<DataViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<TraverseCalculationViewModel>();
            services.AddSingleton<TraverseJournalViewModel>();
            services.AddSingleton<TraverseDesignViewModel>();
            services.AddSingleton<DataGeneratorViewModel>();
            services.AddSingleton<MainViewModel>();

            // Новая архитектура (proof-of-concept)
            services.AddSingleton<NetworkViewModel>();

            return services;
        }

        /// <summary>
        /// Регистрирует менеджеры приложения
        /// </summary>
        public static IServiceCollection AddManagers(this IServiceCollection services)
        {
            services.AddSingleton<IBenchmarkManager, BenchmarkManager>();
            services.AddSingleton<ISharedPointsManager, SharedPointsManager>();
            services.AddSingleton<ITraverseSystemsManager, TraverseSystemsManager>();

            return services;
        }
    }
}
