/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Background service that processes the download post-processing queue
    /// </summary>
    public class DownloadProcessingBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DownloadProcessingBackgroundService> _logger;
        private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10); // Check every 10 seconds

        public DownloadProcessingBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DownloadProcessingBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Download Processing Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Ensure any previously completed downloads are enqueued for processing
                    await EnqueueCompletedDownloadsAsync(stoppingToken);

                    await ProcessQueueAsync(stoppingToken);
                    await ProcessRetryJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing download queue");
                }

                try
                {
                    await Task.Delay(_processingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Download Processing Background Service stopped");
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<IDownloadProcessingQueueService>();

            var job = await queueService.GetNextJobAsync();
            if (job == null) return;

            _logger.LogInformation("Processing job {JobId} for download {DownloadId}: {JobType}", 
                job.Id, job.DownloadId, job.JobType);

            // Mark job as processing
            job.Status = ProcessingJobStatus.Processing;
            job.StartedAt = DateTime.UtcNow;
            job.AddLogEntry("Started processing");
            await queueService.UpdateJobAsync(job);

            try
            {
                await ProcessJobAsync(job, scope, cancellationToken);

                // Only mark the job as completed if it is still in Processing state.
                // Some job handlers may set the job to Failed/Retry/Skipped and we should respect that.
                if (job.Status == ProcessingJobStatus.Processing)
                {
                    job.MarkAsCompleted();
                    _logger.LogInformation("Successfully completed job {JobId} for download {DownloadId}", 
                        job.Id, job.DownloadId);
                }
                else
                {
                    _logger.LogInformation("Job {JobId} for download {DownloadId} finished with status {Status}", job.Id, job.DownloadId, job.Status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process job {JobId} for download {DownloadId}: {Error}", 
                    job.Id, job.DownloadId, ex.Message);
                
                job.AddLogEntry($"Processing failed: {ex.Message}");
                job.ScheduleRetry();
            }

            await queueService.UpdateJobAsync(job);
        }

        private async Task ProcessRetryJobsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<IDownloadProcessingQueueService>();

            var retryJobs = await queueService.GetRetryJobsAsync();
            
            foreach (var job in retryJobs)
            {
                _logger.LogInformation("Retrying job {JobId} for download {DownloadId} (attempt {Attempt}/{MaxAttempts})", 
                    job.Id, job.DownloadId, job.RetryCount + 1, job.MaxRetries);

                // Reset job to pending for processing
                job.Status = ProcessingJobStatus.Pending;
                job.ErrorMessage = null;
                job.AddLogEntry($"Retry #{job.RetryCount} scheduled");
                
                await queueService.UpdateJobAsync(job);
            }
        }

        /// <summary>
        /// Find completed downloads that are not yet enqueued for processing and add them to the queue.
        /// This runs briefly each loop to ensure existing completed items are eventually processed.
        /// </summary>
        private async Task EnqueueCompletedDownloadsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                var queueService = scope.ServiceProvider.GetRequiredService<IDownloadProcessingQueueService>();
                var pathMapping = scope.ServiceProvider.GetService<IRemotePathMappingService>();

                // Find recent completed downloads that have not yet been processed into jobs
                var candidates = await dbContext.Downloads
                    .Where(d => d.Status == DownloadStatus.Completed)
                    .OrderByDescending(d => d.CompletedAt)
                    .Take(200)
                    .ToListAsync(cancellationToken);

                foreach (var dl in candidates)
                {
                    try
                    {
                        // Skip if there is already a job for this download pending/processing/retry
                        var existingJobs = await queueService.GetJobsForDownloadAsync(dl.Id);
                        if (existingJobs != null && existingJobs.Any(j => j.Status == ProcessingJobStatus.Pending || j.Status == ProcessingJobStatus.Processing || j.Status == ProcessingJobStatus.Retry))
                        {
                            continue;
                        }

                        // Determine a plausible source path to process
                        var possiblePaths = new List<string?>();
                        if (!string.IsNullOrEmpty(dl.FinalPath)) possiblePaths.Add(dl.FinalPath);
                        if (!string.IsNullOrEmpty(dl.DownloadPath)) possiblePaths.Add(dl.DownloadPath);
                        if (dl.Metadata != null && dl.Metadata.TryGetValue("ClientContentPath", out var clientObj))
                        {
                            var clientPath = clientObj?.ToString();
                            if (!string.IsNullOrEmpty(clientPath)) possiblePaths.Add(clientPath);
                        }

                        string? found = null;

                        foreach (var p in possiblePaths.Where(p => !string.IsNullOrEmpty(p)))
                        {
                            var testPath = p!;

                            // If path mapping service exists, try translating
                            if (pathMapping != null && !string.IsNullOrEmpty(dl.DownloadClientId))
                            {
                                try
                                {
                                    var translated = await pathMapping.TranslatePathAsync(dl.DownloadClientId, testPath);
                                    if (!string.IsNullOrEmpty(translated) && File.Exists(translated))
                                    {
                                        found = translated;
                                        break;
                                    }
                                }
                                catch
                                {
                                    // ignore and fallback to raw path
                                }
                            }

                            if (File.Exists(testPath))
                            {
                                found = testPath;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(found))
                        {
                            // Queue for processing
                            await queueService.QueueDownloadProcessingAsync(dl.Id, found!, dl.DownloadClientId);
                            _logger.LogInformation("Enqueued existing completed download {DownloadId} for processing: {Source}", dl.Id, found);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to consider completed download {DownloadId} for enqueue", dl.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while enqueuing completed downloads");
            }
        }

        private async Task ProcessJobAsync(DownloadProcessingJob job, IServiceScope scope, CancellationToken cancellationToken)
        {
            switch (job.JobType)
            {
                case ProcessingJobType.MoveOrCopyFile:
                    await ProcessMoveOrCopyJobAsync(job, scope, cancellationToken);
                    break;
                case ProcessingJobType.ExtractMetadata:
                    // Older jobs in the queue may use job types that are no longer supported.
                    // Mark them as failed with a helpful message and do not throw to avoid retry storms.
                    job.AddLogEntry("Job type ExtractMetadata is not supported");
                    job.ErrorMessage = "Job type ExtractMetadata is not supported";
                    job.Status = ProcessingJobStatus.Failed;
                    job.CompletedAt = DateTime.UtcNow;
                    break;
                default:
                    throw new NotSupportedException($"Job type {job.JobType} is not supported");
            }
        }

        private async Task ProcessMoveOrCopyJobAsync(DownloadProcessingJob job, IServiceScope scope, CancellationToken cancellationToken)
        {
            var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();
            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var pathMappingService = scope.ServiceProvider.GetService<IRemotePathMappingService>();
            var fileNamingService = scope.ServiceProvider.GetService<IFileNamingService>();
            var metadataService = scope.ServiceProvider.GetService<IMetadataService>();

            job.AddLogEntry($"Starting file processing: {job.SourcePath}");

            if (string.IsNullOrEmpty(job.SourcePath) || !File.Exists(job.SourcePath))
            {
                // Apply path mapping if needed
                var localPath = job.SourcePath ?? "";
                if (pathMappingService != null && !string.IsNullOrEmpty(job.DownloadClientId))
                {
                    try
                    {
                        localPath = await pathMappingService.TranslatePathAsync(job.DownloadClientId, job.SourcePath ?? "");
                        if (!string.Equals(localPath, job.SourcePath, StringComparison.OrdinalIgnoreCase))
                        {
                            job.AddLogEntry($"Applied path mapping: {job.SourcePath} -> {localPath}");
                            job.SourcePath = localPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        job.AddLogEntry($"Path mapping failed: {ex.Message}");
                    }
                }

                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"Source file not found: {localPath}");
                }
            }

            // Get application settings
            var settings = await configService.GetApplicationSettingsAsync();
            job.AddLogEntry($"Retrieved settings - OutputPath: {settings.OutputPath}, EnableMetadataProcessing: {settings.EnableMetadataProcessing}");

            // Process the file using the enhanced logic from ProcessCompletedDownloadAsync
            await ProcessFileWithEnhancedLogicAsync(job, downloadService, settings, fileNamingService, metadataService, cancellationToken);
        }

        private async Task ProcessFileWithEnhancedLogicAsync(
            DownloadProcessingJob job, 
            IDownloadService downloadService, 
            ApplicationSettings settings,
            IFileNamingService? fileNamingService,
            IMetadataService? metadataService,
            CancellationToken cancellationToken)
        {
            var sourcePath = job.SourcePath!;
            var destinationPath = sourcePath;

            // Handle file move/copy operations if configured
            if (!string.IsNullOrEmpty(settings.OutputPath))
            {
                job.AddLogEntry($"Processing with output path: {settings.OutputPath}");

                // Determine destination path based on settings
                if (fileNamingService != null && settings.EnableMetadataProcessing)
                {
                    job.AddLogEntry("Using file naming service for destination path");
                    
                    // Build metadata for naming - get download info from database
                    var metadata = new AudioMetadata { Title = "Unknown Title" };
                    // When possible we'll build a namingMetadata from the linked Audiobook to ensure
                    // audiobook fields are authoritative for naming (avoid extracted tags overwriting them).
                    AudioMetadata? namingMetadata = null;

                    using var scope = _serviceScopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                    var download = await dbContext.Downloads.FindAsync(job.DownloadId);
                    if (download != null)
                    {
                        // Start with values from the download record
                        metadata.Title = download.Title ?? metadata.Title;
                        metadata.Artist = download.Artist ?? string.Empty;
                        metadata.Album = download.Album ?? string.Empty;
                        job.AddLogEntry($"Using download metadata: {metadata.Title} by {metadata.Artist}");

                        // If the download is linked to an Audiobook, prefer its metadata for naming
                        if (download.AudiobookId != null)
                        {
                            try
                            {
                                var audiobook = await dbContext.Audiobooks.FindAsync(download.AudiobookId);
                                if (audiobook != null)
                                {
                                    // Create a naming-only metadata object from the Audiobook. This will be
                                    // used as the authoritative source for file naming fields.
                                    namingMetadata = new AudioMetadata
                                    {
                                        Title = audiobook.Title ?? metadata.Title,
                                        Artist = (audiobook.Authors != null && audiobook.Authors.Any()) ? string.Join(", ", audiobook.Authors) : metadata.Artist,
                                        AlbumArtist = (audiobook.Authors != null && audiobook.Authors.Any()) ? string.Join(", ", audiobook.Authors) : metadata.Artist,
                                        Series = audiobook.Series,
                                    };

                                    job.AddLogEntry($"Using audiobook metadata for naming: {namingMetadata.Title} by {namingMetadata.Artist}");
                                }
                            }
                            catch (Exception ex)
                            {
                                job.AddLogEntry($"Failed to retrieve audiobook metadata: {ex.Message}");
                            }
                        }
                    }

                    // Only extract file metadata for naming when we do NOT have audiobook naming metadata.
                    // If the download is linked to an audiobook (namingMetadata != null) we must not use
                    // file-embedded tags for naming — the audiobook DB entry is authoritative.
                    if (namingMetadata == null && metadataService != null)
                    {
                        try
                        {
                            var extractedMetadata = await metadataService.ExtractFileMetadataAsync(sourcePath);
                            if (extractedMetadata != null)
                            {
                                // No audiobook naming metadata - merge extracted values without overwriting
                                string FirstNonEmpty(params string?[] candidates)
                                {
                                    foreach (var c in candidates)
                                    {
                                        if (!string.IsNullOrWhiteSpace(c)) return c!;
                                    }
                                    return string.Empty;
                                }

                                metadata.Title = FirstNonEmpty(metadata.Title, extractedMetadata.Title, "Unknown Title");
                                metadata.Artist = FirstNonEmpty(metadata.Artist, extractedMetadata.Artist, extractedMetadata.AlbumArtist, metadata.Artist);
                                metadata.Album = FirstNonEmpty(metadata.Album, extractedMetadata.Album, metadata.Album);

                                if (!metadata.SeriesPosition.HasValue && extractedMetadata.SeriesPosition.HasValue)
                                    metadata.SeriesPosition = extractedMetadata.SeriesPosition;
                                if (!metadata.TrackNumber.HasValue && extractedMetadata.TrackNumber.HasValue)
                                    metadata.TrackNumber = extractedMetadata.TrackNumber;
                                if (!metadata.DiscNumber.HasValue && extractedMetadata.DiscNumber.HasValue)
                                    metadata.DiscNumber = extractedMetadata.DiscNumber;
                                if (!metadata.Year.HasValue && extractedMetadata.Year.HasValue)
                                    metadata.Year = extractedMetadata.Year;
                                if (!metadata.Bitrate.HasValue && extractedMetadata.Bitrate.HasValue)
                                    metadata.Bitrate = extractedMetadata.Bitrate;
                                if (string.IsNullOrWhiteSpace(metadata.Format) && !string.IsNullOrWhiteSpace(extractedMetadata.Format))
                                    metadata.Format = extractedMetadata.Format;

                                job.AddLogEntry($"Merged extracted metadata: {metadata.Title} by {metadata.Artist}");
                            }
                        }
                        catch (Exception ex)
                        {
                            job.AddLogEntry($"Failed to extract metadata: {ex.Message}");
                        }
                    }

                    // Generate path using naming pattern
                    var ext = Path.GetExtension(sourcePath);
                    // Use namingMetadata if present (authoritative audiobook fields), otherwise use metadata
                    var metadataForNaming = namingMetadata ?? metadata;

                    // Log naming variables for diagnostics
                    try
                    {
                        var dbgVars = $"Author={(metadataForNaming.Artist ?? "(null)")}, Series={(metadataForNaming.Series ?? "(null)" )}, Title={(metadataForNaming.Title ?? "(null)")}";
                        job.AddLogEntry($"Resolved naming metadata: {dbgVars}");
                    }
                    catch { }

                    // Record the resolved naming metadata on the job for diagnostics
                    try
                    {
                        job.AddLogEntry($"Resolved naming metadata: Author='{metadataForNaming.Artist}', AlbumArtist='{metadataForNaming.AlbumArtist}', Series='{metadataForNaming.Series}', Title='{metadataForNaming.Title}', Year='{metadataForNaming.Year}'");
                    }
                    catch
                    {
                        // ignore logging errors
                    }
                    // Use the overload that accepts an explicit outputPath so the naming service combines correctly
                    var generatedPath = fileNamingService != null
                        ? await fileNamingService.GenerateFilePathAsync(metadataForNaming, settings.OutputPath ?? string.Empty, null, null, ext)
                        : Path.GetFileName(sourcePath);

                    // Preserve subdirectories from the generated path. The naming pattern may include
                    // subfolders (e.g. {Author}/{Series}/...). If the generatedPath is rooted, use it
                    // directly. If it's relative, combine it with the configured OutputPath so subfolders
                    // are retained instead of being stripped to a single filename.
                    _logger.LogDebug("GeneratedPath from FileNamingService: {GeneratedPath} (rooted={IsRooted})", generatedPath, Path.IsPathRooted(generatedPath));

                    // Only allow subfolders if the naming pattern includes DiskNumber or ChapterNumber
                    var fullPattern = settings.FileNamingPattern ?? string.Empty;
                    var patternAllowsSubfolders = fullPattern.IndexOf("DiskNumber", StringComparison.OrdinalIgnoreCase) >= 0
                        || fullPattern.IndexOf("ChapterNumber", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (Path.IsPathRooted(generatedPath))
                    {
                        destinationPath = generatedPath;
                    }
                    else
                    {
                        var outputRoot = settings.OutputPath ?? string.Empty;

                        if (!patternAllowsSubfolders)
                        {
                            // Force filename-only: take only the filename portion of generatedPath and sanitize it
                            var forcedFilename = Path.GetFileName(generatedPath) ?? Path.GetFileName(sourcePath);
                            try
                            {
                                var invalid = Path.GetInvalidFileNameChars();
                                var sb = new System.Text.StringBuilder();
                                foreach (var c in forcedFilename)
                                {
                                    sb.Append(invalid.Contains(c) ? '_' : c);
                                }
                                forcedFilename = sb.ToString();
                            }
                            catch (Exception ex)
                            {
                                job.AddLogEntry($"Failed to sanitize forced filename: {ex.Message}");
                            }

                            destinationPath = Path.Combine(outputRoot, forcedFilename);
                            job.AddLogEntry($"Pattern does not allow subfolders. Forced filename-only destination: {destinationPath}");
                        }
                        else
                        {
                            destinationPath = Path.Combine(outputRoot, generatedPath);
                        }
                    }

                    job.AddLogEntry($"Generated destination (preserving subfolders): {destinationPath}");
                    try
                    {
                        var destDirForCheck = Path.GetDirectoryName(destinationPath) ?? string.Empty;
                        var exists = !string.IsNullOrEmpty(destDirForCheck) && Directory.Exists(destDirForCheck);
                        var root = string.Empty;
                        try { root = Path.GetPathRoot(destDirForCheck) ?? string.Empty; } catch { root = string.Empty; }
                        job.AddLogEntry($"Destination dir exists: {exists} PathRoot={root}");

                        if (!string.IsNullOrEmpty(root) && string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), destDirForCheck.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        {
                            job.AddLogEntry($"Warning: destination dir is a root path: {destDirForCheck}");
                        }
                    }
                    catch (Exception ex)
                    {
                        job.AddLogEntry($"Failed to inspect destination directory: {ex.Message}");
                    }
                }
                else
                {
                    // Simple naming - use original filename in output directory
                    var fileName = Path.GetFileName(sourcePath);
                    destinationPath = Path.Combine(settings.OutputPath, fileName);
                    job.AddLogEntry($"Using simple destination: {destinationPath}");
                }

                // Determine destination directory but DO NOT create it during import/processing
                var destDir = Path.GetDirectoryName(destinationPath);

                // Only perform file operations if the destination directory already exists.
                if (!string.IsNullOrEmpty(destDir) && Directory.Exists(destDir))
                {
                    // Perform file operation if source and destination are different
                    if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                    {
                        var action = settings.CompletedFileAction ?? "Move";
                        job.AddLogEntry($"Performing {action} operation");

                        // Capture source size before operation for later verification (move will remove source)
                        long? sourceSize = null;
                        try
                        {
                            if (File.Exists(sourcePath))
                            {
                                sourceSize = new FileInfo(sourcePath).Length;
                            }
                        }
                        catch (Exception ex)
                        {
                            job.AddLogEntry($"Failed to read source file size: {ex.Message}");
                        }

                        try
                        {
                            if (string.Equals(action, "Copy", StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(sourcePath, destinationPath, true);
                                job.AddLogEntry($"Copied file: {sourcePath} -> {destinationPath}");
                            }
                            else
                            {
                                // Default to Move
                                File.Move(sourcePath, destinationPath, true);
                                job.AddLogEntry($"Moved file: {sourcePath} -> {destinationPath}");
                            }

                            // Verification: ensure destination exists and (if sourceSize available) sizes match
                            if (!File.Exists(destinationPath))
                            {
                                job.AddLogEntry($"Destination not found after {action}: {destinationPath}");
                                job.ErrorMessage = $"Destination not found after {action}";
                                throw new IOException($"Destination not found after {action}: {destinationPath}");
                            }

                            if (sourceSize.HasValue)
                            {
                                try
                                {
                                    var destSize = new FileInfo(destinationPath).Length;
                                    if (destSize != sourceSize.Value)
                                    {
                                        job.AddLogEntry($"Destination size ({destSize}) does not match source size ({sourceSize.Value})");
                                        job.ErrorMessage = $"Destination size mismatch: {destSize} != {sourceSize.Value}";
                                        throw new IOException("Destination size mismatch after file operation");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // If verifying size fails for any reason, record and surface the error
                                    job.AddLogEntry($"Failed to verify destination size: {ex.Message}");
                                    job.ErrorMessage = ex.Message;
                                    throw;
                                }
                            }

                            job.AddLogEntry($"Verified destination: {destinationPath} (size: {new FileInfo(destinationPath).Length})");
                            job.DestinationPath = destinationPath;
                        }
                        catch (Exception ex)
                        {
                            // Ensure the error is recorded on the job so it surfaces in the queue stats/logs
                            job.AddLogEntry($"File operation failed: {ex.Message}");
                            job.ErrorMessage = ex.Message;
                            throw;
                        }
                    }
                    else
                    {
                        job.AddLogEntry("Source and destination are the same, no file operation needed");
                        job.DestinationPath = sourcePath;
                    }
                }
                else
                {
                    // Do not create directories during processing/import. If destination directory doesn't exist,
                    // leave the file in place and log a warning.
                    job.AddLogEntry($"Destination directory does not exist: {destDir}. Skipping file move/copy and keeping source: {sourcePath}");
                    job.ErrorMessage = $"Destination directory does not exist: {destDir}";
                    job.DestinationPath = sourcePath;
                }
            }
            else
            {
                job.AddLogEntry("No output path configured, keeping file at original location");
                job.DestinationPath = sourcePath;
            }

            // Update the download record with the final path
            await downloadService.ProcessCompletedDownloadAsync(job.DownloadId, job.DestinationPath);
            job.AddLogEntry($"Updated download record with final path: {job.DestinationPath}");
        }
    }
}