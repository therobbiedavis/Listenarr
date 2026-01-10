using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Listenarr.Api.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public class DownloadQueueService : IDownloadQueueService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfigurationService _configurationService;
        private readonly IDownloadRepository _downloadRepository;
        private readonly IDownloadProcessingJobRepository _downloadProcessingJobRepository;
        private readonly IDownloadClientGateway _clientGateway;
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAppMetricsService _metrics;
        private readonly ILogger<DownloadQueueService> _logger;

        private const int QueueCacheExpirationSeconds = 10;
        private const int ClientStatusCacheExpirationSeconds = 30;
        // Don't purge downloads that were added less than this many seconds ago to avoid races with client indexing
        private const int PurgeDelaySeconds = 30;

        public DownloadQueueService(
            IMemoryCache cache,
            IConfigurationService configurationService,
            IDownloadRepository downloadRepository,
            IDownloadProcessingJobRepository downloadProcessingJobRepository,
            IDownloadClientGateway clientGateway,
            IRemotePathMappingService pathMappingService,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IAppMetricsService metrics,
            ILogger<DownloadQueueService> logger)
        {
            _cache = cache;
            _configurationService = configurationService;
            _downloadRepository = downloadRepository;
            _downloadProcessingJobRepository = downloadProcessingJobRepository;
            _clientGateway = clientGateway;
            _pathMappingService = pathMappingService;
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _metrics = metrics ?? new NoopAppMetricsService();
            _logger = logger;
        }

        public async Task<List<QueueItem>> GetQueueAsync()
        {
            var queueItems = new List<QueueItem>();

            var downloadClients = await _cache.GetOrCreateAsync("DownloadClients", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(QueueCacheExpirationSeconds);
                return await _configurationService.GetDownloadClientConfigurationsAsync();
            }) ?? new List<DownloadClientConfiguration>();

            var enabledClients = downloadClients.Where(c => c.IsEnabled).ToList();

            // Build listenarrDownloads list using repository
            List<Download> listenarrDownloads;
            {
                var allDownloads = await _downloadRepository.GetAllAsync();
                _logger.LogInformation("Found {TotalDownloads} downloads (including failed)", allDownloads.Count);

                var ddlDownloads = allDownloads.Where(d => d.DownloadClientId == "DDL").ToList();
                var ddlToShow = new List<Download>();

                if (ddlDownloads.Any())
                {
                    var ddlCompleted = ddlDownloads.Where(d => d.Status == DownloadStatus.Completed).ToList();
                    if (ddlCompleted.Any())
                    {
                        var completedIds = ddlCompleted.Select(d => d.Id).ToList();
                        var pendingJobs = await _downloadProcessingJobRepository.GetPendingDownloadIdsAsync(completedIds);
                        var allJobDownloads = await _downloadProcessingJobRepository.GetAllJobDownloadIdsAsync(completedIds);

                        var ddlCompletedToShow = ddlCompleted
                            .Where(d => pendingJobs.Contains(d.Id) || !allJobDownloads.Contains(d.Id))
                            .ToList();

                        ddlToShow.AddRange(ddlCompletedToShow);
                        _logger.LogInformation("DDL pending jobs count: {PendingJobs}, All job downloads count: {AllJobs}, DDL completed to show: {CompletedToShow}",
                            pendingJobs.Count, allJobDownloads.Count, ddlCompletedToShow.Count);
                    }

                    ddlToShow.AddRange(ddlDownloads.Where(d => d.Status != DownloadStatus.Completed && d.Status != DownloadStatus.Moved));
                }

                var externalDownloads = allDownloads
                    .Where(d => d.DownloadClientId != "DDL" && d.Status != DownloadStatus.Completed && d.Status != DownloadStatus.Moved)
                    .ToList();

                listenarrDownloads = ddlToShow.Concat(externalDownloads).ToList();
                _logger.LogDebug("Final filtering result: {FinalCount} downloads to include in queue filtering ({DdlCount} DDL, {ExternalCount} external)",
                    listenarrDownloads.Count, ddlToShow.Count, externalDownloads.Count);
            }

            // Application settings cache
            ApplicationSettings? appSettings = await _cache.GetOrCreateAsync("ApplicationSettings", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ClientStatusCacheExpirationSeconds);
                try
                {
                    return await _configurationService.GetApplicationSettingsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to load application settings while building queue (non-fatal)");
                    return null;
                }
            });

            var includeCompletedExternal = appSettings != null && appSettings.ShowCompletedExternalDownloads;

            foreach (var client in enabledClients)
            {
                try
                {
                    var clientQueue = await _clientGateway.GetQueueAsync(client);

                    _logger.LogInformation("Before filtering - Client {ClientName} has {TotalItems} queue items", client.Name ?? client.Id, clientQueue.Count);
                    _logger.LogInformation("Database has {DatabaseItems} Listenarr downloads for filtering", listenarrDownloads.Count);

                    // Filter queue to Listenarr downloads
                    var initialFiltered = clientQueue.Where(queueItem =>
                        listenarrDownloads.Any(download =>
                        {
                            var idMatch = download.Id == queueItem.Id;
                            // Also check if queue item ID matches stored torrent hash (any client)
                            var hashMatch = false;
                            try
                            {
                                if (download.Metadata != null && download.Metadata.TryGetValue("TorrentHash", out var hashObj))
                                {
                                    var storedHash = hashObj?.ToString();
                                    if (!string.IsNullOrEmpty(storedHash))
                                    {
                                        hashMatch = storedHash.Equals(queueItem.Id, StringComparison.OrdinalIgnoreCase);
                                    }
                                }
                            }
                            catch { hashMatch = false; }

                            var titleMatch = false;
                            try
                            {
                                if (!string.IsNullOrEmpty(download.Title) && !string.IsNullOrEmpty(queueItem.Title))
                                {
                                    titleMatch = AreTitlesSimilar(download.Title, queueItem.Title);
                                }
                            }
                            catch { titleMatch = false; }

                            var clientMatch = download.DownloadClientId == client.Id;
                            var overallMatch = clientMatch && (idMatch || hashMatch || titleMatch);
                            return overallMatch;
                        })
                    ).ToList();

                    var mappedFiltered = new List<QueueItem>();
                    // Track original client-provided ids that were mapped to Listenarr downloads
                    var mappedClientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var queueItem in initialFiltered)
                    {
                        try
                        {
                            var originalId = queueItem.Id;

                            var matchedDownload = listenarrDownloads.FirstOrDefault(download =>
                                download.DownloadClientId == client.Id && (
                                    download.Id == originalId ||
                                    (download.Metadata != null && download.Metadata.TryGetValue("TorrentHash", out var h) && (h?.ToString() ?? string.Empty).Equals(originalId, StringComparison.OrdinalIgnoreCase)) ||
                                    (!string.IsNullOrEmpty(download.Title) && !string.IsNullOrEmpty(queueItem.Title) && AreTitlesSimilar(download.Title, queueItem.Title))
                                )
                            );

                            if (matchedDownload != null)
                            {
                                mappedClientIds.Add(originalId);
                                queueItem.Id = matchedDownload.Id;

                                // If the client reports this item as completed, mark the Listenarr DB download as Completed so
                                // the UI does not show a duplicate 'queued' entry for the same torrent.
                                try
                                {
                                    if ((queueItem.Status ?? string.Empty).Equals("completed", System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        var dbDownload = await _downloadRepository.FindAsync(matchedDownload.Id);
                                        if (dbDownload != null && dbDownload.Status != DownloadStatus.Completed && dbDownload.Status != DownloadStatus.Moved)
                                        {
                                            dbDownload.Status = DownloadStatus.Completed;
                                            dbDownload.Progress = 100M;
                                            dbDownload.DownloadedSize = dbDownload.TotalSize;
                                            await _downloadRepository.UpdateAsync(dbDownload);
                                            _logger.LogInformation("Marked Listenarr download {DownloadId} as Completed due to client observation", matchedDownload.Id);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed to update DB status for matched download {DownloadId} (non-fatal)", matchedDownload.Id);
                                }
                            }

                            mappedFiltered.Add(queueItem);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error mapping filtered queue item to Listenarr download");
                            mappedFiltered.Add(queueItem);
                        }
                    }

                    queueItems.AddRange(mappedFiltered);


                    if (includeCompletedExternal)
                    {
                        var existingIds = queueItems.Select(q => q.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

                        // Exclude completed items that correspond to original client ids that were already mapped to Listenarr downloads
                        // (mappedClientIds is populated above while mapping)
                        try { existingIds.UnionWith(mappedClientIds); } catch { }

                        var unmatchedCompleted = clientQueue
                            .Where(q => (q.Status ?? string.Empty).Equals("completed", StringComparison.OrdinalIgnoreCase))
                            .Where(q => !existingIds.Contains(q.Id))
                            .Where(q => !listenarrDownloads.Any(d => d.DownloadClientId == client.Id && (
                                d.Id == q.Id ||
                                (d.Metadata != null && d.Metadata.TryGetValue("TorrentHash", out var hh) && (hh?.ToString() ?? string.Empty).Equals(q.Id, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(d.Title) && !string.IsNullOrEmpty(q.Title) && AreTitlesSimilar(d.Title, q.Title))
                            )))
                            .ToList();

                        foreach (var uc in unmatchedCompleted)
                        {
                            var clientName = client.Name ?? uc.DownloadClient ?? client.Id;
                            var clientType = client.Type?.ToLowerInvariant() ?? uc.DownloadClientType ?? "external";

                            if (existingIds.Contains(uc.Id)) continue;

                            queueItems.Add(new QueueItem
                            {
                                Id = uc.Id,
                                Title = uc.Title ?? "Unknown",
                                Quality = uc.Quality ?? "Unknown",
                                Status = "completed",
                                Progress = 100,
                                Size = uc.Size,
                                Downloaded = uc.Downloaded,
                                DownloadSpeed = 0,
                                Eta = null,
                                DownloadClient = clientName,
                                DownloadClientId = client.Id,
                                DownloadClientType = clientType,
                                AddedAt = uc.AddedAt,
                                CanPause = false,
                                CanRemove = true,
                                RemotePath = uc.RemotePath,
                                LocalPath = uc.LocalPath,
                                ContentPath = uc.ContentPath,
                                Seeders = uc.Seeders,
                                Leechers = uc.Leechers,
                                Ratio = uc.Ratio
                            });

                            existingIds.Add(uc.Id);
                        }
                    }

                    _logger.LogDebug("Client {ClientName}: {TotalItems} total items, {FilteredItems} Listenarr items", client.Name, clientQueue.Count, mappedFiltered.Count);

                    // Purge orphaned downloads
                    try
                    {
                        var clientDownloads = listenarrDownloads.Where(d => d.DownloadClientId == client.Id).ToList();
                        var mappedDownloadIds = mappedFiltered.Select(q => q.Id).ToHashSet();
                        var orphanedDownloads = clientDownloads.Where(d => !mappedDownloadIds.Contains(d.Id)).ToList();

                        if (orphanedDownloads.Any())
                        {
                            // Don't purge NZBGet downloads - they move to history immediately and need CompletedDownloadProcessor
                            var toPurge = orphanedDownloads.Where(d => client.Type?.ToLowerInvariant() != "nzbget").ToList();
                            
                            if (toPurge.Count < orphanedDownloads.Count)
                            {
                                _logger.LogDebug("Skipped purge of {SkippedCount} NZBGet downloads (may be in history)", 
                                    orphanedDownloads.Count - toPurge.Count);
                            }

                            // Before purging, avoid purging downloads that were just created (race with client indexing)
                            try
                            {
                                var now = DateTime.UtcNow;
                                var recentlyAdded = toPurge.Where(d => (now - d.StartedAt).TotalSeconds < PurgeDelaySeconds).ToList();
                                if (recentlyAdded.Any())
                                {
                                    _logger.LogDebug("Deferring purge of {Count} recently-added downloads (added < {Seconds}s) for client {ClientName}", recentlyAdded.Count, PurgeDelaySeconds, client.Name ?? client.Id);
                                    toPurge = toPurge.Except(recentlyAdded).ToList();
                                }

                                for (int attempt = 1; attempt <= 5 && toPurge.Any(); attempt++)
                                {
                                    _logger.LogDebug("Re-checking queue before purge (attempt {Attempt}) for client {ClientName}", attempt, client.Name ?? client.Id);
                                    try { await Task.Delay(500 * attempt); } catch { }

                                    var refreshedQueue = await _clientGateway.GetQueueAsync(client);
                                    var refreshedIds = new HashSet<string>(refreshedQueue.Select(q => q.Id), StringComparer.OrdinalIgnoreCase);

                                    // Also prepare normalized titles and sizes for fuzzy matching
                                    var refreshedTitles = refreshedQueue.Where(q => !string.IsNullOrEmpty(q.Title)).Select(q => q.Title).ToList();
                                    var refreshedSizes = refreshedQueue.Where(q => q.Size > 0).Select(q => q.Size).ToList();

                                    // Remove any orphaned download that now appears in refreshed queue (by id, TorrentHash, or similar title/size match)
                                    toPurge = toPurge.Where(d =>
                                    {
                                        try
                                        {
                                            // Exact id match or TorrentHash match keeps it in the queue
                                            if (refreshedIds.Contains(d.Id)) return false;
                                            if (d.Metadata != null && d.Metadata.TryGetValue("TorrentHash", out var hashObj))
                                            {
                                                var hashStr = hashObj?.ToString() ?? string.Empty;
                                                if (!string.IsNullOrEmpty(hashStr) && refreshedIds.Contains(hashStr)) return false;
                                            }

                                            // Also consider title similarity and size as a heuristic match (helps with Deluge timing/ID differences)
                                            if (!string.IsNullOrEmpty(d.Title))
                                            {
                                                var titleMatch = refreshedQueue.Any(q => !string.IsNullOrEmpty(q.Title) && AreTitlesSimilar(d.Title, q.Title));
                                                if (titleMatch)
                                                {
                                                    // If sizes are known, prefer matching sizes as well when available
                                                    var sizeMatch = refreshedQueue.Any(q => q.Size > 0 && d.TotalSize > 0 && q.Size == d.TotalSize);
                                                    if (sizeMatch || refreshedQueue.Any(q => !string.IsNullOrEmpty(q.Title) && AreTitlesSimilar(d.Title, q.Title)))
                                                        return false;
                                                }
                                            }

                                            return true;
                                        }
                                        catch { return true; }
                                    }).ToList();

                                    _logger.LogDebug("After recheck attempt {Attempt}, toPurge count is {Count}", attempt, toPurge.Count);
                                }
                            }
                            catch (Exception exRetry)
                            {
                                _logger.LogDebug(exRetry, "Recheck before purge failed for client {ClientName} (non-fatal)", client.Name ?? client.Id);
                            }

                            foreach (var orphanedDownload in toPurge)
                            {
                                try
                                {
                                    await _downloadRepository.RemoveAsync(orphanedDownload.Id);
                                    _logger.LogInformation("Purged orphaned download record: {DownloadId} '{Title}' (no longer exists in {ClientName} queue)",
                                        orphanedDownload.Id, orphanedDownload.Title, client.Name);
                                    try { _metrics.Increment("download.purged.count"); } catch { }
                                }
                                catch (Exception exRemove)
                                {
                                    _logger.LogWarning(exRemove, "Failed to purge orphaned download {DownloadId} from repository, continuing", orphanedDownload.Id);
                                }
                            }

                            _logger.LogInformation("Attempted purge of {Count} orphaned download records from {ClientName}", toPurge.Count, client.Name);
                        }
                    }
                    catch (Exception purgeEx)
                    {
                        _logger.LogError(purgeEx, "Error purging orphaned downloads for client {ClientName}", client.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting queue from download client {ClientName}", client.Name);
                }
            }

            if (includeCompletedExternal)
            {
                try
                {
                    var existingIds = queueItems.Select(q => q.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var completedExternal = listenarrDownloads
                        .Where(d => d.DownloadClientId != "DDL" && d.Status == DownloadStatus.Completed)
                        .ToList();

                    foreach (var d in completedExternal)
                    {
                        if (existingIds.Contains(d.Id)) continue;

                        var clientCfg = enabledClients.FirstOrDefault(c => c.Id == d.DownloadClientId);
                        var clientName = clientCfg?.Name ?? d.DownloadClientId ?? "External Client";
                        var clientType = clientCfg?.Type?.ToLowerInvariant() ?? "external";

                        queueItems.Add(new QueueItem
                        {
                            Id = d.Id,
                            Title = d.Title ?? "Unknown",
                            Quality = d.Metadata != null && d.Metadata.TryGetValue("Quality", out var q) ? (q?.ToString() ?? "Unknown") : "Unknown",
                            Status = "completed",
                            Progress = 100,
                            Size = d.TotalSize,
                            Downloaded = d.DownloadedSize,
                            DownloadSpeed = 0,
                            Eta = null,
                            DownloadClient = clientName,
                            DownloadClientId = d.DownloadClientId ?? string.Empty,
                            DownloadClientType = clientType,
                            AddedAt = d.StartedAt,
                            CanPause = false,
                            CanRemove = true,
                            RemotePath = d.DownloadPath,
                            LocalPath = d.FinalPath,
                            ContentPath = d.FinalPath ?? d.DownloadPath
                        });

                        existingIds.Add(d.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while appending completed external downloads to queue (non-fatal)");
                }
            }

            return queueItems.OrderByDescending(q => q.AddedAt).ToList();
        }

        private bool AreTitlesSimilar(string a, string b)
        {
            try
            {
                // Conservative normalization used originally in DownloadService.IsMatchingTitle
                var An = NormalizeTitle(a);
                var Bn = NormalizeTitle(b);
                return An.Contains(Bn) || Bn.Contains(An) || An == Bn;
            }
            catch { return false; }
        }

        private string NormalizeTitle(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var lower = s.ToLowerInvariant();
            var cleaned = new string(lower.Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray());
            return string.Join(' ', cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
