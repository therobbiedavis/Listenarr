using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public interface IDiscordBotService
    {
        Task<bool> StartBotAsync();
        Task<bool> StopBotAsync();
        Task<bool> IsBotRunningAsync();
        Task<string?> GetBotStatusAsync();
    }

    public class DiscordBotService : IDiscordBotService
    {
        private readonly ILogger<DiscordBotService> _logger;
        private readonly IStartupConfigService _startupConfigService;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private Process? _botProcess;
        private readonly object _processLock = new object();

        public DiscordBotService(
            ILogger<DiscordBotService> logger, 
            IStartupConfigService startupConfigService, 
            IHostEnvironment hostEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _startupConfigService = startupConfigService;
            _hostEnvironment = hostEnvironment;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<bool> StartBotAsync()
        {
            lock (_processLock)
            {
                // Clear the process reference if it has exited
                if (_botProcess != null && _botProcess.HasExited)
                {
                    _botProcess = null;
                }

                if (_botProcess != null && !_botProcess.HasExited)
                {
                    _logger.LogInformation("Bot is already running");
                    return true;
                }
            }

            try
            {
                var botDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "tools", "discord-bot");
                if (!Directory.Exists(botDirectory))
                {
                    _logger.LogError("Discord bot directory not found at {Path}", botDirectory);
                    return false;
                }

                var indexJsPath = Path.Combine(botDirectory, "index.js");
                if (!File.Exists(indexJsPath))
                {
                    _logger.LogError("Discord bot index.js not found at {Path}", indexJsPath);
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "bash",
                    WorkingDirectory = botDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Determine the Listenarr API URL with proper fallbacks for Docker
                var listenarrUrl = GetListenarrUrl();
                
                _logger.LogInformation("Starting Discord bot with LISTENARR_URL: {Url}", listenarrUrl);
                startInfo.EnvironmentVariables["LISTENARR_URL"] = listenarrUrl;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    startInfo.Arguments = "/c node index.js";
                }
                else
                {
                    startInfo.Arguments = "-c \"node index.js\"";
                }

                _logger.LogInformation("Starting Discord bot with command: {Command} {Args} in {WorkingDir}",
                    startInfo.FileName, startInfo.Arguments, startInfo.WorkingDirectory);

                lock (_processLock)
                {
                    _botProcess = Process.Start(startInfo);
                    if (_botProcess != null)
                    {
                        _botProcess.Exited += (sender, e) =>
                        {
                            _logger.LogInformation("Discord bot process exited with code: {ExitCode}", _botProcess.ExitCode);
                            // Clear the process reference so it can be restarted
                            lock (_processLock)
                            {
                                _botProcess = null;
                            }
                        };

                        // Start async reading of output to prevent deadlocks
                        _ = Task.Run(() => ReadOutputAsync(_botProcess));
                        _ = Task.Run(() => ReadErrorAsync(_botProcess));
                    }
                }

                // Give the process a moment to start
                await Task.Delay(1000);

                return await IsBotRunningAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Discord bot");
                return false;
            }
        }

        private string GetListenarrUrl()
        {
            // Priority 1: LISTENARR_PUBLIC_URL environment variable (Docker deployments)
            var envUrl = Environment.GetEnvironmentVariable("LISTENARR_PUBLIC_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                _logger.LogInformation("Using LISTENARR_PUBLIC_URL from environment: {Url}", envUrl);
                return envUrl.TrimEnd('/');
            }

            // Priority 2: Construct from current HTTP request (when available)
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    var request = httpContext.Request;
                    var scheme = request.Scheme;
                    var host = request.Host.Value;
                    
                    // Check if we're behind a reverse proxy (X-Forwarded headers)
                    if (request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
                    {
                        scheme = forwardedProto.ToString();
                    }
                    if (request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost))
                    {
                        host = forwardedHost.ToString();
                    }

                    var url = $"{scheme}://{host}";
                    _logger.LogInformation("Constructed URL from HTTP context: {Url}", url);
                    return url;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to construct URL from HTTP context");
            }

            // Priority 3: Use startup config
            try
            {
                var config = _startupConfigService.GetConfig();
                if (config != null)
                {
                    var protocol = (config.EnableSsl ?? false) ? "https" : "http";
                    var port = config.Port ?? 5000;
                    var urlBase = config.UrlBase?.TrimEnd('/') ?? "";
                    
                    // In Docker, use host.docker.internal instead of localhost for better compatibility
                    var host = Environment.GetEnvironmentVariable("DOCKER_ENV") != null 
                        ? "host.docker.internal" 
                        : "localhost";
                    
                    var url = $"{protocol}://{host}:{port}{urlBase}";
                    _logger.LogInformation("Constructed URL from startup config: {Url}", url);
                    return url;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to construct URL from startup config");
            }

            // Priority 4: Final fallback - try host.docker.internal first in Docker, then localhost
            var fallbackHost = Environment.GetEnvironmentVariable("DOCKER_ENV") != null 
                ? "host.docker.internal" 
                : "localhost";
            
            var fallbackUrl = $"http://{fallbackHost}:5000";
            _logger.LogWarning("Using fallback URL: {Url}", fallbackUrl);
            return fallbackUrl;
        }

        public Task<bool> StopBotAsync()
        {
            lock (_processLock)
            {
                if (_botProcess == null || _botProcess.HasExited)
                {
                    _logger.LogInformation("Bot is not running");
                    return Task.FromResult(true);
                }

                try
                {
                    _logger.LogInformation("Stopping Discord bot process");
                    _botProcess.Kill(true); // Kill entire process tree
                    _botProcess.WaitForExit(5000); // Wait up to 5 seconds
                    _botProcess = null;
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop Discord bot");
                    return Task.FromResult(false);
                }
            }
        }

        public Task<bool> IsBotRunningAsync()
        {
            lock (_processLock)
            {
                return Task.FromResult(_botProcess != null && !_botProcess.HasExited);
            }
        }

        public async Task<string?> GetBotStatusAsync()
        {
            var isRunning = await IsBotRunningAsync();
            if (!isRunning)
            {
                return "stopped";
            }

            lock (_processLock)
            {
                if (_botProcess != null)
                {
                    return $"running (PID: {_botProcess.Id})";
                }
            }

            return "unknown";
        }

        private async Task ReadOutputAsync(Process process)
        {
            try
            {
                while (!process.HasExited)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        _logger.LogInformation("Bot stdout: {Line}", line);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading bot stdout");
            }
        }

        private async Task ReadErrorAsync(Process process)
        {
            try
            {
                while (!process.HasExited)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (line != null)
                    {
                        _logger.LogError("Bot stderr: {Line}", line);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading bot stderr");
            }
        }
    }
}