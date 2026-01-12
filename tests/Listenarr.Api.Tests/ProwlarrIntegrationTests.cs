using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Controllers;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class ProwlarrIntegrationTests
    {
        private ListenArrDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase($"test_db_{System.Guid.NewGuid()}")
                .Options;
            return new ListenArrDbContext(options);
        }

        [Fact]
        public async Task CreateFromProwlarr_CreatesIndexer()
        {
            var db = CreateDb();
            var logger = new LoggerFactory().CreateLogger<Listenarr.Api.Controllers.IndexersController>();
            var httpClient = new System.Net.Http.HttpClient();
            var controller = new Listenarr.Api.Controllers.IndexersController(db, logger, httpClient);

            var payloadJson = "{\"name\":\"Prowlarr Test (Prowlarr)\",\"implementation\":\"Torznab\",\"protocol\":\"torrent\",\"fields\":[{\"name\":\"baseUrl\",\"value\":\"https://prowlarr.example\"},{\"name\":\"apiKey\",\"value\":\"abc123\"}],\"id\":42}";
            var payload = JsonDocument.Parse(payloadJson).RootElement;

            var result = await controller.CreateFromProwlarr(payload);
            var created = await db.Indexers.FirstOrDefaultAsync(i => i.ProwlarrIndexerId == 42);

            Assert.NotNull(created);
            Assert.True(created!.AddedByProwlarr);
            Assert.Equal("https://prowlarr.example", created.Url);
            Assert.Equal("abc123", created.ApiKey);
            Assert.Equal(42, created.ProwlarrIndexerId);
        }
    }
}
