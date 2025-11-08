using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Service for managing Discord bot operations
    /// </summary>
    public interface IDiscordBotService
    {
        Task<object> GetBotStatusAsync();
        Task<bool> RestartBotAsync();
        Task<string[]> GetBotLogsAsync(int lines = 100);
    }

    /// <summary>
    /// Implementation of Discord bot service
    /// </summary>
    public class DiscordBotService : IDiscordBotService
    {
        private readonly string _botDirectory;
        private readonly string _botScriptPath;

        public DiscordBotService()
        {
            // Assume the bot is in tools/discord-bot relative to the API project
            var apiDirectory = Path.GetDirectoryName(typeof(DiscordBotService).Assembly.Location);
            // Navigate up to the solution root, then to tools/discord-bot
            var solutionRoot = Path.GetFullPath(Path.Combine(apiDirectory, "..", "..", ".."));
            _botDirectory = Path.Combine(solutionRoot, "tools", "discord-bot");
            _botScriptPath = Path.Combine(_botDirectory, "index.js");
        }

        public async Task<object> GetBotStatusAsync()
        {
            try
            {
                // Check if bot process is running
                var processes = Process.GetProcessesByName("node");
                bool isRunning = false;
                Process botProcess = null;

                foreach (var process in processes)
                {
                    try
                    {
                        // Check if this node process is running our bot script
                        var commandLine = GetProcessCommandLine(process);
                        if (commandLine != null && commandLine.Contains("discord-bot") && commandLine.Contains("index.js"))
                        {
                            isRunning = true;
                            botProcess = process;
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore processes we can't access
                    }
                }

                var status = new
                {
                    isRunning,
                    botDirectory = _botDirectory,
                    botScriptExists = File.Exists(_botScriptPath),
                    processId = botProcess?.Id,
                    startTime = botProcess?.StartTime,
                    totalProcessorTime = botProcess?.TotalProcessorTime
                };

                return status;
            }
            catch (Exception ex)
            {
                return new { error = ex.Message, isRunning = false };
            }
        }

        public async Task<bool> RestartBotAsync()
        {
            try
            {
                // First, try to kill any existing bot processes
                var processes = Process.GetProcessesByName("node");
                foreach (var process in processes)
                {
                    try
                    {
                        var commandLine = GetProcessCommandLine(process);
                        if (commandLine != null && commandLine.Contains("discord-bot") && commandLine.Contains("index.js"))
                        {
                            process.Kill();
                            await Task.Delay(1000); // Wait for process to terminate
                        }
                    }
                    catch
                    {
                        // Ignore processes we can't kill
                    }
                }

                // Start the bot if the script exists
                if (!File.Exists(_botScriptPath))
                {
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = _botScriptPath,
                    WorkingDirectory = _botDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                // Set environment variable for the URL if not already set
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LISTENARR_URL")))
                {
                    // Try to determine the URL from current request context
                    // For now, use a default
                    startInfo.EnvironmentVariables["LISTENARR_URL"] = "http://localhost:5000";
                }

                var newProcess = Process.Start(startInfo);
                if (newProcess != null)
                {
                    // Wait a bit to see if the process starts successfully
                    await Task.Delay(2000);
                    return !newProcess.HasExited;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<string[]> GetBotLogsAsync(int lines = 100)
        {
            try
            {
                var logFiles = new[] { "bot-session.log", "bot-search-errors.log" };
                var allLines = new List<string>();

                foreach (var logFile in logFiles)
                {
                    var logPath = Path.Combine(_botDirectory, logFile);
                    if (File.Exists(logPath))
                    {
                        var fileLines = await File.ReadAllLinesAsync(logPath);
                        allLines.AddRange(fileLines);
                    }
                }

                // Sort by timestamp (assuming ISO format at start of line) and take the most recent lines
                allLines.Sort((a, b) =>
                {
                    try
                    {
                        var aTime = DateTime.Parse(a.Split(' ')[0]);
                        var bTime = DateTime.Parse(b.Split(' ')[0]);
                        return bTime.CompareTo(aTime); // Descending order
                    }
                    catch
                    {
                        return 0;
                    }
                });

                return allLines.Take(Math.Min(lines, allLines.Count)).ToArray();
            }
            catch (Exception)
            {
                return new[] { "Error reading bot logs" };
            }
        }

        private string GetProcessCommandLine(Process process)
        {
            try
            {
                // This is a simplified approach - in production you might use WMI or other methods
                // to get the full command line of a process
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }
    }
}