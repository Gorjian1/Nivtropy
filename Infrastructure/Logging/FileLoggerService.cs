using System;
using System.IO;
using System.Text;

namespace Nivtropy.Infrastructure.Logging
{
    /// <summary>
    /// Реализация сервиса логирования с записью в файл
    /// </summary>
    public class FileLoggerService : ILoggerService
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _lock = new();
        private readonly bool _includeDebug;

        public FileLoggerService(bool includeDebug = false)
        {
            _includeDebug = includeDebug;
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nivtropy",
                "logs");

            EnsureLogDirectoryExists();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(_logDirectory, $"nivtropy_{timestamp}.log");
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null
                ? $"{message}\nException: {exception.GetType().Name}: {exception.Message}\nStackTrace: {exception.StackTrace}"
                : message;

            WriteLog("ERROR", fullMessage);
        }

        public void LogDebug(string message)
        {
            if (_includeDebug)
            {
                WriteLog("DEBUG", message);
            }
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}";

                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Ошибки логирования не должны прерывать работу приложения
            }
        }

        private void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                // Игнорируем ошибки создания директории
            }
        }

        /// <summary>
        /// Очищает старые лог-файлы (старше указанного количества дней)
        /// </summary>
        public void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, "nivtropy_*.log");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Игнорируем ошибки удаления отдельных файлов
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки очистки логов
            }
        }
    }
}
