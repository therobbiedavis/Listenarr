using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class IndexersControllerTests
    {
        private class CaptureHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }
            private readonly HttpResponseMessage _response;

            public CaptureHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(_response);
            }
        }

        [Fact]
        public async Task TestDraft_Newznab_AppendsApiKeyQueryParam()
        {
            // Arrange - in-memory db for controller ctor (persist=false path won't call DB saves)
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ListenArrDbContext(options);
            var logger = new LoggerFactory().CreateLogger<Listenarr.Api.Controllers.IndexersController>();

            // Prepare a handler that captures outgoing requests and returns OK
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
            var handler = new CaptureHandler(resp);
            var client = new HttpClient(handler);

            var controller = new Listenarr.Api.Controllers.IndexersController(db, logger, client);

            var indexer = new Indexer
            {
                Name = "althub",
                Type = "Usenet",
                Implementation = "Newznab",
                Url = "https://api.althub.co.za",
                ApiKey = "MY_SUPER_KEY",
                Categories = "3030",
                EnableRss = true,
                EnableAutomaticSearch = true,
                EnableInteractiveSearch = true,
                EnableAnimeStandardSearch = false,
                IsEnabled = true,
                Priority = 25,
                MinimumAge = 0,
                Retention = 0,
                MaximumSize = 0,
                AdditionalSettings = string.Empty
            };

            // Act
            var result = await controller.TestDraft(indexer);

            // Assert
            Assert.NotNull(handler.LastRequest);
            var uri = handler.LastRequest!.RequestUri!.ToString();
            Assert.Contains("apikey=MY_SUPER_KEY", uri, StringComparison.OrdinalIgnoreCase);
            // also ensure HTTP method is GET
            Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        }
    }
}
