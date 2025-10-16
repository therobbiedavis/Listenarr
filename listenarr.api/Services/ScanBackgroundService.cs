using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    public class ScanBackgroundService : BackgroundService
    {
        private readonly IScanQueueService _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScanBackgroundService> _logger;
        private readonly IHubContext<DownloadHub> _hubContext;

        public ScanBackgroundService(IScanQueueService queue, IServiceScopeFactory scopeFactory, ILogger<ScanBackgroundService> logger, IHubContext<DownloadHub> hubContext)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScanBackgroundService started");
            if (_queue is ScanQueueService sq)
            {
                var reader = sq.Reader;
                _logger.LogInformation("ScanBackgroundService awaiting jobs from queue");
                try
                {
                    await foreach (var job in reader.ReadAllAsync(stoppingToken))
                    {
                        _logger.LogDebug("Dequeued scan job {JobId} from channel", job.Id);
                    try
                    {
                        _logger.LogInformation("Processing scan job {JobId} for audiobook {AudiobookId}", job.Id, job.AudiobookId);
                        // notify clients that job is now processing
                        try
                        {
                            await _hubContext.Clients.All.SendAsync("ScanJobUpdate", new { jobId = job.Id.ToString(), audiobookId = job.AudiobookId, status = "Processing", startedAt = DateTime.UtcNow });
                        }
                        catch { }
                        // update in-memory job status
                        try { _queue.UpdateJobStatus(job.Id, "Processing"); } catch { }
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                        var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();

                        var audiobook = await db.Audiobooks.FindAsync(job.AudiobookId);
                        if (audiobook == null)
                        {
                            _logger.LogWarning("Audiobook {Id} not found for scan job {JobId}", job.AudiobookId, job.Id);
                            continue;
                        }

                        var scanRoot = job.Path;
                        try
                        {
                            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                            var settings = await configService.GetApplicationSettingsAsync();
                            if (string.IsNullOrEmpty(scanRoot)) scanRoot = settings.OutputPath;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to read settings for scan job {JobId}", job.Id);
                        }

                        if (string.IsNullOrEmpty(scanRoot) || !Directory.Exists(scanRoot))
                        {
                            _logger.LogWarning("Scan path not found for job {JobId}: {Path}", job.Id, scanRoot);
                            continue;
                        }

                        // If the audiobook has a stored file path, prefer scanning its containing folder
                        // This ensures a scan requested for a specific audiobook only operates within the
                        // closest folder (e.g. /.../Stephen Graham Jones/Mongrels/) rather than the
                        // parent artist folder.
                        try
                        {
                            if (!string.IsNullOrEmpty(audiobook.FilePath))
                            {
                                var audiobookDir = Path.GetDirectoryName(audiobook.FilePath) ?? string.Empty;
                                if (!string.IsNullOrEmpty(audiobookDir) && Directory.Exists(audiobookDir))
                                {
                                    // Use the audiobook directory as the scan root to avoid scanning siblings
                                    scanRoot = audiobookDir;
                                    _logger.LogDebug("Adjusted scan root to audiobook folder for job {JobId}: {ScanRoot}", job.Id, scanRoot);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to adjust scan root to audiobook folder for job {JobId}", job.Id);
                        }

                        var titleToken = (audiobook.Title ?? string.Empty).Replace("\"", string.Empty).Trim();
                        var authorToken = audiobook.Authors?.FirstOrDefault() ?? string.Empty;

                        var exts = new[] { ".m4b", ".mp3", ".flac", ".ogg", ".opus", ".m4a", ".aac", ".wav" };

                        // Collect candidate audio files first
                        var candidates = new List<string>();
                        // Walk directories iteratively and safely, catching IO/Access exceptions per directory.
                        var dirs = new Stack<string>();
                        dirs.Push(scanRoot);

                        while (dirs.Count > 0)
                        {
                            var dir = dirs.Pop();
                            try
                            {
                                // normalize path to full path to avoid odd relative issues
                                var normalizedDir = Path.GetFullPath(dir);

                                // enumerate files in this directory
                                foreach (var file in Directory.EnumerateFiles(normalizedDir))
                                {
                                    try
                                    {
                                        var ext = Path.GetExtension(file);
                                        if (!exts.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                                        candidates.Add(file);
                                    }
                                    catch (Exception innerFileEx)
                                    {
                                        _logger.LogDebug(innerFileEx, "Skipped file while scanning {Dir}", normalizedDir);
                                        continue;
                                    }
                                }

                                // enqueue subdirectories
                                foreach (var sub in Directory.EnumerateDirectories(normalizedDir))
                                {
                                    dirs.Push(sub);
                                }
                            }
                            catch (System.IO.IOException ioEx)
                            {
                                _logger.LogWarning(ioEx, "IO error while enumerating directory for scan job {JobId}: {Dir}", job.Id, dir);
                                // don't fail the whole job - continue scanning other directories
                                continue;
                            }
                            catch (UnauthorizedAccessException uaEx)
                            {
                                _logger.LogWarning(uaEx, "Access denied while enumerating directory for scan job {JobId}: {Dir}", job.Id, dir);
                                continue;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Unexpected error while enumerating directory for scan job {JobId}: {Dir}", job.Id, dir);
                                continue;
                            }
                        }

                        // Decide which candidates belong to this audiobook.
                        // Strategy: if title/author tokens are empty, accept all candidates.
                        // Otherwise, group candidates by parent directory; if any file or the directory name matches the title/author token, accept the whole directory (useful for disc/chapter sets).
                        var foundFiles = new List<string>();
                        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        if (candidates.Count > 0)
                        {
                            if (string.IsNullOrEmpty(titleToken) && string.IsNullOrEmpty(authorToken))
                            {
                                foreach (var c in candidates) { if (unique.Add(c)) foundFiles.Add(c); }
                            }
                            else
                            {
                                var groups = candidates.GroupBy(f => Path.GetDirectoryName(f) ?? string.Empty);
                                foreach (var group in groups)
                                {
                                    var dirName = Path.GetFileName(group.Key) ?? string.Empty;
                                    var groupHasMatch = group.Any(f =>
                                        (!string.IsNullOrEmpty(titleToken) && Path.GetFileNameWithoutExtension(f).IndexOf(titleToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                        || (!string.IsNullOrEmpty(authorToken) && f.IndexOf(authorToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                        || (!string.IsNullOrEmpty(titleToken) && dirName.IndexOf(titleToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                    );

                                    if (groupHasMatch)
                                    {
                                        foreach (var f in group) { if (unique.Add(f)) foundFiles.Add(f); }
                                    }
                                    else
                                    {
                                        // include files that individually match
                                        foreach (var f in group)
                                        {
                                            var fname = Path.GetFileNameWithoutExtension(f);
                                            if (!string.IsNullOrEmpty(titleToken) && fname.IndexOf(titleToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                if (unique.Add(f)) foundFiles.Add(f);
                                            }
                                            else if (!string.IsNullOrEmpty(authorToken) && f.IndexOf(authorToken, StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                if (unique.Add(f)) foundFiles.Add(f);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var createdFiles = 0;
                        foreach (var filePath in foundFiles)
                        {
                            try
                            {
                                using var afScope = _scopeFactory.CreateScope();
                                var audioFileService = afScope.ServiceProvider.GetRequiredService<IAudioFileService>();
                                var created = await audioFileService.EnsureAudiobookFileAsync(audiobook.Id, filePath, "scan");
                                if (created) createdFiles++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to add file {File} during scan job {JobId}", filePath, job.Id);
                            }
                        }

                        await db.SaveChangesAsync();

                        // Remove AudiobookFile DB rows for files that no longer exist on disk
                        try
                        {
                            var existingFiles = await db.AudiobookFiles
                                .Where(f => f.AudiobookId == audiobook.Id)
                                .ToListAsync();

                            var foundSet = new HashSet<string>(foundFiles, StringComparer.OrdinalIgnoreCase);
                            var toRemove = existingFiles
                                .Where(f => f.Path != null && !foundSet.Contains(f.Path) && !System.IO.File.Exists(f.Path))
                                .ToList();

                            List<object> removedFilesDto = new();
                            if (toRemove.Count > 0)
                            {
                                foreach (var rem in toRemove)
                                {
                                    try
                                    {
                                        removedFilesDto.Add(new { id = rem.Id, path = rem.Path });
                                        db.AudiobookFiles.Remove(rem);
                                        _logger.LogInformation("Removing missing AudiobookFile DB row Id={Id} Path={Path}", rem.Id, rem.Path);

                                        // Add history entry for removed file
                                        var historyEntry = new History
                                        {
                                            AudiobookId = audiobook.Id,
                                            AudiobookTitle = audiobook.Title ?? "Unknown",
                                            EventType = "File Removed",
                                            Message = $"File removed (no longer exists): {Path.GetFileName(rem.Path)}",
                                            Source = "Scan",
                                            Data = JsonSerializer.Serialize(new
                                            {
                                                FilePath = rem.Path,
                                                FileSize = rem.Size,
                                                Format = rem.Format,
                                                Source = rem.Source
                                            }),
                                            Timestamp = DateTime.UtcNow
                                        };
                                        db.History.Add(historyEntry);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to remove AudiobookFile Id={Id} Path={Path}", rem.Id, rem.Path);
                                    }
                                }

                                await db.SaveChangesAsync();

                                // Broadcast a friendly message about removed files so UI can show a notice
                                try
                                {
                                    await _hubContext.Clients.All.SendAsync("FilesRemoved", new { audiobookId = audiobook.Id, removed = removedFilesDto });
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed to broadcast FilesRemoved event for audiobook {AudiobookId}", audiobook.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to reconcile audiobook files after scan job {JobId}", job.Id);
                        }

                        // Handle legacy filePath field migration
                        try
                        {
                            var needsUpdate = false;
                            if (!string.IsNullOrEmpty(audiobook.FilePath))
                            {
                                // Check if the legacy filePath exists
                                if (System.IO.File.Exists(audiobook.FilePath))
                                {
                                    // File exists - check if we already have an AudiobookFile record for it
                                    var existingFileRecord = await db.AudiobookFiles
                                        .FirstOrDefaultAsync(f => f.AudiobookId == audiobook.Id && f.Path == audiobook.FilePath);

                                    if (existingFileRecord == null)
                                    {
                                        // Create AudiobookFile record for the legacy filePath
                                        try
                                        {
                                            using var afScope = _scopeFactory.CreateScope();
                                            var audioFileService = afScope.ServiceProvider.GetRequiredService<IAudioFileService>();
                                            var created = await audioFileService.EnsureAudiobookFileAsync(audiobook.Id, audiobook.FilePath, "scan-legacy");
                                            if (created)
                                            {
                                                _logger.LogInformation("Migrated legacy filePath to AudiobookFile record for audiobook {AudiobookId}: {Path}", audiobook.Id, audiobook.FilePath);
                                                createdFiles++;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to migrate legacy filePath for audiobook {AudiobookId}: {Path}", audiobook.Id, audiobook.FilePath);
                                        }
                                    }
                                }
                                else
                                {
                                    // File doesn't exist - clear the legacy filePath and related fields
                                    audiobook.FilePath = null;
                                    audiobook.FileSize = null;
                                    needsUpdate = true;
                                    _logger.LogInformation("Cleared missing legacy filePath for audiobook {AudiobookId}: {Path}", audiobook.Id, audiobook.FilePath);

                                    // Add history entry for cleared filePath
                                    var historyEntry = new History
                                    {
                                        AudiobookId = audiobook.Id,
                                        AudiobookTitle = audiobook.Title ?? "Unknown",
                                        EventType = "File Removed",
                                        Message = $"Legacy file path cleared (file no longer exists)",
                                        Source = "Scan",
                                        Data = JsonSerializer.Serialize(new
                                        {
                                            FilePath = audiobook.FilePath,
                                            Source = "legacy-migration"
                                        }),
                                        Timestamp = DateTime.UtcNow
                                    };
                                    db.History.Add(historyEntry);
                                }
                            }

                            if (needsUpdate)
                            {
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to handle legacy filePath migration for audiobook {AudiobookId}", audiobook.Id);
                        }

                        // update job status and broadcast completion
                        try { _queue.UpdateJobStatus(job.Id, "Completed"); } catch { }

                        // Detach the previously-tracked audiobook entity so the subsequent query fetches fresh DB state
                        try { db.Entry(audiobook).State = EntityState.Detached; } catch { }
                        var updated = await db.Audiobooks.Include(a => a.Files).FirstOrDefaultAsync(a => a.Id == audiobook.Id);
                        if (updated != null)
                        {
                            // Project to a lightweight DTO to avoid JSON reference cycles when serializing EF tracked entities
                            var audiobookDto = new
                            {
                                id = updated.Id,
                                title = updated.Title,
                                authors = updated.Authors,
                                description = updated.Description,
                                imageUrl = updated.ImageUrl,
                                filePath = updated.FilePath,
                                fileSize = updated.FileSize,
                                runtime = updated.Runtime,
                                monitored = updated.Monitored,
                                quality = updated.Quality,
                                series = updated.Series,
                                seriesNumber = updated.SeriesNumber,
                                tags = updated.Tags,
                                files = updated.Files?.Select(f => new {
                                    id = f.Id,
                                    path = f.Path,
                                    size = f.Size,
                                    durationSeconds = f.DurationSeconds,
                                    format = f.Format,
                                    bitrate = f.Bitrate,
                                    sampleRate = f.SampleRate,
                                    channels = f.Channels,
                                    source = f.Source,
                                    createdAt = f.CreatedAt
                                }).ToList()
                                ,
                                wanted = updated.Monitored && (updated.Files == null || !updated.Files.Any())
                            };

                            await _hubContext.Clients.All.SendAsync("AudiobookUpdate", audiobookDto);
                            await _hubContext.Clients.All.SendAsync("ScanJobUpdate", new { jobId = job.Id.ToString(), audiobookId = job.AudiobookId, status = "Completed", found = foundFiles.Count, created = createdFiles, completedAt = DateTime.UtcNow });
                            _logger.LogInformation("Broadcasted AudiobookUpdate for AudiobookId {AudiobookId} after scan job {JobId}", audiobook.Id, job.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing scan job {JobId}", job.Id);
                        try { _queue.UpdateJobStatus(job.Id, "Failed", ex.Message); } catch { }
                        try { await _hubContext.Clients.All.SendAsync("ScanJobUpdate", new { jobId = job.Id.ToString(), audiobookId = job.AudiobookId, status = "Failed", error = ex.Message, failedAt = DateTime.UtcNow }); } catch { }
                    }
                    }
                    }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("ScanBackgroundService cancellation requested");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in ScanBackgroundService loop");
                }
            }
        }
    }
}
