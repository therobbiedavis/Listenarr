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
using System.Text.Json;

namespace Listenarr.Api.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly string _configPath;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigurationService(ILogger<ConfigurationService> logger)
        {
            _logger = logger;
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ListenArr");
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            
            // Ensure config directory exists
            Directory.CreateDirectory(_configPath);
        }

        public async Task<List<ApiConfiguration>> GetApiConfigurationsAsync()
        {
            try
            {
                var configFile = Path.Combine(_configPath, "apis.json");
                if (!File.Exists(configFile))
                {
                    return new List<ApiConfiguration>();
                }

                var json = await File.ReadAllTextAsync(configFile);
                return JsonSerializer.Deserialize<List<ApiConfiguration>>(json) ?? new List<ApiConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading API configurations");
                return new List<ApiConfiguration>();
            }
        }

        public async Task<ApiConfiguration?> GetApiConfigurationAsync(string id)
        {
            var configs = await GetApiConfigurationsAsync();
            return configs.FirstOrDefault(c => c.Id == id);
        }

        public async Task<string> SaveApiConfigurationAsync(ApiConfiguration config)
        {
            try
            {
                var configs = await GetApiConfigurationsAsync();
                var existingIndex = configs.FindIndex(c => c.Id == config.Id);
                
                if (existingIndex >= 0)
                {
                    configs[existingIndex] = config;
                }
                else
                {
                    configs.Add(config);
                }

                var configFile = Path.Combine(_configPath, "apis.json");
                var json = JsonSerializer.Serialize(configs, _jsonOptions);
                await File.WriteAllTextAsync(configFile, json);
                
                return config.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving API configuration");
                throw;
            }
        }

        public async Task<bool> DeleteApiConfigurationAsync(string id)
        {
            try
            {
                var configs = await GetApiConfigurationsAsync();
                var configToRemove = configs.FirstOrDefault(c => c.Id == id);
                
                if (configToRemove == null) return false;
                
                configs.Remove(configToRemove);
                
                var configFile = Path.Combine(_configPath, "apis.json");
                var json = JsonSerializer.Serialize(configs, _jsonOptions);
                await File.WriteAllTextAsync(configFile, json);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting API configuration");
                return false;
            }
        }

        public async Task<List<DownloadClientConfiguration>> GetDownloadClientConfigurationsAsync()
        {
            try
            {
                var configFile = Path.Combine(_configPath, "download-clients.json");
                if (!File.Exists(configFile))
                {
                    return new List<DownloadClientConfiguration>();
                }

                var json = await File.ReadAllTextAsync(configFile);
                return JsonSerializer.Deserialize<List<DownloadClientConfiguration>>(json) ?? new List<DownloadClientConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading download client configurations");
                return new List<DownloadClientConfiguration>();
            }
        }

        public async Task<DownloadClientConfiguration?> GetDownloadClientConfigurationAsync(string id)
        {
            var configs = await GetDownloadClientConfigurationsAsync();
            return configs.FirstOrDefault(c => c.Id == id);
        }

        public async Task<string> SaveDownloadClientConfigurationAsync(DownloadClientConfiguration config)
        {
            try
            {
                var configs = await GetDownloadClientConfigurationsAsync();
                var existingIndex = configs.FindIndex(c => c.Id == config.Id);
                
                if (existingIndex >= 0)
                {
                    configs[existingIndex] = config;
                }
                else
                {
                    configs.Add(config);
                }

                var configFile = Path.Combine(_configPath, "download-clients.json");
                var json = JsonSerializer.Serialize(configs, _jsonOptions);
                await File.WriteAllTextAsync(configFile, json);
                
                return config.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving download client configuration");
                throw;
            }
        }

        public async Task<bool> DeleteDownloadClientConfigurationAsync(string id)
        {
            try
            {
                var configs = await GetDownloadClientConfigurationsAsync();
                var configToRemove = configs.FirstOrDefault(c => c.Id == id);
                
                if (configToRemove == null) return false;
                
                configs.Remove(configToRemove);
                
                var configFile = Path.Combine(_configPath, "download-clients.json");
                var json = JsonSerializer.Serialize(configs, _jsonOptions);
                await File.WriteAllTextAsync(configFile, json);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting download client configuration");
                return false;
            }
        }

        public async Task<ApplicationSettings> GetApplicationSettingsAsync()
        {
            try
            {
                var configFile = Path.Combine(_configPath, "settings.json");
                if (!File.Exists(configFile))
                {
                    var defaultSettings = new ApplicationSettings();
                    await SaveApplicationSettingsAsync(defaultSettings);
                    return defaultSettings;
                }

                var json = await File.ReadAllTextAsync(configFile);
                return JsonSerializer.Deserialize<ApplicationSettings>(json) ?? new ApplicationSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application settings");
                return new ApplicationSettings();
            }
        }

        public async Task SaveApplicationSettingsAsync(ApplicationSettings settings)
        {
            try
            {
                var configFile = Path.Combine(_configPath, "settings.json");
                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                await File.WriteAllTextAsync(configFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving application settings");
                throw;
            }
        }
    }
}