using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Listenarr.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public class CompletedDownloadProcessor : ICompletedDownloadProcessor
    {
        private readonly Listenarr.Api.Repositories.IDownloadRepository _downloadRepository;
        private readonly IFileFinalizer _fileFinalizer;
        private readonly IConfigurationService _configurationService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IImportService _importService;
        private readonly IArchiveExtractor _archiveExtractor;
        private readonly IHubBroadcaster? _hubBroadcaster;
        private readonly IHubContext<Listenarr.Api.Hubs.DownloadHub> _hubContext;
        private readonly IDownloadQueueService _downloadQueueService;
        private readonly ILogger<CompletedDownloadProcessor> _logger;
        private readonly IAppMetricsService _metrics;

        public CompletedDownloadProcessor(
            Listenarr.Api.Repositories.IDownloadRepository downloadRepository,
            IFileFinalizer fileFinalizer,
            IConfigurationService configurationService,
            IServiceScopeFactory serviceScopeFactory,
            IImportService importService,
            IArchiveExtractor archiveExtractor,
            IDownloadQueueService downloadQueueService,
            IHubContext<Listenarr.Api.Hubs.DownloadHub> hubContext,
            ILogger<CompletedDownloadProcessor> logger,
            IHubBroadcaster? hubBroadcaster = null,
            IAppMetricsService? metrics = null)
        {
            _downloadRepository = downloadRepository;
            _fileFinalizer = fileFinalizer;
            _configurationService = configurationService;
            _serviceScopeFactory = serviceScopeFactory;
            _importService = importService;
            _archiveExtractor = archiveExtractor;
            _downloadQueueService = downloadQueueService;
            _hubContext = hubContext;
            _hubBroadcaster = hubBroadcaster;
            _logger = logger;
            _metrics = metrics ?? new NoopAppMetricsService();
        }

        public async Task ProcessCompletedDownloadAsync(string downloadId, string finalPath)
        {
            _logger.LogInformation("ProcessCompletedDownloadAsync called for {DownloadId} (finalPath: {FinalPath})", downloadId, finalPath);

            try
            {
                var download = await _downloadRepository.FindAsync(downloadId);
                if (download == null)
                {
                    _logger.LogWarning("ProcessCompletedDownloadAsync: download record not found: {DownloadId}", downloadId);
                }
                else
                {
                    download.Status = DownloadStatus.Completed;
                    await _downloadRepository.UpdateAsync(download);
                    _logger.LogInformation("Marked download {DownloadId} as Completed (pre-import)", downloadId);

                    try
                    {
                        var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                        using var scopeSync = scopeFactoryToUse.CreateScope();
                        var scopedDb = scopeSync.ServiceProvider.GetService<ListenArrDbContext>();
                        if (scopedDb != null)
                        {
                            var local = await scopedDb.Downloads.FindAsync(downloadId);
                            if (local != null)
                            {
                                local.Status = DownloadStatus.Completed;
                                _logger.LogDebug("Synchronized Completed status into scoped ListenArrDbContext for {DownloadId}", downloadId);
                            }
                        }
                    }
                    catch (Exception syncEx)
                    {
                        _logger.LogDebug(syncEx, "Failed to synchronize status into scoped ListenArrDbContext (non-fatal)");
                    }
                }

                ApplicationSettings settings = new ApplicationSettings();
                try
                {
                    settings = await _configurationService.GetApplicationSettingsAsync() ?? new ApplicationSettings();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ProcessCompletedDownloadAsync: Failed to load application settings, using defaults");
                    settings = new ApplicationSettings();
                }

                if (string.IsNullOrWhiteSpace(finalPath))
                {
                    _logger.LogWarning("ProcessCompletedDownloadAsync: finalPath is empty for download {DownloadId}", downloadId);
                }
                else
                {
                    if (System.IO.Directory.Exists(finalPath))
                    {
                        try
                        {
                            var files = System.IO.Directory.GetFiles(finalPath, "*", System.IO.SearchOption.AllDirectories);
                            if (files != null && files.Length > 0)
                            {
                                var importResults = await _fileFinalizer.ImportFilesFromDirectoryAsync(downloadId, null, files, settings);
                                _logger.LogInformation("FileFinalizer.ImportFilesFromDirectoryAsync returned {Count} results for download {DownloadId}", importResults?.Count ?? 0, downloadId);
                            // if any successful imports returned final paths, set Download.FinalPath to the first one
                            try
                            {
                                var finalFromDirectory = importResults?.FirstOrDefault(r => r != null && r.Success && !string.IsNullOrWhiteSpace(r.FinalPath))?.FinalPath;
                                if (!string.IsNullOrWhiteSpace(finalFromDirectory))
                                {
                                    var tracked = await _downloadRepository.FindAsync(downloadId);
                                    if (tracked != null)
                                    {
                                        tracked.FinalPath = finalFromDirectory;
                                        await _downloadRepository.UpdateAsync(tracked);
                                        _logger.LogInformation("Updated download {DownloadId} FinalPath to directory import result: {FinalPath}", downloadId, finalFromDirectory);
                                    }

                                    try
                                    {
                                        var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                                        using var afScope = scopeFactoryToUse.CreateScope();
                                        var scopedDb2 = afScope.ServiceProvider.GetService<ListenArrDbContext>();
                                        if (scopedDb2 != null)
                                        {
                                            var local2 = await scopedDb2.Downloads.FindAsync(downloadId);
                                            if (local2 != null)
                                            {
                                                local2.FinalPath = finalFromDirectory;
                                                local2.Status = DownloadStatus.Completed;
                                                _logger.LogDebug("Synchronized FinalPath into scoped ListenArrDbContext for {DownloadId}", downloadId);
                                            }
                                        }
                                    }
                                    catch (Exception sync2Ex)
                                    {
                                        _logger.LogDebug(sync2Ex, "Failed to synchronize FinalPath into scoped ListenArrDbContext (non-fatal)");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to update FinalPath from directory import results (non-fatal)");
                            }

                            // Process archives inside the directory (extract and import)
                            if (settings.ExtractArchives)
                            {
                                var archiveFiles = files.Where(f => _archiveExtractor.IsArchive(f)).ToList();
                                foreach (var archivePath in archiveFiles)
                                {
                                    string? tempDirExtracted = null;
                                    try
                                    {
                                        tempDirExtracted = await _archiveExtractor.ExtractArchiveToTempDirAsync(archivePath);
                                        if (!string.IsNullOrWhiteSpace(tempDirExtracted) && System.IO.Directory.Exists(tempDirExtracted))
                                        {
                                            var extractedFiles = System.IO.Directory.GetFiles(tempDirExtracted, "*", System.IO.SearchOption.AllDirectories);
                                            if (extractedFiles != null && extractedFiles.Length > 0)
                                            {
                                                var extractedResults = await _fileFinalizer.ImportFilesFromDirectoryAsync(downloadId, null, extractedFiles, settings);
                                                _logger.LogInformation("Imported {Count} files extracted from archive {Archive} for download {DownloadId}", extractedResults?.Count ?? 0, archivePath, downloadId);

                                                var finalFromExtracted = extractedResults?.FirstOrDefault(r => r != null && r.Success && !string.IsNullOrWhiteSpace(r.FinalPath))?.FinalPath;
                                                if (!string.IsNullOrWhiteSpace(finalFromExtracted))
                                                {
                                                    var tracked = await _downloadRepository.FindAsync(downloadId);
                                                    if (tracked != null)
                                                    {
                                                        tracked.FinalPath = finalFromExtracted;
                                                        await _downloadRepository.UpdateAsync(tracked);
                                                        _logger.LogInformation("Updated download {DownloadId} FinalPath to extracted import result: {FinalPath}", downloadId, finalFromExtracted);
                                                    }

                                                    try
                                                    {
                                                        var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                                                        using var afScope = scopeFactoryToUse.CreateScope();
                                                        var scopedDb2 = afScope.ServiceProvider.GetService<ListenArrDbContext>();
                                                        if (scopedDb2 != null)
                                                        {
                                                            var local2 = await scopedDb2.Downloads.FindAsync(downloadId);
                                                            if (local2 != null)
                                                            {
                                                                local2.FinalPath = finalFromExtracted;
                                                                local2.Status = DownloadStatus.Completed;
                                                                _logger.LogDebug("Synchronized FinalPath into scoped ListenArrDbContext for {DownloadId}", downloadId);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception sync2Ex)
                                                    {
                                                        _logger.LogDebug(sync2Ex, "Failed to synchronize FinalPath into scoped ListenArrDbContext (non-fatal)");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to extract/import archive {Archive} for download {DownloadId}", archivePath, downloadId);
                                    }
                                    finally
                                    {
                                        if (!string.IsNullOrWhiteSpace(tempDirExtracted) && System.IO.Directory.Exists(tempDirExtracted))
                                        {
                                            try { System.IO.Directory.Delete(tempDirExtracted, true); } catch { }
                                        }
                                    }
                                }
                            }                            }
                            else
                            {
                                _logger.LogInformation("ProcessCompletedDownloadAsync: directory {FinalPath} contains no files to import (DownloadId: {DownloadId})", finalPath, downloadId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "ProcessCompletedDownloadAsync: failed to import files from directory {FinalPath} for download {DownloadId}", finalPath, downloadId);
                        }
                    }
                    else
                    {
                        try
                        {
                            // If configured, and the file is an archive, extract and import contained files
                            if (settings.ExtractArchives && _archiveExtractor.IsArchive(finalPath))
                            {
                                string? tempExtractDir = null;
                                try
                                {
                                    tempExtractDir = await _archiveExtractor.ExtractArchiveToTempDirAsync(finalPath);
                                    if (!string.IsNullOrWhiteSpace(tempExtractDir) && System.IO.Directory.Exists(tempExtractDir))
                                    {
                                        var extractedFiles = System.IO.Directory.GetFiles(tempExtractDir, "*", System.IO.SearchOption.AllDirectories);
                                        if (extractedFiles != null && extractedFiles.Length > 0)
                                        {
                                            var extractedResults = await _fileFinalizer.ImportFilesFromDirectoryAsync(downloadId, null, extractedFiles, settings);
                                            _logger.LogInformation("Imported {Count} files extracted from archive {Archive} for download {DownloadId}", extractedResults?.Count ?? 0, finalPath, downloadId);

                                            var finalFromExtracted = extractedResults?.FirstOrDefault(r => r != null && r.Success && !string.IsNullOrWhiteSpace(r.FinalPath))?.FinalPath;
                                            if (!string.IsNullOrWhiteSpace(finalFromExtracted))
                                            {
                                                var tracked = await _downloadRepository.FindAsync(downloadId);
                                                if (tracked != null)
                                                {
                                                    tracked.FinalPath = finalFromExtracted;
                                                    await _downloadRepository.UpdateAsync(tracked);
                                                    _logger.LogInformation("Updated download {DownloadId} FinalPath to extracted import result: {FinalPath}", downloadId, finalFromExtracted);
                                                }

                                                try
                                                {
                                                    var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                                                    using var afScope = scopeFactoryToUse.CreateScope();
                                                    var scopedDb2 = afScope.ServiceProvider.GetService<ListenArrDbContext>();
                                                    if (scopedDb2 != null)
                                                    {
                                                        var local2 = await scopedDb2.Downloads.FindAsync(downloadId);
                                                        if (local2 != null)
                                                        {
                                                            local2.FinalPath = finalFromExtracted;
                                                            local2.Status = DownloadStatus.Completed;
                                                            _logger.LogDebug("Synchronized FinalPath into scoped ListenArrDbContext for {DownloadId}", downloadId);
                                                        }
                                                    }
                                                }
                                                catch (Exception sync2Ex)
                                                {
                                                    _logger.LogDebug(sync2Ex, "Failed to synchronize FinalPath into scoped ListenArrDbContext (non-fatal)");
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to extract/import archive {FinalPath} for download {DownloadId}", finalPath, downloadId);
                                }
                                finally
                                {
                                    if (!string.IsNullOrWhiteSpace(tempExtractDir) && System.IO.Directory.Exists(tempExtractDir))
                                    {
                                        try { System.IO.Directory.Delete(tempExtractDir, true); } catch { }
                                    }
                                }
                            }
                            else
                            {
                                var importResult = await _fileFinalizer.ImportSingleFileAsync(downloadId, null, finalPath, settings);
                                _logger.LogInformation("FileFinalizer.ImportSingleFileAsync result for download {DownloadId}: Success={Success}, FinalPath={FinalPath}", downloadId, importResult?.Success, importResult?.FinalPath);

                                if (importResult != null && importResult.Success && !string.IsNullOrWhiteSpace(importResult.FinalPath))
                                {
                                    try
                                    {
                                        var tracked = await _downloadRepository.FindAsync(downloadId);
                                        if (tracked != null)
                                        {
                                            tracked.FinalPath = importResult.FinalPath;
                                            await _downloadRepository.UpdateAsync(tracked);
                                            _logger.LogInformation("Updated download {DownloadId} FinalPath to import result: {FinalPath}", downloadId, importResult.FinalPath);
                                        }

                                        try
                                        {
                                            var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                                            using var scopeSync2 = scopeFactoryToUse.CreateScope();
                                            var scopedDb2 = scopeSync2.ServiceProvider.GetService<ListenArrDbContext>();
                                            if (scopedDb2 != null)
                                            {
                                                var local2 = await scopedDb2.Downloads.FindAsync(downloadId);
                                                if (local2 != null)
                                                {
                                                    local2.FinalPath = importResult.FinalPath;
                                                    local2.Status = DownloadStatus.Completed;
                                                    _logger.LogDebug("Synchronized FinalPath into scoped ListenArrDbContext for {DownloadId}", downloadId);
                                                }
                                            }
                                        }
                                        catch (Exception sync2Ex)
                                        {
                                            _logger.LogDebug(sync2Ex, "Failed to synchronize FinalPath into scoped ListenArrDbContext (non-fatal)");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to update Download.FinalPath after import for {DownloadId}", downloadId);
                                    }
                                }

                                if (importResult != null && importResult.Success && !string.IsNullOrWhiteSpace(importResult.FinalPath))
                                {
                                    try
                                    {
                                        var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                                        using var afScope = scopeFactoryToUse.CreateScope();

                                        int? candidateBitrate = null;
                                        try
                                        {
                                            var metadataSvc = afScope.ServiceProvider.GetService<IMetadataService>();
                                            if (metadataSvc != null)
                                            {
                                                var meta = await metadataSvc.ExtractFileMetadataAsync(importResult.FinalPath);
                                                candidateBitrate = meta?.Bitrate;
                                            }
                                        }
                                        catch
                                        {
                                            candidateBitrate = null;
                                        }

                                        int? maxExistingBitrate = null;
                                        try
                                        {
                                            var scopedDb = afScope.ServiceProvider.GetService<ListenArrDbContext>();
                                            if (scopedDb != null && download != null && download.AudiobookId != null)
                                            {
                                                var existing = await scopedDb.AudiobookFiles
                                                    .Where(f => f.AudiobookId == download.AudiobookId && f.Bitrate.HasValue)
                                                    .Select(f => f.Bitrate!.Value)
                                                    .ToListAsync();
                                                if (existing.Any()) maxExistingBitrate = existing.Max();
                                            }
                                        }
                                        catch
                                        {
                                            maxExistingBitrate = null;
                                        }

                                        if (maxExistingBitrate.HasValue && candidateBitrate.HasValue && maxExistingBitrate.Value >= candidateBitrate.Value)
                                        {
                                            _logger.LogInformation("Skipping registration of imported file for audiobook {AudiobookId} because existing quality {Existing} >= candidate {Candidate}", download?.AudiobookId, maxExistingBitrate.Value, candidateBitrate.Value);
                                        }
                                        else
                                        {
                                            var audioFileService = afScope.ServiceProvider.GetService<IAudioFileService>()
                                                ?? ActivatorUtilities.CreateInstance<AudioFileService>(afScope.ServiceProvider,
                                                    scopeFactoryToUse,
                                                    afScope.ServiceProvider.GetService<ILogger<AudioFileService>>() ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioFileService>(),
                                                    afScope.ServiceProvider.GetRequiredService<IMemoryCache>(),
                                                    afScope.ServiceProvider.GetRequiredService<MetadataExtractionLimiter>());

                                            if (download?.AudiobookId != null)
                                            {
                                                // Get the audiobook to calculate relative path (consistent with scan service)
                                                var scopedDb = afScope.ServiceProvider.GetService<ListenArrDbContext>();
                                                var audiobook = scopedDb != null ? await scopedDb.Audiobooks.FindAsync(download.AudiobookId.Value) : null;
                                                
                                                string pathToStore = importResult.FinalPath;
                                                if (audiobook != null && !string.IsNullOrWhiteSpace(audiobook.BasePath))
                                                {
                                                    try
                                                    {
                                                        // Store relative path if the file is within the audiobook's base path
                                                        pathToStore = Path.GetRelativePath(audiobook.BasePath, importResult.FinalPath);
                                                        _logger.LogDebug("Converted absolute path to relative for audiobook {AudiobookId}: {Absolute} -> {Relative}", 
                                                            download.AudiobookId, importResult.FinalPath, pathToStore);
                                                    }
                                                    catch (Exception rpEx)
                                                    {
                                                        _logger.LogDebug(rpEx, "Could not calculate relative path, using absolute");
                                                        pathToStore = importResult.FinalPath;
                                                    }
                                                }
                                                
                                                var created = await audioFileService.EnsureAudiobookFileAsync(download.AudiobookId.Value, pathToStore, "download");
                                                if (created)
                                                {
                                                    _logger.LogInformation("Registered imported file to audiobook {AudiobookId}: {Path}", download.AudiobookId, pathToStore);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "ProcessCompletedDownloadAsync: failed to register imported single file to audiobook for download {DownloadId}", downloadId);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "ProcessCompletedDownloadAsync: failed to import single file {FinalPath} for download {DownloadId}", finalPath, downloadId);
                        }
                    }
                }

                // Cleanup from download client if configured
                try
                {
                    if (download != null && !string.IsNullOrWhiteSpace(download.DownloadClientId))
                    {
                        var scopeFactoryToUse = (_importService as ImportService)?.ScopeFactory ?? _serviceScopeFactory;
                        using var cleanupScope = scopeFactoryToUse.CreateScope();
                        var configService = cleanupScope.ServiceProvider.GetService<IConfigurationService>();
                        var downloadClientGateway = cleanupScope.ServiceProvider.GetService<IDownloadClientGateway>();
                        
                        if (configService != null && downloadClientGateway != null)
                        {
                            var clientConfig = await configService.GetDownloadClientConfigurationAsync(download.DownloadClientId);
                            if (clientConfig != null && !string.IsNullOrEmpty(clientConfig.RemoveCompletedDownloads) && 
                                clientConfig.RemoveCompletedDownloads != "none")
                            {
                                bool deleteFiles = clientConfig.RemoveCompletedDownloads == "remove_and_delete";
                                var removed = await downloadClientGateway.RemoveAsync(clientConfig, download.Id, deleteFiles);
                                
                                if (removed)
                                {
                                    _logger.LogInformation("Removed download {DownloadId} from client {ClientName} (deleteFiles={DeleteFiles})", 
                                        download.Id, clientConfig.Name, deleteFiles);
                                    
                                    // Log to history
                                    var historyRepo = cleanupScope.ServiceProvider.GetService<IHistoryRepository>();
                                    if (historyRepo != null)
                                    {
                                        var historyEntry = new Listenarr.Domain.Models.History
                                        {
                                            AudiobookId = download.AudiobookId,
                                            AudiobookTitle = download.Title,
                                            EventType = "Imported",
                                            Message = $"Automatically imported and removed from {clientConfig.Name}. Files deleted: {deleteFiles}",
                                            Source = "AutoImport",
                                            Timestamp = DateTime.UtcNow,
                                            NotificationSent = false,
                                            Data = System.Text.Json.JsonSerializer.Serialize(new { 
                                                DownloadId = download.Id,
                                                ClientName = clientConfig.Name,
                                                ClientType = clientConfig.Type,
                                                FilesDeleted = deleteFiles,
                                                FinalPath = download.FinalPath
                                            })
                                        };
                                        await historyRepo.AddAsync(historyEntry);
                                        _logger.LogInformation("Added history entry for automatic import of {DownloadId}", downloadId);
                                        
                                        // Send notification
                                        try
                                        {
                                            var notificationService = cleanupScope.ServiceProvider.GetService<INotificationService>();
                                            if (notificationService != null)
                                            {
                                                var webhooks = await configService.GetWebhookConfigurationsAsync();
                                                foreach (var webhook in webhooks.Where(w => w.IsEnabled && w.Triggers.Contains("Imported")))
                                                {
                                                    await notificationService.SendNotificationAsync(
                                                        "Imported",
                                                        new {
                                                            AudiobookTitle = download.Title,
                                                            DownloadClient = clientConfig.Name,
                                                            FilePath = download.FinalPath,
                                                            RemovedFromClient = true,
                                                            FilesDeleted = deleteFiles,
                                                            Timestamp = DateTime.UtcNow
                                                        },
                                                        webhook.Url,
                                                        webhook.Triggers
                                                    );
                                                }
                                                
                                                // Mark notification as sent
                                                historyEntry.NotificationSent = true;
                                                await historyRepo.UpdateAsync(historyEntry);
                                            }
                                        }
                                        catch (Exception notifyEx)
                                        {
                                            _logger.LogWarning(notifyEx, "Failed to send import notification for {DownloadId}", downloadId);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to remove download {DownloadId} from client {ClientName}", 
                                        download.Id, clientConfig.Name);
                                }
                            }
                        }
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Error during post-import cleanup for {DownloadId}", downloadId);
                }

                try
                {
                    var currentQueue = await _downloadQueueService.GetQueueAsync();
                    if (_hubBroadcaster != null)
                    {
                        await _hubBroadcaster.BroadcastQueueUpdateAsync(currentQueue);
                        _logger.LogInformation("Broadcasted QueueUpdate via IHubBroadcaster after processing download {DownloadId}", downloadId);
                    }
                    else
                    {
                        await _hubContext.Clients.All.SendAsync("QueueUpdate", currentQueue);
                        try
                        {
                            var clientProxy = _hubContext?.Clients?.All;
                            if (clientProxy != null)
                            {
                                await clientProxy.SendCoreAsync("QueueUpdate", new object[] { currentQueue }, System.Threading.CancellationToken.None);
                            }
                        }
                        catch (Exception exInner)
                        {
                            _logger.LogDebug(exInner, "Direct SendCoreAsync for QueueUpdate failed (non-fatal)");
                        }

                        _logger.LogInformation("Broadcasted QueueUpdate after processing download {DownloadId}", downloadId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast QueueUpdate after processing download {DownloadId}", downloadId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ProcessCompletedDownloadAsync for {DownloadId}", downloadId);
            }
        }
    }
}
