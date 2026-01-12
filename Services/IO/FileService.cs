using System.IO;
using System.Text;

namespace Nivtropy.Services.IO
{
    /// <summary>
    /// Реализация сервиса файловых операций через System.IO
    /// </summary>
    public class FileService : IFileService
    {
        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public string ReadAllText(string path, Encoding encoding) => File.ReadAllText(path, encoding);

        public string[] ReadAllLines(string path) => File.ReadAllLines(path);

        public string[] ReadAllLines(string path, Encoding encoding) => File.ReadAllLines(path, encoding);

        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

        public void WriteAllText(string path, string contents, Encoding encoding) => File.WriteAllText(path, contents, encoding);

        public string GetExtension(string path) => Path.GetExtension(path);

        public string GetFileName(string path) => Path.GetFileName(path);

        public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    }
}
