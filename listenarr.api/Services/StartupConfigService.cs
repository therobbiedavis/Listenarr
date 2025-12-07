using Listenarr.Domain.Models;
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
            // Path to config.json in the application config directory
            // For published executables, use the base directory (publish folder)
            // For development, this will be adjusted by the content root path logic in Program.cs
            _configPath = Path.Combine(AppContext.BaseDirectory, "config", "config.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _logger.LogInformation("Startup config not found at {Path}, creating default config", _configPath);
                    _config = CreateDefaultConfig();
                    SaveDefaultConfig();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<StartupConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                // Auto-generate API key if missing from existing config
                if (_config != null && string.IsNullOrWhiteSpace(_config.ApiKey))
                {
                    _config.ApiKey = GenerateApiKey();
                    SaveConfigFile(_config); // Save the updated config with new API key
                    _logger.LogInformation("Auto-generated API key for existing configuration");
                }
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
                SaveConfigFile(config);
                _config = config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save startup config to {Path}", _configPath);
                throw;
            }

            return Task.CompletedTask;
        }

        private StartupConfig CreateDefaultConfig()
        {
            // Generate a cryptographically secure API key for initial setup
            var apiKey = GenerateApiKey();
            
            return new StartupConfig
            {
                // Basic configuration with sensible defaults
                LogLevel = "Information",
                EnableSsl = false,
                Port = 5000,
                SslPort = 6868,
                UrlBase = "/",
                BindAddress = "*",
                ApiKey = apiKey, // Auto-generated on first run
                // Authentication: Set to "true" to require login, "false" for open access
                // When enabled, uses secure session-based authentication with Bearer tokens
                AuthenticationRequired = "false",
                UpdateMechanism = "BuiltIn",
                LaunchBrowser = true,
                Branch = "main",
                InstanceName = "Listenarr",
                SyslogPort = null,
                AnalyticsEnabled = false,
                SslCertPath = null,
                SslCertPassword = null,
                Ffmpeg = new FfmpegConfig
                {
                    Provider = "gyan", // Default to gyan.dev for Windows
                    ReleaseOverride = null,
                    ChecksumUrl = null,
                    Arch = null
                }
            };
        }

        private static string GenerateApiKey()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).TrimEnd('=');
        }

        private void SaveDefaultConfig()
        {
            try
            {
                SaveConfigFile(_config);
                _logger.LogInformation("Default config.json created at {Path}", _configPath);
                _logger.LogInformation("Authentication is DISABLED by default. Set 'AuthenticationRequired' to 'true' in config.json to enable secure login.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save default config to {Path}", _configPath);
            }
        }
        
        private void SaveConfigFile(StartupConfig? config)
        {
            if (config == null) return;
            
            // Ensure the config directory exists
            var configDir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configPath, json);
        }
    }
}

