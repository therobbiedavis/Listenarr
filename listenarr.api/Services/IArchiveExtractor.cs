using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IArchiveExtractor
    {
        /// <summary>
        /// Extracts an archive to a temporary directory and returns the path of the temp directory, or null on failure.
        /// The caller is responsible for deleting the temporary directory when done.
        /// </summary>
        Task<string?> ExtractArchiveToTempDirAsync(string archivePath);

        /// <summary>
        /// Returns true when the provided path appears to be a supported archive type.
        /// </summary>
        bool IsArchive(string filePath);
    }
}