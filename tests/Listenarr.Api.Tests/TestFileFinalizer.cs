using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    // Lightweight test fallback for IFileFinalizer used by tests that don't register an IImportService.
    internal class TestFileFinalizer : IFileFinalizer
    {
        private readonly IImportService? _importService;

        public TestFileFinalizer(IImportService? importService)
        {
            _importService = importService;
        }

        public Task<List<ImportResult>> ImportFilesFromDirectoryAsync(string downloadId, int? audiobookId, IEnumerable<string> files, ApplicationSettings settings)
        {
            if (_importService != null)
            {
                return _importService.ImportFilesFromDirectoryAsync(downloadId, audiobookId, files, settings);
            }

            var results = files.Select(f => new ImportResult
            {
                Success = true,
                SourcePath = f,
                FinalPath = f
            }).ToList();

            return Task.FromResult(results);
        }

        public Task<ImportResult> ImportSingleFileAsync(string downloadId, int? audiobookId, string sourcePath, ApplicationSettings settings)
        {
            if (_importService != null)
            {
                return _importService.ImportSingleFileAsync(downloadId, audiobookId, sourcePath, settings);
            }

            var result = new ImportResult
            {
                Success = true,
                SourcePath = sourcePath,
                FinalPath = sourcePath
            };

            return Task.FromResult(result);
        }
    }
}
