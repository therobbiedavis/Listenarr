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
using Listenarr.Api.Hubs;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Background service that polls external download client queues and pushes updates via SignalR
    /// </summary>
    public class QueueMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHubContext<DownloadHub> _hubContext;
        private readonly ILogger<QueueMonitorService> _logger;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
        private List<QueueItem> _lastQueueState = new();

        public QueueMonitorService(
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<DownloadHub> hubContext,
            ILogger<QueueMonitorService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queue Monitor Service starting");

            // Wait a bit before starting to ensure the app is fully initialized
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Queue Monitor Service");
                }

                // Wait before next poll
                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("Queue Monitor Service stopping");
        }

        private async Task MonitorQueueAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();

            try
            {
                // Get current queue from all download clients
                var currentQueue = await downloadService.GetQueueAsync();

                // Check if queue has changed
                if (HasQueueChanged(_lastQueueState, currentQueue))
                {
                    _logger.LogDebug("Queue changed, broadcasting update ({Count} items)", currentQueue.Count);

                    // Broadcast queue update via SignalR
                    await _hubContext.Clients.All.SendAsync(
                        "QueueUpdate",
                        currentQueue,
                        cancellationToken);

                    // Update last known state
                    _lastQueueState = currentQueue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to monitor queue");
            }
        }

        private bool HasQueueChanged(List<QueueItem> oldQueue, List<QueueItem> newQueue)
        {
            // Quick checks first
            if (oldQueue.Count != newQueue.Count)
                return true;

            // Create lookup for comparison
            var oldLookup = oldQueue.ToDictionary(q => q.Id);

            foreach (var newItem in newQueue)
            {
                if (!oldLookup.TryGetValue(newItem.Id, out var oldItem))
                {
                    // New item
                    return true;
                }

                // Check if any important properties changed
                if (oldItem.Status != newItem.Status ||
                    oldItem.Progress != newItem.Progress ||
                    oldItem.Downloaded != newItem.Downloaded ||
                    oldItem.ErrorMessage != newItem.ErrorMessage)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
