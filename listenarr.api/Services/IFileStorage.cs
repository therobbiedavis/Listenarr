// csharp
using System.Threading;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IFileStorage
    {
        Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
        Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
        bool FileExists(string path);
        void CreateDirectory(string path);
        void DeleteFile(string path);
        void DeleteDirectory(string path, bool recursive);
    }
}
