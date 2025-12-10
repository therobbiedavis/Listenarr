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
    public class IndexersAuthTests
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

        private Listenarr.Api.Controllers.IndexersController CreateController(HttpMessageHandler handler)
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var logger = new LoggerFactory().CreateLogger<Listenarr.Api.Controllers.IndexersController>();
            var client = new HttpClient(handler);

            return new Listenarr.Api.Controllers.IndexersController(db, logger, client);
        }

        [Fact]
        public async Task TestDraft_Newznab_InvalidApiKey_ReturnsBadRequestAndMarksFailed()
        {
            // Arrange - server responds with 403 and message indicating invalid API key
            var resp = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("Invalid API key")
            };
            var handler = new CaptureHandler(resp);
            var controller = CreateController(handler);

            var indexer = new Indexer
            {
                Name = "althub",
                Type = "Usenet",
                Implementation = "Newznab",
                Url = "https://api.althub.co.za",
                ApiKey = "BAD_KEY",
            };

            // Act
            var result = await controller.TestDraft(indexer);

            // Assert: the handler was used and the request contained apikey param
            Assert.NotNull(handler.LastRequest);
            var uri = handler.LastRequest!.RequestUri!.ToString();
            Assert.Contains("apikey=BAD_KEY", uri, StringComparison.OrdinalIgnoreCase);
            // Result should be 400 BadRequest because we treat auth errors as failures
            Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
            // The in-memory provided indexer should have been updated to reflect failure (persist=false still updates instance)
            Assert.False(indexer.LastTestSuccessful);
            Assert.NotNull(indexer.LastTestError);
        }

        [Fact]
        public async Task TestDraft_Newznab_ValidApiKey_ReturnsOkAndMarksSuccess()
        {
            // Arrange - server responds with 200 OK and some valid payload
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ \"result\": true }")
            };
            var handler = new CaptureHandler(resp);
            var controller = CreateController(handler);

            var indexer = new Indexer
            {
                Name = "althub",
                Type = "Usenet",
                Implementation = "Newznab",
                Url = "https://api.althub.co.za",
                ApiKey = "GOOD_KEY",
            };

            // Act
            var result = await controller.TestDraft(indexer);

            // Assert
            Assert.NotNull(handler.LastRequest);
            var uri = handler.LastRequest!.RequestUri!.ToString();
            Assert.Contains("apikey=GOOD_KEY", uri, StringComparison.OrdinalIgnoreCase);
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
            Assert.True(indexer.LastTestSuccessful);
            Assert.Null(indexer.LastTestError);
        }
    }
}
