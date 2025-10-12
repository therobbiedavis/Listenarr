using Listenarr.Api.Models;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    public interface IStartupConfigService
    {
        StartupConfig? GetConfig();
        Task ReloadAsync();
        Task SaveAsync(StartupConfig config);
    }

    public class StartupConfigService : IStartupConfigService
    {
        private readonly ILogger<StartupConfigService> _logger;
        private readonly string _configPath;
        private StartupConfig? _config;

        public StartupConfigService(ILogger<StartupConfigService> logger)
        {
            _logger = logger;
            // Path to config.json in the project config directory
            _configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config", "config.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _logger.LogInformation("Startup config not found at {Path}, using defaults", _configPath);
                    _config = new StartupConfig();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<StartupConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load startup config from {Path}", _configPath);
                _config = new StartupConfig();
            }
        }

        public StartupConfig? GetConfig() => _config;

        public Task ReloadAsync()
        {
            Load();
            return Task.CompletedTask;
        }

        public Task SaveAsync(StartupConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                // Attempt to write to the same path we read from
                File.WriteAllText(_configPath, json);
                _config = config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save startup config to {Path}", _configPath);
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
