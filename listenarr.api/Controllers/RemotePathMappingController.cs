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

using Listenarr.Domain.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/remotepath")]
    public class RemotePathMappingController : ControllerBase
    {
        private readonly IRemotePathMappingService _pathMappingService;
        private readonly ILogger<RemotePathMappingController> _logger;

        public RemotePathMappingController(
            IRemotePathMappingService pathMappingService,
            ILogger<RemotePathMappingController> logger)
        {
            _pathMappingService = pathMappingService;
            _logger = logger;
        }

        /// <summary>
        /// Get all remote path mappings
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<RemotePathMapping>>> GetAll()
        {
            try
            {
                var mappings = await _pathMappingService.GetAllAsync();
                return Ok(mappings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all remote path mappings");
                return StatusCode(500, new { error = "Failed to retrieve path mappings" });
            }
        }

        /// <summary>
        /// Get a specific remote path mapping by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<RemotePathMapping>> GetById(int id)
        {
            try
            {
                var mapping = await _pathMappingService.GetByIdAsync(id);
                if (mapping == null)
                {
                    return NotFound(new { error = $"Path mapping with ID {id} not found" });
                }
                return Ok(mapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving remote path mapping {Id}", id);
                return StatusCode(500, new { error = "Failed to retrieve path mapping" });
            }
        }

        /// <summary>
        /// Get all remote path mappings for a specific download client
        /// </summary>
        [HttpGet("client/{downloadClientId}")]
        public async Task<ActionResult<List<RemotePathMapping>>> GetByClientId(string downloadClientId)
        {
            try
            {
                _logger.LogInformation("Retrieving path mappings for download client: {DownloadClientId}", downloadClientId);
                var mappings = await _pathMappingService.GetByClientIdAsync(downloadClientId);
                return Ok(mappings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving remote path mappings for client {DownloadClientId}", downloadClientId);
                return StatusCode(500, new { error = "Failed to retrieve path mappings" });
            }
        }

        /// <summary>
        /// Create a new remote path mapping
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<RemotePathMapping>> Create([FromBody] RemotePathMapping mapping)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mapping.DownloadClientId))
                {
                    return BadRequest(new { error = "DownloadClientId is required" });
                }

                if (string.IsNullOrWhiteSpace(mapping.RemotePath))
                {
                    return BadRequest(new { error = "RemotePath is required" });
                }

                if (string.IsNullOrWhiteSpace(mapping.LocalPath))
                {
                    return BadRequest(new { error = "LocalPath is required" });
                }

                _logger.LogInformation("Creating remote path mapping for client {DownloadClientId}: {RemotePath} -> {LocalPath}",
                    mapping.DownloadClientId, mapping.RemotePath, mapping.LocalPath);

                var created = await _pathMappingService.CreateAsync(mapping);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating remote path mapping");
                return StatusCode(500, new { error = "Failed to create path mapping" });
            }
        }

        /// <summary>
        /// Update an existing remote path mapping
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<RemotePathMapping>> Update(int id, [FromBody] RemotePathMapping mapping)
        {
            try
            {
                if (id != mapping.Id && mapping.Id != 0)
                {
                    return BadRequest(new { error = "ID mismatch" });
                }

                mapping.Id = id;

                if (string.IsNullOrWhiteSpace(mapping.DownloadClientId))
                {
                    return BadRequest(new { error = "DownloadClientId is required" });
                }

                if (string.IsNullOrWhiteSpace(mapping.RemotePath))
                {
                    return BadRequest(new { error = "RemotePath is required" });
                }

                if (string.IsNullOrWhiteSpace(mapping.LocalPath))
                {
                    return BadRequest(new { error = "LocalPath is required" });
                }

                _logger.LogInformation("Updating remote path mapping {Id} for client {DownloadClientId}",
                    id, mapping.DownloadClientId);

                var updated = await _pathMappingService.UpdateAsync(mapping);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = $"Path mapping with ID {id} not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating remote path mapping {Id}", id);
                return StatusCode(500, new { error = "Failed to update path mapping" });
            }
        }

        /// <summary>
        /// Delete a remote path mapping
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                _logger.LogInformation("Deleting remote path mapping {Id}", id);
                var deleted = await _pathMappingService.DeleteAsync(id);

                if (!deleted)
                {
                    return NotFound(new { error = $"Path mapping with ID {id} not found" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting remote path mapping {Id}", id);
                return StatusCode(500, new { error = "Failed to delete path mapping" });
            }
        }

        /// <summary>
        /// Test path translation for a specific download client and remote path
        /// </summary>
        [HttpPost("translate")]
        public async Task<ActionResult<object>> TranslatePath([FromBody] TranslatePathRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DownloadClientId))
                {
                    return BadRequest(new { error = "DownloadClientId is required" });
                }

                if (string.IsNullOrWhiteSpace(request.RemotePath))
                {
                    return BadRequest(new { error = "RemotePath is required" });
                }

                _logger.LogInformation("Translating path for client {DownloadClientId}: {RemotePath}",
                    request.DownloadClientId, request.RemotePath);

                var localPath = await _pathMappingService.TranslatePathAsync(
                    request.DownloadClientId,
                    request.RemotePath);

                var translated = localPath != request.RemotePath;

                return Ok(new
                {
                    downloadClientId = request.DownloadClientId,
                    remotePath = request.RemotePath,
                    localPath = localPath,
                    translated = translated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating path for client {DownloadClientId}",
                    request.DownloadClientId);
                return StatusCode(500, new { error = "Failed to translate path" });
            }
        }
    }

    /// <summary>
    /// Request model for path translation endpoint
    /// </summary>
    public class TranslatePathRequest
    {
        public string DownloadClientId { get; set; } = string.Empty;
        public string RemotePath { get; set; } = string.Empty;
    }
}

