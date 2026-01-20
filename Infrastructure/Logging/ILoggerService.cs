using System;

namespace Nivtropy.Infrastructure.Logging
{
    /// <summary>
    /// Интерфейс сервиса логирования
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// Логирует информационное сообщение
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Логирует предупреждение
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Логирует ошибку
        /// </summary>
        void LogError(string message, Exception? exception = null);

        /// <summary>
        /// Логирует отладочное сообщение
        /// </summary>
        void LogDebug(string message);
    }
}
