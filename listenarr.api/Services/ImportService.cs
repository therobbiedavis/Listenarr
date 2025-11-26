using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public class ImportService : IImportService
    {
        private readonly IDbContextFactory<ListenArrDbContext> _dbFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFileNamingService _fileNamingService;
        private readonly IMetadataService? _metadataService;
        private readonly ILogger<ImportService> _logger;

        public ImportService(
            IDbContextFactory<ListenArrDbContext> dbFactory,
            IServiceScopeFactory scopeFactory,
            IFileNamingService fileNamingService,
            IMetadataService? metadataService,
            ILogger<ImportService> logger)
        {
            _dbFactory = dbFactory;
            _scopeFactory = scopeFactory;
            _fileNamingService = fileNamingService;
            _metadataService = metadataService;
            _logger = logger;
        }

        public async Task<ImportResult> ImportSingleFileAsync(string downloadId, int? audiobookId, string sourcePath, ApplicationSettings settings, CancellationToken ct = default)
        {
            var result = new ImportResult { SourcePath = sourcePath };

            try
            {
                // Build initial metadata context
                var metadata = new AudioMetadata
                {
                    Title = Path.GetFileNameWithoutExtension(sourcePath) ?? "Unknown Title"
                };

                // If download references an audiobook, prefer DB metadata
                AudioMetadata? namingMetadata = null;
                if (audiobookId != null)
                {
                    try
                    {
                        await using var db = await _dbFactory.CreateDbContextAsync(ct);
                        var audiobook = await db.Audiobooks.FindAsync(new object[] { audiobookId.Value }, ct);
                        if (audiobook != null)
                        {
                            namingMetadata = new AudioMetadata
                            {
                                Title = audiobook.Title ?? metadata.Title,
                                Artist = (audiobook.Authors != null && audiobook.Authors.Any()) ? string.Join(", ", audiobook.Authors) : "Unknown Author",
                                AlbumArtist = (audiobook.Authors != null && audiobook.Authors.Any()) ? string.Join(", ", audiobook.Authors) : "Unknown Author",
                                Series = audiobook.Series ?? string.Empty,
                                SeriesPosition = !string.IsNullOrWhiteSpace(audiobook.SeriesNumber) && int.TryParse(audiobook.SeriesNumber, out var sp) ? sp : null,
                                Year = !string.IsNullOrWhiteSpace(audiobook.PublishYear) && int.TryParse(audiobook.PublishYear, out var y) ? y : null,
                            };
                            _logger.LogDebug("ImportSingleFile: Using audiobook metadata for naming (Download {DownloadId}): {Title} by {Artist}", downloadId, namingMetadata.Title, namingMetadata.Artist);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "ImportSingleFile: failed to load audiobook metadata for naming (Download {DownloadId})", downloadId);
                    }
                }

                // Optionally extract file metadata (only when no audiobook naming metadata)
                if (namingMetadata == null && _metadataService != null && File.Exists(sourcePath))
                {
                    try
                    {
                        var extracted = await _metadataService.ExtractFileMetadataAsync(sourcePath);
                        if (extracted != null)
                        {
                            metadata.Title = FirstNonEmpty(metadata.Title, extracted.Title);
                            metadata.Artist = FirstNonEmpty(metadata.Artist, extracted.Artist, extracted.AlbumArtist);
                            metadata.Album = FirstNonEmpty(metadata.Album, extracted.Album);
                            metadata.SeriesPosition ??= extracted.SeriesPosition;
                            metadata.TrackNumber ??= extracted.TrackNumber;
                            metadata.DiscNumber ??= extracted.DiscNumber;
                            metadata.Year ??= extracted.Year;
                            metadata.Bitrate ??= extracted.Bitrate;
                            metadata.Format ??= extracted.Format;
                            _logger.LogDebug("ImportSingleFile: merged extracted metadata for {File}", sourcePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ImportSingleFile: failed to extract metadata from {File}, using defaults", sourcePath);
                    }
                }

                var metadataForNaming = namingMetadata ?? metadata;

                // Base path and filename pattern selection
                string basePathForFile = settings.OutputPath; // default
                string filenamePattern = settings.FileNamingPattern;
                if (audiobookId != null && namingMetadata != null)
                {
                    try
                    {
                        await using var db = await _dbFactory.CreateDbContextAsync(ct);
                        var ab = await db.Audiobooks.FindAsync(new object[] { audiobookId.Value }, ct);
                        if (ab != null && !string.IsNullOrWhiteSpace(ab.BasePath))
                        {
                            basePathForFile = ab.BasePath; // will be combined with filename-only pattern
                            _logger.LogDebug("ImportSingleFile: using audiobook base path for download {DownloadId}: {BasePath}", downloadId, basePathForFile);
                            // For audiobook base path we keep the filename-only pattern
                            filenamePattern = "{Title}";
                        }
                    }
                    catch { /* ignore */ }
                }

                if (string.IsNullOrWhiteSpace(basePathForFile)) basePathForFile = "./completed";
                if (string.IsNullOrWhiteSpace(filenamePattern)) filenamePattern = "{Author}/{Series}/{Title}";

                // build variables
                var variables = new Dictionary<string, object>
                {
                    { "Author", metadataForNaming.Artist ?? "Unknown Author" },
                    { "Series", string.IsNullOrWhiteSpace(metadataForNaming.Series) ? string.Empty : metadataForNaming.Series },
                    { "Title", metadataForNaming.Title ?? "Unknown Title" },
                    { "SeriesNumber", metadataForNaming.SeriesPosition?.ToString() ?? metadataForNaming.TrackNumber?.ToString() ?? string.Empty },
                    { "Year", metadataForNaming.Year?.ToString() ?? string.Empty },
                    { "Quality", (metadataForNaming.Bitrate.HasValue ? metadataForNaming.Bitrate.ToString() + "kbps" : null) ?? metadataForNaming.Format ?? string.Empty },
                    { "DiskNumber", metadataForNaming.DiscNumber?.ToString() ?? string.Empty },
                    { "ChapterNumber", metadataForNaming.TrackNumber?.ToString() ?? string.Empty }
                };

                var patternAllowsSubfolders = filenamePattern.IndexOf("DiskNumber", StringComparison.OrdinalIgnoreCase) >= 0
                    || filenamePattern.IndexOf("ChapterNumber", StringComparison.OrdinalIgnoreCase) >= 0
                    || filenamePattern.IndexOf('/') >= 0
                    || filenamePattern.IndexOf('\\') >= 0;

                // When basepath was explicitly from audiobook and pattern is for audiobook we treat as filename only
                var treatAsFilename = filenamePattern == "{Title}" ? true : !patternAllowsSubfolders;

                var filename = _fileNamingService.ApplyNamingPattern(filenamePattern, variables, treatAsFilename);
                var ext = Path.GetExtension(sourcePath);
                if (!filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) filename += ext;

                if (!patternAllowsSubfolders)
                {
                    try { filename = Path.GetFileName(filename); }
                    catch { filename = Path.GetFileName(sourcePath); }
                }

                var destinationPath = Path.Combine(basePathForFile, filename);

                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destinationPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                // Perform file operation
                try
                {
                    var initialDest = Path.Combine(destDir, Path.GetFileName(sourcePath));
                    var uniqueInitial = FileUtils.GetUniqueDestinationPath(initialDest);

                    var action = settings.CompletedFileAction ?? "Move";
                    if (string.Equals(action, "Copy", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(sourcePath, uniqueInitial, true);
                        result.WasCopied = true;
                    }
                    else
                    {
                        File.Move(sourcePath, uniqueInitial, true);
                        result.WasMoved = true;
                    }

                    // Now apply filename pattern
                    var uniqueFinal = FileUtils.GetUniqueDestinationPath(destinationPath);
                    if (!string.Equals(Path.GetFullPath(uniqueInitial), Path.GetFullPath(uniqueFinal), StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Move(uniqueInitial, uniqueFinal, true); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "ImportSingleFile: failed to rename {Source} -> {Dest}", uniqueInitial, uniqueFinal);
                            uniqueFinal = uniqueInitial; // fallback
                        }
                    }

                    result.FinalPath = uniqueFinal;
                    result.Success = true;

                    // Note: single-file imports do not register the audiobook file immediately here.
                    // Registration and any quality gating is handled by the caller (DownloadService)
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = ex.Message;
                    _logger.LogWarning(ex, "ImportSingleFile: failed file operation for {File}", sourcePath);
                }

            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                _logger.LogWarning(ex, "ImportSingleFile: unexpected failure for {File}", sourcePath);
            }

            return result;

            static string FirstNonEmpty(params string?[] candidates)
            {
                foreach (var c in candidates)
                    if (!string.IsNullOrWhiteSpace(c)) return c!;
                return string.Empty;
            }
        }

        public async Task<List<ImportResult>> ImportFilesFromDirectoryAsync(string downloadId, int? audiobookId, IEnumerable<string> files, ApplicationSettings settings, CancellationToken ct = default)
        {
            var results = new List<ImportResult>();

            try
            {
                // Precompute audiobook and best existing quality to avoid import-order races
                Audiobook? batchAudiobook = null;
                string? bestExisting = null;
                QualityProfile? abProfile = null;

                if (audiobookId != null)
                {
                    try
                    {
                        await using var db = await _dbFactory.CreateDbContextAsync(ct);
                        batchAudiobook = await db.Audiobooks
                            .Include(a => a.QualityProfile)
                            .Include(a => a.Files)
                            .FirstOrDefaultAsync(a => a.Id == audiobookId.Value, ct);

                        abProfile = batchAudiobook?.QualityProfile;

                        if (batchAudiobook != null && batchAudiobook.Files != null && batchAudiobook.Files.Any())
                        {
                            foreach (var f in batchAudiobook.Files)
                            {
                                try
                                {
                                    string q = string.Empty;
                                    if (!string.IsNullOrEmpty(f.Format)) q = f.Format;
                                    if (f.Bitrate.HasValue)
                                    {
                                        var kb = f.Bitrate.Value / 1000;
                                        if (kb >= 320) q = "MP3 320kbps";
                                        else if (kb >= 256) q = "MP3 256kbps";
                                        else if (kb >= 192) q = "MP3 192kbps";
                                        else if (kb >= 128) q = "MP3 128kbps";
                                    }
                                    if (string.IsNullOrEmpty(q) && !string.IsNullOrEmpty(f.Path)) q = DetermineQualityFromMetadata(null, f.Path);

                                    if (string.IsNullOrEmpty(bestExisting)) bestExisting = q;
                                    else if (!string.IsNullOrEmpty(q) && !string.IsNullOrEmpty(bestExisting) && abProfile != null)
                                    {
                                        if (IsQualityBetter(q, bestExisting, abProfile)) bestExisting = q;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "ImportFilesFromDirectory: Failed to load audiobook for batch quality evaluation (DownloadId: {DownloadId})", downloadId);
                    }
                }

                foreach (var file in files)
                {
                    var res = new ImportResult { SourcePath = file };
                    try
                    {
                        var candidateMetadata = (AudioMetadata?)null;
                        if (_metadataService != null)
                        {
                            try { candidateMetadata = await _metadataService.ExtractFileMetadataAsync(file); } catch { candidateMetadata = null; }
                        }

                        var candidateQuality = DetermineQualityFromMetadata(candidateMetadata, file);

                        // If linked to audiobook, decide whether to import based on quality profile
                        if (audiobookId != null && batchAudiobook != null)
                        {
                            try
                            {
                                if (batchAudiobook != null && batchAudiobook.Files != null && batchAudiobook.Files.Any())
                                {
                                    if (!IsQualityBetter(candidateQuality, bestExisting, abProfile))
                                    {
                                        res.Success = false;
                                        res.SkippedReason = $"candidate quality '{candidateQuality}' is not better than existing '{bestExisting}'";
                                        results.Add(res);
                                        _logger.LogInformation("ImportFilesFromDirectory: Skipping import of file {File} for audiobook {AudiobookId} because candidate quality '{Candidate}' is not better than existing '{Existing}'", file, batchAudiobook.Id, candidateQuality, bestExisting);
                                        continue; // skip importing this file
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "ImportFilesFromDirectory: Failed to evaluate quality for multi-file import {File}", file);
                            }
                        }

                        // Determine destination directory (prefer audiobook basepath)
                        string destDirForFile = string.Empty;
                        Audiobook? abForNaming = null;
                        if (audiobookId != null)
                        {
                            try
                            {
                                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                                abForNaming = await db.Audiobooks.FindAsync(new object[] { audiobookId.Value }, ct);
                                if (abForNaming != null && !string.IsNullOrWhiteSpace(abForNaming.BasePath)) destDirForFile = abForNaming.BasePath;
                            }
                            catch { destDirForFile = string.Empty; }
                        }
                        if (string.IsNullOrWhiteSpace(destDirForFile)) destDirForFile = settings.OutputPath ?? "./completed";

                        // Only perform file operations if destination directory exists (do not create)
                        if (!string.IsNullOrEmpty(destDirForFile) && Directory.Exists(destDirForFile))
                        {
                            // Build naming metadata: prefer audiobook metadata when available, otherwise use extracted candidate metadata
                            var namingMetadata = new AudioMetadata();
                            if (abForNaming != null)
                            {
                                namingMetadata.Title = abForNaming.Title ?? Path.GetFileNameWithoutExtension(file);
                                namingMetadata.Artist = (abForNaming.Authors != null && abForNaming.Authors.Any()) ? string.Join(", ", abForNaming.Authors) : string.Empty;
                                namingMetadata.AlbumArtist = namingMetadata.Artist;
                                namingMetadata.Series = abForNaming.Series;
                            }
                            else if (candidateMetadata != null)
                            {
                                namingMetadata = candidateMetadata;
                            }
                            else
                            {
                                namingMetadata.Title = Path.GetFileNameWithoutExtension(file);
                            }

                            var filenamePattern = abForNaming != null ? "{Title}" : settings.FileNamingPattern;
                            if (string.IsNullOrWhiteSpace(filenamePattern))
                                filenamePattern = "{Author}/{Series}/{Title}";

                            var ext = Path.GetExtension(file);

                            var variablesForFile = new Dictionary<string, object>
                            {
                                { "Author", namingMetadata.Artist ?? "Unknown Author" },
                                { "Series", string.IsNullOrWhiteSpace(namingMetadata.Series) ? string.Empty : namingMetadata.Series },
                                { "Title", namingMetadata.Title ?? Path.GetFileNameWithoutExtension(file) },
                                { "SeriesNumber", namingMetadata.SeriesPosition?.ToString() ?? namingMetadata.TrackNumber?.ToString() ?? string.Empty },
                                { "Year", namingMetadata.Year?.ToString() ?? string.Empty },
                                { "Quality", (namingMetadata.Bitrate.HasValue ? namingMetadata.Bitrate.ToString() + "kbps" : null) ?? namingMetadata.Format ?? string.Empty },
                                { "DiskNumber", namingMetadata.DiscNumber?.ToString() ?? string.Empty },
                                { "ChapterNumber", namingMetadata.TrackNumber?.ToString() ?? string.Empty }
                            };

                            var patternAllowsSubfolders = filenamePattern.IndexOf("DiskNumber", StringComparison.OrdinalIgnoreCase) >= 0
                                || filenamePattern.IndexOf("ChapterNumber", StringComparison.OrdinalIgnoreCase) >= 0
                                || filenamePattern.IndexOf('/') >= 0
                                || filenamePattern.IndexOf('\\') >= 0;
                            var treatAsFilename = abForNaming != null ? true : !patternAllowsSubfolders;

                            var filename = _fileNamingService.ApplyNamingPattern(filenamePattern, variablesForFile, treatAsFilename);
                            if (!filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) filename += ext;

                            if (!patternAllowsSubfolders)
                            {
                                try
                                {
                                    var forced = Path.GetFileName(filename);
                                    var invalid = Path.GetInvalidFileNameChars();
                                    var sb = new System.Text.StringBuilder();
                                    foreach (var c in forced)
                                    {
                                        sb.Append(invalid.Contains(c) ? '_' : c);
                                    }
                                    filename = sb.ToString();
                                }
                                catch
                                {
                                    filename = Path.GetFileName(filename);
                                }
                            }

                            var destPathForFile = Path.Combine(destDirForFile, filename);

                            // After generating the target filename, we'll still place the file into
                            // the destination directory first (original filename) then apply
                            // the naming pattern on that destination file so that the file
                            // exists in the destination before any renaming occurs.
                            var initialDest = Path.Combine(destDirForFile, Path.GetFileName(file));
                            var uniqueInitial = FileUtils.GetUniqueDestinationPath(initialDest);

                            var action = settings.CompletedFileAction ?? "Move";
                            if (string.Equals(action, "Copy", StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(file, uniqueInitial, true);
                                _logger.LogInformation("ImportFilesFromDirectory: Copied file {Source} -> {Dest}", file, uniqueInitial);
                                res.WasCopied = true;
                            }
                            else
                            {
                                File.Move(file, uniqueInitial, true);
                                _logger.LogInformation("ImportFilesFromDirectory: Moved file {Source} -> {Dest}", file, uniqueInitial);
                                res.WasMoved = true;
                            }

                            // Now apply the filename pattern on the destination copy/move
                            var uniqueFinal = FileUtils.GetUniqueDestinationPath(destPathForFile);

                            // If the final name differs from the initial unique path, move/rename it
                            if (!string.Equals(Path.GetFullPath(uniqueInitial), Path.GetFullPath(uniqueFinal), StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    File.Move(uniqueInitial, uniqueFinal, true);
                                    _logger.LogInformation("ImportFilesFromDirectory: Renamed/Moved destination file {Source} -> {Final}", uniqueInitial, uniqueFinal);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "ImportFilesFromDirectory: Failed to apply naming/rename on multi-file import for {File}", uniqueInitial);
                                    uniqueFinal = uniqueInitial;
                                }
                            }

                            res.FinalPath = uniqueFinal;
                            res.Success = true;

                            // Register audiobook file if linked
                            if (audiobookId != null)
                            {
                                try
                                {
                                    using var afScope = _scopeFactory.CreateScope();
                                    var audioFileService = afScope.ServiceProvider.GetService<IAudioFileService>()
                                        ?? new AudioFileService(_scopeFactory, afScope.ServiceProvider.GetService<ILogger<AudioFileService>>() ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioFileService>(), afScope.ServiceProvider.GetRequiredService<IMemoryCache>(), afScope.ServiceProvider.GetRequiredService<MetadataExtractionLimiter>());

                                    var created = await audioFileService.EnsureAudiobookFileAsync(audiobookId.Value, res.FinalPath, "download");
                                    res.WasRegisteredToAudiobook = created;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "ImportFilesFromDirectory: Failed to create AudiobookFile for imported file {File}", file);
                                }
                            }
                        }
                        else
                        {
                            res.Success = false;
                            res.Message = "Destination directory does not exist";
                            res.SkippedReason = destDirForFile;
                            _logger.LogWarning("ImportFilesFromDirectory: Destination directory does not exist for multi-file import: {DestDir}. Keeping source file: {Source}", destDirForFile, file);
                        }
                    }
                    catch (Exception ex)
                    {
                        res.Success = false;
                        res.Message = ex.Message;
                        _logger.LogWarning(ex, "ImportFilesFromDirectory: Failed processing file in directory import: {File}", file);
                    }

                    results.Add(res);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportFilesFromDirectory: Failed to import files from directory for download {DownloadId}", downloadId);
            }

            return results;
        }

        public Task<ImportResult> ReprocessExistingFileAsync(string downloadId, int? audiobookId, string sourcePath, ApplicationSettings settings, CancellationToken ct = default)
        {
            // For reprocessing we can reuse ImportSingleFileAsync semantics
            return ImportSingleFileAsync(downloadId, audiobookId, sourcePath, settings, ct);
        }

        // Local helpers - copy from DownloadService's helpers for parity
        private static string DetermineQualityFromMetadata(AudioMetadata? metadata, string path)
        {
            if (metadata != null)
            {
                if (!string.IsNullOrEmpty(metadata.Format)) return metadata.Format;
                if (metadata.Bitrate.HasValue) return metadata.Bitrate.Value + "kbps";
            }

            // Best-effort from filename
            var name = Path.GetFileName(path) ?? string.Empty;
            if (name.IndexOf("320", StringComparison.OrdinalIgnoreCase) >= 0) return "MP3 320kbps";
            if (name.IndexOf("256", StringComparison.OrdinalIgnoreCase) >= 0) return "MP3 256kbps";
            if (name.IndexOf("192", StringComparison.OrdinalIgnoreCase) >= 0) return "MP3 192kbps";
            if (name.IndexOf("128", StringComparison.OrdinalIgnoreCase) >= 0) return "MP3 128kbps";
            return string.Empty;
        }

        private static bool IsQualityBetter(string? candidate, string? existing, QualityProfile? profile)
        {
            // Default conservative: treat unknown as not better
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(existing) || profile == null) return false;
            // Very simple: prefer higher numeric bitrate if present
            bool TryParse(string q, out int kb)
            {
                kb = 0;
                var digits = new string(q.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var d))
                {
                    // normalize: 320 or 320kbps -> 320
                    kb = d;
                    return true;
                }
                return false;
            }

            if (TryParse(candidate, out var candKb) && TryParse(existing, out var exKb))
            {
                return candKb > exKb;
            }

            return !string.Equals(candidate, existing, StringComparison.OrdinalIgnoreCase);
        }
    }
}
