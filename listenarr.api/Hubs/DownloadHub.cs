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
using Listenarr.Api.Models;

namespace Listenarr.Api.Hubs
{
    /// <summary>
    /// SignalR hub for real-time download progress updates
    /// </summary>
    public class DownloadHub : Hub
    {
        private readonly ILogger<DownloadHub> _logger;

        public DownloadHub(ILogger<DownloadHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Client can request current downloads status
        /// </summary>
        public Task RequestDownloadsUpdate()
        {
            _logger.LogDebug("Client {ConnectionId} requested downloads update", Context.ConnectionId);
            // The background service will handle sending updates
            return Task.CompletedTask;
        }
    }
}
