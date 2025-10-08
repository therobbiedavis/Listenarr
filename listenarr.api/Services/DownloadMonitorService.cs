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

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Hubs;
using Listenarr.Api.Models;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Background service that monitors download clients and pushes updates via SignalR
    /// </summary>
    public class DownloadMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHubContext<DownloadHub> _hubContext;
        private readonly ILogger<DownloadMonitorService> _logger;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
        private readonly Dictionary<string, Download> _lastDownloadStates = new();

        public DownloadMonitorService(
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<DownloadHub> hubContext,
            ILogger<DownloadMonitorService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Download Monitor Service starting");

            // Wait a bit before starting to ensure the app is fully initialized
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorDownloadsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Download Monitor Service");
                }

                // Wait before next poll
                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("Download Monitor Service stopping");
        }

        private async Task MonitorDownloadsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

            // Get all active downloads from database
            var activeDownloads = await dbContext.Downloads
                .Where(d => d.Status == DownloadStatus.Queued ||
                           d.Status == DownloadStatus.Downloading ||
                           d.Status == DownloadStatus.Processing)
                .ToListAsync(cancellationToken);

            // Only poll download clients if there are active downloads
            if (activeDownloads.Any())
            {
                var clientDownloads = activeDownloads.Where(d => d.DownloadClientId != "DDL").ToList();

                if (clientDownloads.Any())
                {
                    await PollDownloadClientsAsync(clientDownloads, configService, dbContext, cancellationToken);
                }
            }

            // Get all downloads to send to clients (only if there are active downloads or every 30 seconds)
            List<Download> allDownloads = new();
            var shouldFetchAll = activeDownloads.Any() || (DateTime.UtcNow.Second % 30 == 0);

            if (shouldFetchAll)
            {
                allDownloads = await dbContext.Downloads
                    .OrderByDescending(d => d.StartedAt)
                    .Take(100) // Limit to recent 100 downloads
                    .ToListAsync(cancellationToken);
            }

            // Check for changes and broadcast updates (only if we have data)
            if (allDownloads.Any())
            {
                await BroadcastDownloadUpdatesAsync(allDownloads, cancellationToken);
            }
        }

        private async Task PollDownloadClientsAsync(
            List<Download> downloads, 
            IConfigurationService configService,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // Group downloads by client
            var downloadsByClient = downloads.GroupBy(d => d.DownloadClientId);

            foreach (var clientGroup in downloadsByClient)
            {
                var clientId = clientGroup.Key;
                if (string.IsNullOrEmpty(clientId)) continue;

                try
                {
                    var client = await configService.GetDownloadClientConfigurationAsync(clientId);
                    if (client == null || !client.IsEnabled) continue;

                    // Poll based on client type
                    switch (client.Type.ToLower())
                    {
                        case "qbittorrent":
                            await PollQBittorrentAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                        case "transmission":
                            await PollTransmissionAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                        case "sabnzbd":
                            await PollSABnzbdAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                        case "nzbget":
                            await PollNZBGetAsync(client, clientGroup.ToList(), dbContext, cancellationToken);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling download client {ClientId}", clientId);
                }
            }
        }

        private Task PollQBittorrentAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // TODO: Implement qBittorrent API polling
            // Get torrent list, match by hash/name, update progress
            _logger.LogDebug("Polling qBittorrent client {ClientName}", client.Name);
            return Task.CompletedTask;
        }

        private Task PollTransmissionAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // TODO: Implement Transmission API polling
            _logger.LogDebug("Polling Transmission client {ClientName}", client.Name);
            return Task.CompletedTask;
        }

        private Task PollSABnzbdAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // TODO: Implement SABnzbd API polling
            _logger.LogDebug("Polling SABnzbd client {ClientName}", client.Name);
            return Task.CompletedTask;
        }

        private Task PollNZBGetAsync(
            DownloadClientConfiguration client,
            List<Download> downloads,
            ListenArrDbContext dbContext,
            CancellationToken cancellationToken)
        {
            // TODO: Implement NZBGet API polling
            _logger.LogDebug("Polling NZBGet client {ClientName}", client.Name);
            return Task.CompletedTask;
        }

        private async Task BroadcastDownloadUpdatesAsync(
            List<Download> currentDownloads, 
            CancellationToken cancellationToken)
        {
            var changedDownloads = new List<Download>();

            foreach (var download in currentDownloads)
            {
                // Check if this download has changed
                if (_lastDownloadStates.TryGetValue(download.Id, out var lastState))
                {
                    if (HasDownloadChanged(lastState, download))
                    {
                        changedDownloads.Add(download);
                        _lastDownloadStates[download.Id] = CloneDownload(download);
                    }
                }
                else
                {
                    // New download
                    changedDownloads.Add(download);
                    _lastDownloadStates[download.Id] = CloneDownload(download);
                }
            }

            // Clean up old download states that are no longer in the list
            var currentIds = currentDownloads.Select(d => d.Id).ToHashSet();
            var keysToRemove = _lastDownloadStates.Keys.Where(k => !currentIds.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _lastDownloadStates.Remove(key);
            }

            // Broadcast updates if there are changes
            if (changedDownloads.Any())
            {
                _logger.LogDebug("Broadcasting {Count} download updates", changedDownloads.Count);
                
                await _hubContext.Clients.All.SendAsync(
                    "DownloadUpdate", 
                    changedDownloads, 
                    cancellationToken);
            }

            // Also send full list periodically (every 10 polls)
            if (DateTime.UtcNow.Second % 30 == 0)
            {
                await _hubContext.Clients.All.SendAsync(
                    "DownloadsList", 
                    currentDownloads, 
                    cancellationToken);
            }
        }

        private bool HasDownloadChanged(Download oldDownload, Download newDownload)
        {
            return oldDownload.Status != newDownload.Status ||
                   oldDownload.Progress != newDownload.Progress ||
                   oldDownload.DownloadedSize != newDownload.DownloadedSize ||
                   oldDownload.ErrorMessage != newDownload.ErrorMessage ||
                   oldDownload.CompletedAt != newDownload.CompletedAt;
        }

        private Download CloneDownload(Download download)
        {
            return new Download
            {
                Id = download.Id,
                Title = download.Title,
                Artist = download.Artist,
                Album = download.Album,
                OriginalUrl = download.OriginalUrl,
                Status = download.Status,
                Progress = download.Progress,
                TotalSize = download.TotalSize,
                DownloadedSize = download.DownloadedSize,
                DownloadPath = download.DownloadPath,
                FinalPath = download.FinalPath,
                StartedAt = download.StartedAt,
                CompletedAt = download.CompletedAt,
                ErrorMessage = download.ErrorMessage,
                DownloadClientId = download.DownloadClientId
            };
        }
    }
}
