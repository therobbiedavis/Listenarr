using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services
{
    public interface IFileFinalizer
    {
        Task<List<ImportResult>> ImportFilesFromDirectoryAsync(string downloadId, int? audiobookId, IEnumerable<string> files, ApplicationSettings settings);
        Task<ImportResult> ImportSingleFileAsync(string downloadId, int? audiobookId, string sourcePath, ApplicationSettings settings);
    }
}
