using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Listenarr.Api.Services
{
    public class ArchiveExtractor : IArchiveExtractor
    {
        private readonly ILogger<ArchiveExtractor> _logger;
        private static readonly string[] KnownArchiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz" };

        public ArchiveExtractor(ILogger<ArchiveExtractor>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ArchiveExtractor>.Instance;
        }

        public bool IsArchive(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            var ext = Path.GetExtension(filePath);
            return KnownArchiveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<string?> ExtractArchiveToTempDirAsync(string archivePath)
        {
            try
            {
                if (!File.Exists(archivePath)) return null;
                if (!IsArchive(archivePath)) return null;

                var tmp = Path.Combine(Path.GetTempPath(), "listenarr-extract", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tmp);

                // Use SharpCompress to extract safely
                using var archive = ArchiveFactory.Open(archivePath);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    try
                    {
                        var destPath = Path.Combine(tmp, entry.Key.Replace('\\', Path.DirectorySeparatorChar));
                        var destDir = Path.GetDirectoryName(destPath) ?? string.Empty;
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                        entry.WriteToFile(destPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                    }
                    catch (Exception exEntry)
                    {
                        _logger.LogDebug(exEntry, "ArchiveExtractor: failed to extract entry {Entry} from archive {Archive}", entry.Key, archivePath);
                    }
                }

                return await Task.FromResult(tmp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ArchiveExtractor: failed to extract archive {Archive}", archivePath);
                return null;
            }
        }
    }
}






























