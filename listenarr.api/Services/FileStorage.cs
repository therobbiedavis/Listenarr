// csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public class FileStorage : IFileStorage
    {
        public async Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(path, contents ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Synchronous move is fine here; wrap in Task for interface contract.
            File.Move(sourcePath, destinationPath, overwrite: true);
            return Task.CompletedTask;
        }

        public bool FileExists(string path) => File.Exists(path);

        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
            }
        }
    }
}
