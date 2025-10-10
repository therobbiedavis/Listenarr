using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class AuthenticationMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AuthenticationMiddlewareTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ProtectedEndpoint_Returns401_WhenUnauthenticated_AndAuthRequired()
        {
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace IStartupConfigService to force auth required for the test
                    services.AddSingleton<Listenarr.Api.Services.IStartupConfigService>(sp =>
                    {
                        return new TestStartupConfigService(new StartupConfig { AuthenticationRequired = "Enabled" });
                    });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var resp = await client.GetAsync("/api/library");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task AllowAnonymousEndpoint_IsAccessible_WhenAuthRequired()
        {
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<Listenarr.Api.Services.IStartupConfigService>(sp =>
                    {
                        return new TestStartupConfigService(new StartupConfig { AuthenticationRequired = "Enabled" });
                    });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            // /api/startupconfig is intentionally allowed-anonymous by middleware allow-list
            var resp = await client.GetAsync("/api/startupconfig");
            Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
        }
    }

    // Simple test implementation to supply config
    internal class TestStartupConfigService : Listenarr.Api.Services.IStartupConfigService
    {
        private readonly StartupConfig _cfg;
        public TestStartupConfigService(StartupConfig cfg) { _cfg = cfg; }
        public StartupConfig? GetConfig() => _cfg;
        public Task ReloadAsync() => Task.CompletedTask;
        public Task SaveAsync(StartupConfig config) => Task.CompletedTask;
    }
}
