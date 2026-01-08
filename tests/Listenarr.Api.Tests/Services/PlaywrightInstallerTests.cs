using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Listenarr.Api.Tests.Services
{
    public class PlaywrightInstallerTests
    {
        private class FakeFetcher : Listenarr.Api.Services.IPlaywrightPageFetcher
        {
            private readonly bool _initialized;
            public FakeFetcher(bool initialized) => _initialized = initialized;
            public Task<string?> FetchAsync(string url, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
            public Task<bool> TryEnsureInitializedAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(_initialized);
        }

        private class FakeProcessRunner : Listenarr.Api.Services.IProcessRunner
        {
            public Task<Listenarr.Api.Services.ProcessResult> RunAsync(System.Diagnostics.ProcessStartInfo startInfo, int timeoutMs = 60000, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Should not be called when npx is missing");
            }

            public System.Diagnostics.Process StartProcess(System.Diagnostics.ProcessStartInfo startInfo) => throw new NotImplementedException();
            public IDisposable RegisterTransientSensitive(System.Collections.Generic.IEnumerable<string> values) => new Disposable();
            private class Disposable : IDisposable { public void Dispose() { } }
        }

        [Fact]
        public async Task InstallOnceAsync_NoNpxOnPath_ReturnsNpxNotFound()
        {
            var origPath = Environment.GetEnvironmentVariable("PATH");
            try
            {
                // Ensure PATH does not contain npx
                Environment.SetEnvironmentVariable("PATH", string.Empty);

                var fetcher = new FakeFetcher(false);
                var processRunner = new FakeProcessRunner();
                var config = new ConfigurationBuilder().Build();
                var logger = NullLogger<Listenarr.Api.Services.PlaywrightInstaller>.Instance;
                var status = new Listenarr.Api.Services.PlaywrightInstallStatus();
                var installer = new Listenarr.Api.Services.PlaywrightInstaller(logger, fetcher, status, config, processRunner);

                var result = await installer.InstallOnceAsync(System.Threading.CancellationToken.None);

                Assert.False(result.Success);
                Assert.Equal("npx not found", result.Err);
                Assert.Equal("npx not found", status.LastError);
            }
            finally
            {
                // restore PATH
                Environment.SetEnvironmentVariable("PATH", origPath);
            }
        }
    }
}
