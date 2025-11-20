using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Api.Controllers;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class SearchControllerTests
    {
        [Fact]
        public async Task IntelligentSearch_ReturnsEnrichedResults()
        {
            // Arrange
            var mockService = new Mock<ISearchService>();
            var sample = new List<SearchResult>
            {
                new SearchResult { Id = "1", Title = "Sample", Asin = "B000000000" }
            };
            mockService.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>())).ReturnsAsync(sample);

            var logger = Mock.Of<ILogger<SearchController>>();
            var mockAudimetaService = new Mock<AudimetaService>(new System.Net.Http.HttpClient(), Mock.Of<ILogger<AudimetaService>>());
            var controller = new SearchController(mockService.Object, logger, mockAudimetaService.Object);

            // Act
            var actionResult = await controller.IntelligentSearch("query");

            // Assert
            List<SearchResult>? returned = null;
            if (actionResult.Value != null)
            {
                returned = actionResult.Value;
            }
            else if (actionResult.Result is OkObjectResult ok)
            {
                returned = ok.Value as List<SearchResult>;
            }

            Assert.NotNull(returned);
            Assert.Single(returned!);
            Assert.Equal("Sample", returned![0].Title);
        }

        [Fact]
        public async Task IndexersSearch_ReturnsIndexerResults()
        {
            // Arrange
            var mockService = new Mock<ISearchService>();
            var sample = new List<SearchResult>
            {
                new SearchResult { Id = "i1", Title = "IndexerResult", Source = "MockIndexer" }
            };
            mockService.Setup(s => s.SearchIndexersAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SearchSortBy>(), It.IsAny<SearchSortDirection>(), It.IsAny<bool>())).ReturnsAsync(sample);

            var logger = Mock.Of<ILogger<SearchController>>();
            var mockAudimetaService = new Mock<AudimetaService>(new System.Net.Http.HttpClient(), Mock.Of<ILogger<AudimetaService>>());
            var controller = new SearchController(mockService.Object, logger, mockAudimetaService.Object);

            // Act
            var actionResult = await controller.IndexersSearch("query");

            // Assert
            List<SearchResult>? returned = null;
            if (actionResult.Value != null)
            {
                returned = actionResult.Value;
            }
            else if (actionResult.Result is OkObjectResult ok)
            {
                returned = ok.Value as List<SearchResult>;
            }

            Assert.NotNull(returned);
            Assert.Single(returned!);
            Assert.Equal("IndexerResult", returned![0].Title);
        }
    }
}
