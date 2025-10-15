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
                
                job.MarkAsCompleted();
                _logger.LogInformation("Successfully completed job {JobId} for download {DownloadId}", 
                    job.Id, job.DownloadId);
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

        private async Task ProcessJobAsync(DownloadProcessingJob job, IServiceScope scope, CancellationToken cancellationToken)
        {
            switch (job.JobType)
            {
                case ProcessingJobType.MoveOrCopyFile:
                    await ProcessMoveOrCopyJobAsync(job, scope, cancellationToken);
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

                    using var scope = _serviceScopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                    var download = await dbContext.Downloads.FindAsync(job.DownloadId);
                    if (download != null)
                    {
                        metadata.Title = download.Title ?? "Unknown Title";
                        metadata.Artist = download.Artist;
                        metadata.Album = download.Album;
                        job.AddLogEntry($"Using download metadata: {metadata.Title} by {metadata.Artist}");
                    }

                    // Try to extract metadata from file if metadata processing enabled
                    if (metadataService != null)
                    {
                        try
                        {
                            var extractedMetadata = await metadataService.ExtractFileMetadataAsync(sourcePath);
                            if (extractedMetadata != null)
                            {
                                metadata = extractedMetadata;
                                job.AddLogEntry($"Extracted file metadata: {metadata.Title} by {metadata.Artist}");
                            }
                        }
                        catch (Exception ex)
                        {
                            job.AddLogEntry($"Failed to extract metadata: {ex.Message}");
                        }
                    }

                    // Generate path using naming pattern
                    var ext = Path.GetExtension(sourcePath);
                    var generatedPath = await fileNamingService.GenerateFilePathAsync(metadata, null, null, ext);

                    // Preserve subdirectories from the generated path. The naming pattern may include
                    // subfolders (e.g. {Author}/{Series}/...). If the generatedPath is rooted, use it
                    // directly. If it's relative, combine it with the configured OutputPath so subfolders
                    // are retained instead of being stripped to a single filename.
                    if (Path.IsPathRooted(generatedPath))
                    {
                        destinationPath = generatedPath;
                    }
                    else
                    {
                        var outputRoot = settings.OutputPath ?? string.Empty;
                        destinationPath = Path.Combine(outputRoot, generatedPath);
                    }

                    job.AddLogEntry($"Generated destination (preserving subfolders): {destinationPath}");
                }
                else
                {
                    // Simple naming - use original filename in output directory
                    var fileName = Path.GetFileName(sourcePath);
                    destinationPath = Path.Combine(settings.OutputPath, fileName);
                    job.AddLogEntry($"Using simple destination: {destinationPath}");
                }

                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    job.AddLogEntry($"Created directory: {destDir}");
                }

                // Perform file operation if source and destination are different
                if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                {
                    var action = settings.CompletedFileAction ?? "Move";
                    job.AddLogEntry($"Performing {action} operation");

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

                    job.DestinationPath = destinationPath;
                }
                else
                {
                    job.AddLogEntry("Source and destination are the same, no file operation needed");
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