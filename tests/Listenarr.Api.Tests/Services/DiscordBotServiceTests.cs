using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Api.Models;
using System.Runtime.InteropServices;

namespace Listenarr.Api.Tests.Services
{
    public class DiscordBotServiceTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public DiscordBotServiceTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            _output = output;
        }
        // A simple fake StartupConfigService for tests
        private class FakeStartupConfigService : IStartupConfigService
        {
            private readonly StartupConfig _cfg;
            public FakeStartupConfigService(StartupConfig cfg) => _cfg = cfg;
            public StartupConfig? GetConfig() => _cfg;
            public Task ReloadAsync() => Task.CompletedTask;
            public Task SaveAsync(StartupConfig config) { return Task.CompletedTask; }
        }

        // Minimal IHostEnvironment fake
        private class FakeHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "Listenarr.Tests";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        // Fake IProcessRunner that returns a controllable long-running process
        private class FakeProcessRunner : IProcessRunner
        {
            public Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, int timeoutMs = 60000, CancellationToken cancellationToken = default)
            {
                // Simulate node --version preflight success
                if (startInfo.FileName == "node" || (startInfo.FileName != null && startInfo.FileName.EndsWith("node", StringComparison.OrdinalIgnoreCase)))
                {
                    return Task.FromResult(new ProcessResult(0, "v-test", string.Empty, false));
                }

                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty, false));
            }

            public Process StartProcess(ProcessStartInfo startInfo)
            {
                // Start a short-lived sleeper process so the service sees a running Process
                ProcessStartInfo psi;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c ping -n 30 127.0.0.1 > nul",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "sleep",
                        Arguments = "30",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };
                }

                var proc = new Process { StartInfo = psi };
                proc.Start();
                return proc;
            }
        }

        [Fact]
        public async Task StartAndStopBot_WithFakeRunner_StartsAndStopsProcess()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr_test_discord_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var botDir = Path.Combine(tempRoot, "tools", "discord-bot");
            Directory.CreateDirectory(botDir);
            // Create a dummy index.js so DiscordBotService can find it
            File.WriteAllText(Path.Combine(botDir, "index.js"), "console.log('dummy'); setTimeout(()=>{}, 100000);");

            var hostEnv = new FakeHostEnvironment { ContentRootPath = tempRoot };
            var cfg = new StartupConfig { ApiKey = "test-api-key", EnableSsl = false, Port = 5000 };
            var startupService = new FakeStartupConfigService(cfg);
            var httpAccessor = new HttpContextAccessor();
            var logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug)).CreateLogger<DiscordBotService>();
            var fakeRunner = new FakeProcessRunner();

            var svc = new DiscordBotService(logger, startupService, hostEnv, httpAccessor, fakeRunner);

            try
            {
                // Debug: ensure test setup is correct
                _output.WriteLine($"[Test] ContentRootPath: {hostEnv.ContentRootPath}");
                _output.WriteLine($"[Test] Bot dir exists: {Directory.Exists(botDir)}");
                _output.WriteLine($"[Test] index.js exists: {File.Exists(Path.Combine(botDir, "index.js"))}");

                // Act - start the bot
                var started = await svc.StartBotAsync();

                // Assert start was successful and bot is running
                Assert.True(started, "StartBotAsync should return true");
                var isRunning = await svc.IsBotRunningAsync();
                Assert.True(isRunning, "Bot should be running after StartBotAsync");

                var status = await svc.GetBotStatusAsync();
                Assert.NotNull(status);
                Assert.Contains("running", status);

                // Act - stop the bot
                var stopped = await svc.StopBotAsync();
                Assert.True(stopped, "StopBotAsync should return true");

                var isRunningAfter = await svc.IsBotRunningAsync();
                Assert.False(isRunningAfter, "Bot should not be running after StopBotAsync");
            }
            finally
            {
                // Cleanup
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }
}
