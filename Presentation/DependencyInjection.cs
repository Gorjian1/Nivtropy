using Microsoft.Extensions.DependencyInjection;
using Nivtropy.Infrastructure.Parsers;
using Nivtropy.Application.Export;
using Nivtropy.Infrastructure.Export;
using Nivtropy.Infrastructure.Logging;
using Nivtropy.Application.Services;
using Nivtropy.Presentation.Services.Dialog;
using Nivtropy.Domain.Services;
using Nivtropy.Presentation.Services.Visualization;
using Nivtropy.Presentation.ViewModels;
using Nivtropy.Presentation.ViewModels.Managers;
using Nivtropy.Application.Mappers;
using Nivtropy.Application.Commands.Handlers;
using Nivtropy.Application.Queries;
using Nivtropy.Application.Persistence;
using Nivtropy.Infrastructure.Persistence;

namespace Nivtropy.Presentation
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
        /// Регистрирует Infrastructure и Application сервисы
        /// Domain Services регистрируются отдельно через AddDomainServices()
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Сервис логирования
            services.AddSingleton<ILoggerService, FileLoggerService>();

            // Сервис диалогов
            services.AddSingleton<IDialogService, DialogService>();

            // Сервисы парсинга и построения данных
            services.AddSingleton<IDataParser, DatParser>();
            // ITraverseBuilder больше не регистрируется - он стал implementation detail TraverseCalculationService

            // Сервисы визуализации
            services.AddSingleton<IProfileVisualizationService, ProfileVisualizationService>();
            services.AddSingleton<ITraverseSystemVisualizationService, TraverseSystemVisualizationService>();
            services.AddSingleton<IGeneratedProfileVisualizationService, GeneratedProfileVisualizationService>();

            // Сервисы статистики и анализа
            services.AddSingleton<IProfileStatisticsService, ProfileStatisticsService>();
            services.AddSingleton<IRunAnnotationService, RunAnnotationService>();
            services.AddSingleton<ITraverseCalculationService, TraverseCalculationService>();
            services.AddSingleton<ITraverseCalculationWorkflowService, TraverseCalculationWorkflowService>();
            services.AddSingleton<IDesignCalculationService, DesignCalculationService>();
            services.AddSingleton<INoiseGeneratorService, NoiseGeneratorService>();

            // Сервисы экспорта
            services.AddSingleton<ITraverseExportService, TraverseExportService>();
            services.AddSingleton<INivelorExportService, NivelorExportService>();

            // Сервисы работы с допусками (moved to Domain)
            services.AddSingleton<IToleranceCalculator, ToleranceCalculator>();

            // Сервис расчёта невязки и допусков (Application layer)
            services.AddSingleton<IClosureCalculationService, ClosureCalculationService>();

            // Domain Services (calculation services)
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
            services.AddSingleton<INetworkAdjuster, LeastSquaresNetworkAdjuster>();

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
