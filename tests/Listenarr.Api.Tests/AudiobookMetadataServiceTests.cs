using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class AudiobookMetadataServiceTests
    {
        [Fact]
        public async Task GetMetadataAsync_UsesAudnexus_WhenAudimetaReturnsNull()
        {
            // Arrange
            var mockSearch = new Mock<ISearchService>();
            var audimetaMock = new Mock<AudimetaService>(new System.Net.Http.HttpClient(), Mock.Of<ILogger<AudimetaService>>());
            var audnexusMock = new Mock<IAudnexusService>();
            var logger = Mock.Of<ILogger<AudiobookMetadataService>>();

            // Simulate two metadata sources: Audimeta (priority 1) then Audnexus (priority 2)
            var sources = new List<ApiConfiguration>
            {
                new ApiConfiguration { Name = "Audimeta", BaseUrl = "https://audimeta.de", Priority = 1, IsEnabled = true },
                new ApiConfiguration { Name = "Audnexus", BaseUrl = "https://api.audnex.us", Priority = 2, IsEnabled = true }
            };

            mockSearch.Setup(s => s.GetEnabledMetadataSourcesAsync()).ReturnsAsync(sources);

            // Audimeta returns null
            audimetaMock.Setup(a => a.GetBookMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>())).ReturnsAsync((AudimetaBookResponse?)null);

            // Audnexus returns a book with Image, Authors, Description and IsAdult set
            var audnexusResp = new AudnexusBookResponse {
                Asin = "BTESTASIN",
                Title = "Test Title",
                Image = "https://audnexus.covers/cover.jpg",
                Authors = new List<AudnexusAuthor> { new AudnexusAuthor { Asin = "BAUTH", Name = "Author One" } },
                Description = "Test description",
                IsAdult = true
            };
            audnexusMock.Setup(a => a.GetBookMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>())).ReturnsAsync(audnexusResp);

            var svc = new AudiobookMetadataService(mockSearch.Object, audimetaMock.Object, audnexusMock.Object, logger);

            // Act
            var res = await svc.GetMetadataAsync("BTESTASIN", "us", true);

            // Assert
            Assert.NotNull(res);
            // Expect the returned object to contain the converted metadata (as AudimetaBookResponse wrapper)
            var asObj = res as dynamic;
            Assert.NotNull(asObj.metadata);
            var metadata = asObj.metadata as AudimetaBookResponse;
            Assert.NotNull(metadata);
            Assert.Equal("BTESTASIN", metadata.Asin);
            Assert.Equal("https://audnexus.covers/cover.jpg", metadata.ImageUrl);
            Assert.Equal("Audnexus", (string)asObj.source);

            // New assertions for mapped fields
            Assert.NotNull(metadata.Authors);
            Assert.Single(metadata.Authors);
            Assert.Equal("BAUTH", metadata.Authors[0].Asin);
            Assert.Equal("Test description", metadata.Description);
            Assert.True(metadata.Explicit);
        }
    }
}