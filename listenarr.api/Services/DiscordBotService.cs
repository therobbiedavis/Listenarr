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
        private Process? _botProcess;
        private readonly object _processLock = new object();

        public DiscordBotService(ILogger<DiscordBotService> logger)
        {
            _logger = logger;
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
                var startInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "bash",
                    WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "discord-bot"),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Set environment variable for API URL
                startInfo.EnvironmentVariables["LISTENARR_URL"] = "http://localhost:5000";

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