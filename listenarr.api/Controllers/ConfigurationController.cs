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
using System.Linq;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<ConfigurationController> _logger;
        private readonly IUserService _userService;

        public ConfigurationController(IConfigurationService configurationService, ILogger<ConfigurationController> logger, IUserService userService)
        {
            _configurationService = configurationService;
            _logger = logger;
            _userService = userService;
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
        public async Task<ActionResult<object>> SaveApiConfiguration([FromBody] ApiConfiguration config)
        {
            try
            {
                var id = await _configurationService.SaveApiConfigurationAsync(config);
                return Ok(new { id });
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
                // Redact client-local DownloadPath before returning to frontend
                var response = configs.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Type,
                    c.Host,
                    c.Port,
                    c.Username,
                    // Do not include DownloadPath - client should decide its local path
                    c.UseSSL,
                    c.IsEnabled,
                    Settings = c.Settings,
                    c.CreatedAt
                }).ToList();

                return Ok(response);
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

                // Redact client-local DownloadPath before returning
                var response = new
                {
                    config.Id,
                    config.Name,
                    config.Type,
                    config.Host,
                    config.Port,
                    config.Username,
                    // Do not include DownloadPath
                    config.UseSSL,
                    config.IsEnabled,
                    Settings = config.Settings,
                    config.CreatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving download client configuration {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("download-clients")]
        public async Task<ActionResult<object>> SaveDownloadClientConfiguration([FromBody] DownloadClientConfiguration config)
        {
            try
            {
                if (config == null)
                {
                    return BadRequest("Missing download client configuration");
                }
                // If updating an existing configuration, avoid overwriting sensitive fields
                // with blank values from the incoming payload. Fetch existing config
                // and copy username/password and any client-specific apiKey when missing.
                if (!string.IsNullOrWhiteSpace(config?.Id))
                {
                    var existing = await _configurationService.GetDownloadClientConfigurationAsync(config.Id);
                    if (existing != null)
                    {
                        // Preserve username/password if incoming values are empty
                        if (string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(existing.Username))
                        {
                            config.Username = existing.Username;
                        }

                        if (string.IsNullOrWhiteSpace(config.Password) && !string.IsNullOrWhiteSpace(existing.Password))
                        {
                            config.Password = existing.Password;
                        }

                        // Preserve SABnzbd API key (stored in Settings["apiKey"]) if not provided
                        try
                        {
                            if (existing.Settings != null)
                            {
                                if (!config.Settings?.ContainsKey("apiKey") ?? true)
                                {
                                    if (existing.Settings.TryGetValue("apiKey", out var existingApiKeyObj))
                                    {
                                        var existingApiKey = existingApiKeyObj?.ToString();
                                        if (!string.IsNullOrWhiteSpace(existingApiKey))
                                        {
                                            if (config.Settings == null)
                                                config.Settings = new System.Collections.Generic.Dictionary<string, object>();
                                            config.Settings["apiKey"] = existingApiKey;
                                        }
                                    }
                                }
                                else
                                {
                                    // If config.Settings contains apiKey but it's blank, preserve existing
                                    if (config.Settings != null && config.Settings.TryGetValue("apiKey", out var incomingApiKeyObj))
                                    {
                                        var incomingApiKey = incomingApiKeyObj?.ToString();
                                        if (string.IsNullOrWhiteSpace(incomingApiKey) && existing.Settings.TryGetValue("apiKey", out var exKey))
                                        {
                                            var existingApiKey = exKey?.ToString();
                                            if (!string.IsNullOrWhiteSpace(existingApiKey))
                                            {
                                                config.Settings["apiKey"] = existingApiKey;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Non-fatal: if Settings isn't a dictionary or unexpected structure, ignore and proceed
                        }
                    }
                }

                var id = await _configurationService.SaveDownloadClientConfigurationAsync(config!);
                return Ok(new { id });
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

        // Test a download client configuration (accepts full config payload so credentials can be included)
        [HttpPost("download-clients/test")]
        public async Task<ActionResult<object>> TestDownloadClientConfiguration([FromBody] DownloadClientConfiguration config)
        {
            try
            {
                // Delegate to download service to perform protocol-specific lightweight tests
                var downloadService = HttpContext.RequestServices.GetService(typeof(IDownloadService)) as IDownloadService;
                if (downloadService == null)
                {
                    _logger.LogError("DownloadService not available to perform client test");
                    return StatusCode(500, new { success = false, message = "Server misconfiguration: download service unavailable" });
                }

                var (Success, Message, Client) = await downloadService.TestDownloadClientAsync(config);
                return Ok(new { success = Success, message = Message, client = Client });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing download client configuration");
                return StatusCode(500, new { success = false, message = ex.Message });
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
        public async Task<ActionResult<ApplicationSettings>> SaveApplicationSettings([FromBody] ApplicationSettings settings)
        {
            try
            {
                _logger.LogDebug("Saving application settings");
                await _configurationService.SaveApplicationSettingsAsync(settings);
                
                // Return the saved settings to confirm what was persisted
                var savedSettings = await _configurationService.GetApplicationSettingsAsync();
                
                // Clear sensitive admin credentials from response (they are [NotMapped] but let's be safe)
                savedSettings.AdminUsername = null;
                savedSettings.AdminPassword = null;
                
                _logger.LogDebug("Application settings saved successfully");
                return Ok(savedSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving application settings");
                return StatusCode(500, new { error = "Failed to save application settings", message = ex.Message });
            }
        }

        // Startup Configuration endpoints
        [HttpGet("startupconfig")]
        public async Task<ActionResult<StartupConfig>> GetStartupConfig()
        {
            try
            {
                var config = await _configurationService.GetStartupConfigAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving startup configuration");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("startupconfig")]
        public async Task<ActionResult<StartupConfig>> SaveStartupConfig([FromBody] StartupConfig config)
        {
            try
            {
                await _configurationService.SaveStartupConfigAsync(config);
                // Return the saved config to confirm what was persisted
                var savedConfig = await _configurationService.GetStartupConfigAsync();
                return Ok(savedConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving startup configuration");
                return StatusCode(500, "Internal server error");
            }
        }

        // Regenerate API key (requires authentication)
        [HttpPost("apikey/regenerate")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Administrator")]
        public async Task<ActionResult<object>> RegenerateApiKey()
        {
            try
            {
                var cfg = await _configurationService.GetStartupConfigAsync();
                var current = cfg ?? new StartupConfig();
                // Generate a new API key (cryptographically secure)
                var bytes = new byte[32];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(bytes);
                }
                var newKey = Convert.ToBase64String(bytes).TrimEnd('=');
                current.ApiKey = newKey;
                await _configurationService.SaveStartupConfigAsync(current);
                return Ok(new { apiKey = newKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating API key");
                return StatusCode(500, "Internal server error");
            }
        }

        // Generate API key for initial setup (when no API key exists and no users exist)
        [HttpPost("apikey/generate-initial")]
        public async Task<ActionResult<object>> GenerateInitialApiKey()
        {
            try
            {
                // Only allow this endpoint when there are no users in the system
                // This prevents bypassing authentication after initial setup
                var userCount = await _userService.GetUsersCountAsync();
                if (userCount > 0)
                {
                    return StatusCode(403, "Initial API key generation is only allowed when no users exist");
                }

                var cfg = await _configurationService.GetStartupConfigAsync();
                var current = cfg ?? new StartupConfig();
                
                // Only generate if no API key exists
                if (!string.IsNullOrWhiteSpace(current.ApiKey))
                {
                    return Ok(new { apiKey = current.ApiKey, message = "API key already exists" });
                }

                // Generate a new API key (cryptographically secure)
                var bytes = new byte[32];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(bytes);
                }
                var newKey = Convert.ToBase64String(bytes).TrimEnd('=');
                current.ApiKey = newKey;
                await _configurationService.SaveStartupConfigAsync(current);
                _logger.LogInformation("Initial API key generated successfully");
                return Ok(new { apiKey = newKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating initial API key");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}