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
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly IDownloadService _downloadService;
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(IDownloadService downloadService, ILogger<DownloadController> logger)
        {
            _downloadService = downloadService;
            _logger = logger;
        }

        /// <summary>
        /// Search for an audiobook across all indexers sequentially until a match is found,
        /// then automatically send to the appropriate download client
        /// </summary>
        [HttpPost("search-and-download")]
        public async Task<ActionResult<SearchAndDownloadResult>> SearchAndDownload([FromBody] SearchAndDownloadRequest request)
        {
            try
            {
                var result = await _downloadService.SearchAndDownloadAsync(request.AudiobookId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in search and download for audiobook {AudiobookId}", request.AudiobookId);
                return StatusCode(500, new { message = "Failed to search and download", error = ex.Message });
            }
        }

        /// <summary>
        /// Manually send a search result to a download client
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult<string>> SendToDownloadClient([FromBody] SendDownloadRequest request)
        {
            try
            {
                var downloadId = await _downloadService.SendToDownloadClientAsync(
                    request.SearchResult, 
                    request.DownloadClientId
                );
                return Ok(new { downloadId, message = "Sent to download client successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending to download client");
                return StatusCode(500, new { message = "Failed to send to download client", error = ex.Message });
            }
        }

        /// <summary>
        /// Get the current download queue from all enabled download clients
        /// </summary>
        [HttpGet("queue")]
        public async Task<ActionResult<List<QueueItem>>> GetQueue()
        {
            try
            {
                var queue = await _downloadService.GetQueueAsync();
                return Ok(queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting download queue");
                return StatusCode(500, new { message = "Failed to get download queue", error = ex.Message });
            }
        }

        /// <summary>
        /// Remove an item from the download queue
        /// </summary>
        [HttpDelete("queue/{downloadId}")]
        public async Task<ActionResult> RemoveFromQueue(string downloadId, [FromQuery] string? downloadClientId = null)
        {
            try
            {
                var removed = await _downloadService.RemoveFromQueueAsync(downloadId, downloadClientId);
                if (removed)
                {
                    return Ok(new { message = "Removed from queue successfully" });
                }
                return NotFound(new { message = "Download not found in queue" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from queue");
                return StatusCode(500, new { message = "Failed to remove from queue", error = ex.Message });
            }
        }
    }

    public class SearchAndDownloadRequest
    {
        public int AudiobookId { get; set; }
    }

    public class SendDownloadRequest
    {
        public SearchResult SearchResult { get; set; } = new();
        public string? DownloadClientId { get; set; }
    }
}
