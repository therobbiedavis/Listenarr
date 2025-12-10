using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Listenarr.Api.Tests.Services
{
    public class SystemProcessRunnerTests
    {
        [Fact]
        public async Task RunAsync_RedactsTransientSensitiveValues()
        {
            var logger = new NullLogger<SystemProcessRunner>();
            var runner = new SystemProcessRunner(logger);

            var secret = "TRANSIENT-SECRET-123";

            var psi = CreateEchoProcessStartInfo(secret);

            using var reg = runner.RegisterTransientSensitive(new[] { secret });
            var result = await runner.RunAsync(psi, 5000);

            Assert.DoesNotContain(secret, result.Stdout);
            Assert.Contains("<redacted>", result.Stdout);
        }

        [Fact]
        public async Task RunAsync_DoesNotRedact_WhenNotRegistered()
        {
            var logger = new NullLogger<SystemProcessRunner>();
            var runner = new SystemProcessRunner(logger);

            var secret = "TRANSIENT-SECRET-456";
            var psi = CreateEchoProcessStartInfo(secret);

            var result = await runner.RunAsync(psi, 5000);

            Assert.Contains(secret, result.Stdout);
        }

        private static ProcessStartInfo CreateEchoProcessStartInfo(string text)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c echo {text}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"echo {text}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }
    }
}
