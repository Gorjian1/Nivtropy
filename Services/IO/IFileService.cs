using System.Text;

namespace Nivtropy.Services.IO
{
    /// <summary>
    /// Интерфейс сервиса файловых операций
    /// Абстрагирует файловые операции для тестируемости
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Проверяет существование файла
        /// </summary>
        bool FileExists(string path);

        /// <summary>
        /// Проверяет существование директории
        /// </summary>
        bool DirectoryExists(string path);

        /// <summary>
        /// Читает весь текст из файла
        /// </summary>
        string ReadAllText(string path);

        /// <summary>
        /// Читает весь текст из файла с указанной кодировкой
        /// </summary>
        string ReadAllText(string path, Encoding encoding);

        /// <summary>
        /// Читает все строки из файла
        /// </summary>
        string[] ReadAllLines(string path);

        /// <summary>
        /// Читает все строки из файла с указанной кодировкой
        /// </summary>
        string[] ReadAllLines(string path, Encoding encoding);

        /// <summary>
        /// Записывает текст в файл
        /// </summary>
        void WriteAllText(string path, string contents);

        /// <summary>
        /// Записывает текст в файл с указанной кодировкой
        /// </summary>
        void WriteAllText(string path, string contents, Encoding encoding);

        /// <summary>
        /// Получает расширение файла
        /// </summary>
        string GetExtension(string path);

        /// <summary>
        /// Получает имя файла без пути
        /// </summary>
        string GetFileName(string path);

        /// <summary>
        /// Получает директорию файла
        /// </summary>
        string? GetDirectoryName(string path);
    }
}
