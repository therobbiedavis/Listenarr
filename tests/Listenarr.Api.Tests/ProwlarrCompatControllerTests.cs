using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Listenarr.Api.Controllers;
using Listenarr.Infrastructure.Models;
using Listenarr.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System;

namespace Listenarr.Api.Tests
{
    public class ProwlarrCompatControllerTests
    {
        private static ListenArrDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ListenArrDbContext(options);
        }

        [Fact]
        public async Task PostIndexers_BroadcastsSignalR_WhenNewIndexersCreated()
        {
            var db = CreateInMemoryDb();
            var logger = Mock.Of<ILogger<ProwlarrCompatController>>();

            var mockClientProxy = new Mock<IClientProxy>();
            var mockHubClients = new Mock<IHubClients>();
            mockHubClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            var mockHubContext = new Mock<IHubContext<Listenarr.Api.Hubs.SettingsHub>>();
            mockHubContext.SetupGet(h => h.Clients).Returns(mockHubClients.Object);

            var mockLogger = new Mock<ILogger<ProwlarrCompatController>>();
            var mockToastService = new Mock<IToastService>();
            var controller = new ProwlarrCompatController(mockLogger.Object, db, mockHubContext.Object, mockToastService.Object);

            var newIndexer = new { name = "Unit Test Indexer", implementation = "Newznab", baseUrl = "http://localhost", apiPath = "api", apiKey = "KEY" };
            var arr = JsonSerializer.Serialize(new[] { newIndexer });

            var payload = JsonDocument.Parse(arr).RootElement;
            var result = await controller.PostIndexers(payload);

            // Verify that SendCoreAsync (SignalR) was invoked for the indexer update
            mockClientProxy.Verify(
                p => p.SendCoreAsync("IndexersUpdated", It.IsAny<object[]>(), default),
                Times.Once);

            // Verify a 'Created indexer' log entry exists
            Assert.True(mockLogger.Invocations.Any(inv => inv.ToString().Contains("Created indexer")), "Expected a log entry containing 'Created indexer'");

            // Verify a 'Indexers processed' log entry exists
            Assert.True(mockLogger.Invocations.Any(inv => inv.ToString().Contains("Indexers processed")), "Expected a log entry containing 'Indexers processed'");
            // Verify that raw payload was logged (redacted/truncated text should include the indexer name)
            Assert.True(mockLogger.Invocations.Any(inv => inv.ToString().Contains("Unit Test Indexer")), "Expected a log entry containing the indexer name");        }
    }
}
