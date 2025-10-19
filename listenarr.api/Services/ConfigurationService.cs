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
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly ListenArrDbContext _dbContext;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly IUserService _userService;
        private readonly IStartupConfigService _startupConfigService;

        public ConfigurationService(ListenArrDbContext dbContext, ILogger<ConfigurationService> logger, IUserService userService, IStartupConfigService startupConfigService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _userService = userService;
            _startupConfigService = startupConfigService;
        }

        // API Configuration methods
        public async Task<List<ApiConfiguration>> GetApiConfigurationsAsync()
        {
            try
            {
                return await _dbContext.ApiConfigurations
                    .OrderBy(c => c.Priority)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading API configurations from database");
                return new List<ApiConfiguration>();
            }
        }

        public async Task<ApiConfiguration?> GetApiConfigurationAsync(string id)
        {
            try
            {
                return await _dbContext.ApiConfigurations
                    .FirstOrDefaultAsync(c => c.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading API configuration {Id} from database", id);
                return null;
            }
        }

        public async Task<string> SaveApiConfigurationAsync(ApiConfiguration config)
        {
            try
            {
                var existing = await _dbContext.ApiConfigurations
                    .FirstOrDefaultAsync(c => c.Id == config.Id);
                
                if (existing != null)
                {
                    // Update existing
                    _dbContext.Entry(existing).CurrentValues.SetValues(config);
                    existing.HeadersJson = config.HeadersJson;
                    existing.ParametersJson = config.ParametersJson;
                }
                else
                {
                    // Add new
                    config.CreatedAt = DateTime.UtcNow;
                    _dbContext.ApiConfigurations.Add(config);
                }
                
                await _dbContext.SaveChangesAsync();
                return config.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving API configuration to database");
                throw;
            }
        }

        public async Task<bool> DeleteApiConfigurationAsync(string id)
        {
            try
            {
                var config = await _dbContext.ApiConfigurations
                    .FirstOrDefaultAsync(c => c.Id == id);
                
                if (config == null) return false;
                
                _dbContext.ApiConfigurations.Remove(config);
                await _dbContext.SaveChangesAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting API configuration from database");
                return false;
            }
        }

        // Download Client Configuration methods
        public async Task<List<DownloadClientConfiguration>> GetDownloadClientConfigurationsAsync()
        {
            try
            {
                return await _dbContext.DownloadClientConfigurations
                    .OrderBy(c => c.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading download client configurations from database");
                return new List<DownloadClientConfiguration>();
            }
        }

        public async Task<DownloadClientConfiguration?> GetDownloadClientConfigurationAsync(string id)
        {
            try
            {
                return await _dbContext.DownloadClientConfigurations
                    .FirstOrDefaultAsync(c => c.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading download client configuration {Id} from database", id);
                return null;
            }
        }

        public async Task<string> SaveDownloadClientConfigurationAsync(DownloadClientConfiguration config)
        {
            try
            {
                var existing = await _dbContext.DownloadClientConfigurations
                    .FirstOrDefaultAsync(c => c.Id == config.Id);
                
                if (existing != null)
                {
                    // Update existing
                    _dbContext.Entry(existing).CurrentValues.SetValues(config);
                    existing.SettingsJson = config.SettingsJson;
                }
                else
                {
                    // Add new
                    config.CreatedAt = DateTime.UtcNow;
                    _dbContext.DownloadClientConfigurations.Add(config);
                }
                
                await _dbContext.SaveChangesAsync();
                return config.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving download client configuration to database");
                throw;
            }
        }

        public async Task<bool> DeleteDownloadClientConfigurationAsync(string id)
        {
            try
            {
                var config = await _dbContext.DownloadClientConfigurations
                    .FirstOrDefaultAsync(c => c.Id == id);
                
                if (config == null) return false;
                
                _dbContext.DownloadClientConfigurations.Remove(config);
                await _dbContext.SaveChangesAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting download client configuration from database");
                return false;
            }
        }

        // Application Settings methods
        public async Task<ApplicationSettings> GetApplicationSettingsAsync()
        {
            try
            {
                // Try to get from database first
                var settings = await _dbContext.ApplicationSettings.FirstOrDefaultAsync();
                
                if (settings == null)
                {
                    // Create default settings
                    settings = new ApplicationSettings();
                    _dbContext.ApplicationSettings.Add(settings);
                    await _dbContext.SaveChangesAsync();
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application settings from database");
                return new ApplicationSettings();
            }
        }

        public async Task SaveApplicationSettingsAsync(ApplicationSettings settings)
        {
            try
            {
                // Ensure Id is always 1 (singleton pattern)
                settings.Id = 1;
                
                var existing = await _dbContext.ApplicationSettings.FirstOrDefaultAsync();
                
                if (existing != null)
                {
                    // Update existing settings
                    _dbContext.Entry(existing).CurrentValues.SetValues(settings);
                    // Manually update list property
                    existing.AllowedFileExtensions = settings.AllowedFileExtensions;
                }
                else
                {
                    // Add new settings
                    _dbContext.ApplicationSettings.Add(settings);
                }
                
                await _dbContext.SaveChangesAsync();

                // If the request included admin credentials, ensure a user exists/updated
                try
                {
                    if (!string.IsNullOrWhiteSpace(settings.AdminUsername) && !string.IsNullOrWhiteSpace(settings.AdminPassword))
                    {
                        _logger.LogDebug("Processing admin user credentials: {Username}", settings.AdminUsername);
                        
                        var existingUser = await _userService.GetByUsernameAsync(settings.AdminUsername!);
                        if (existingUser == null)
                        {
                            _logger.LogInformation("Creating new admin user: {Username}", settings.AdminUsername);
                            await _userService.CreateUserAsync(settings.AdminUsername!, settings.AdminPassword!, null, true);
                            _logger.LogInformation("Admin user created successfully: {Username}", settings.AdminUsername);
                        }
                        else
                        {
                            _logger.LogInformation("Updating existing admin user password: {Username}", settings.AdminUsername);
                            // Update password to provided value
                            await _userService.UpdatePasswordAsync(settings.AdminUsername!, settings.AdminPassword!);
                            _logger.LogInformation("Admin user password updated successfully: {Username}", settings.AdminUsername);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No admin credentials provided in settings update");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create or update admin user '{Username}' from application settings. Settings will still be saved.", settings.AdminUsername);
                    // Do not fail saving settings if user creation fails; log and continue
                    // This prevents the 500 error and allows settings to be saved even if user operations fail
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving application settings to database");
                throw;
            }
        }

        // Startup Configuration methods
        public Task<StartupConfig> GetStartupConfigAsync()
        {
            try
            {
                var config = _startupConfigService.GetConfig();
                return Task.FromResult(config ?? new StartupConfig());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving startup configuration");
                return Task.FromResult(new StartupConfig());
            }
        }

        public async Task SaveStartupConfigAsync(StartupConfig config)
        {
            try
            {
                await _startupConfigService.SaveAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving startup configuration");
                throw;
            }
        }
    }
}