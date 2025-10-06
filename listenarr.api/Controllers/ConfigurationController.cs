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
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(IConfigurationService configurationService, ILogger<ConfigurationController> logger)
        {
            _configurationService = configurationService;
            _logger = logger;
        }

        // API Configuration endpoints
        [HttpGet("apis")]
        public async Task<ActionResult<List<ApiConfiguration>>> GetApiConfigurations()
        {
            try
            {
                var configs = await _configurationService.GetApiConfigurationsAsync();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API configurations");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("apis/{id}")]
        public async Task<ActionResult<ApiConfiguration>> GetApiConfiguration(string id)
        {
            try
            {
                var config = await _configurationService.GetApiConfigurationAsync(id);
                if (config == null)
                {
                    return NotFound();
                }
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API configuration {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("apis")]
        public async Task<ActionResult<string>> SaveApiConfiguration([FromBody] ApiConfiguration config)
        {
            try
            {
                var id = await _configurationService.SaveApiConfigurationAsync(config);
                return Ok(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving API configuration");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("apis/{id}")]
        public async Task<ActionResult<bool>> DeleteApiConfiguration(string id)
        {
            try
            {
                var deleted = await _configurationService.DeleteApiConfigurationAsync(id);
                return Ok(deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting API configuration {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // Download Client Configuration endpoints
        [HttpGet("download-clients")]
        public async Task<ActionResult<List<DownloadClientConfiguration>>> GetDownloadClientConfigurations()
        {
            try
            {
                var configs = await _configurationService.GetDownloadClientConfigurationsAsync();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving download client configurations");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("download-clients/{id}")]
        public async Task<ActionResult<DownloadClientConfiguration>> GetDownloadClientConfiguration(string id)
        {
            try
            {
                var config = await _configurationService.GetDownloadClientConfigurationAsync(id);
                if (config == null)
                {
                    return NotFound();
                }
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving download client configuration {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("download-clients")]
        public async Task<ActionResult<string>> SaveDownloadClientConfiguration([FromBody] DownloadClientConfiguration config)
        {
            try
            {
                var id = await _configurationService.SaveDownloadClientConfigurationAsync(config);
                return Ok(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving download client configuration");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("download-clients/{id}")]
        public async Task<ActionResult<bool>> DeleteDownloadClientConfiguration(string id)
        {
            try
            {
                var deleted = await _configurationService.DeleteDownloadClientConfigurationAsync(id);
                return Ok(deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting download client configuration {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // Application Settings endpoints
        [HttpGet("settings")]
        public async Task<ActionResult<ApplicationSettings>> GetApplicationSettings()
        {
            try
            {
                var settings = await _configurationService.GetApplicationSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving application settings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("settings")]
        public async Task<ActionResult> SaveApplicationSettings([FromBody] ApplicationSettings settings)
        {
            try
            {
                await _configurationService.SaveApplicationSettingsAsync(settings);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving application settings");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}