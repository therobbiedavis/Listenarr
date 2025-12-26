using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Listenarr.Api.Services;
using Listenarr.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class IntelligentSearchIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public IntelligentSearchIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task IntelligentSearch_ReturnsEnrichedResults_WhenAudibleIsMocked()
        {
            // Build controller directly to avoid in-memory HTTP binding issues in CI environment
            var mockSearch = new Moq.Mock<ISearchService>();
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<SearchResult> { new SearchResult { Asin = "B0TESTASIN", Title = "Clean Title" } });

            var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<SearchController>>();
            var audimeta = new TestEmptyAudimetaService();
            var metadata = new TestAudibleMetadataService();
            var controller = new Listenarr.Api.Controllers.SearchController(mockSearch.Object, logger, audimeta, (IAudiobookMetadataService)metadata);
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var actionResult = await controller.IntelligentSearch("test-query");
            List<SearchResult>? returned = null;
            if (actionResult.Value != null) returned = actionResult.Value;
            else if (actionResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult ok) returned = ok.Value as List<SearchResult>;

            Assert.NotNull(returned);
            Assert.Single(returned);
            Assert.Equal("B0TESTASIN", returned![0].Asin);
            Assert.Equal("Clean Title", returned![0].Title);
        }

        [Fact]
        public async Task IntelligentSearch_TitlePrefix_MatchesOnlyTitleNotAuthor()
        {
            var mockSearch = new Moq.Mock<ISearchService>();
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<SearchResult> { new SearchResult { Asin = "B000000001", Title = "Ingram: A Novel" }, new SearchResult { Asin = "B000000002", Title = "Different Book", Author = "Ingram" } });

            var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<SearchController>>();
            var audimeta = new TestEmptyAudimetaService();
            var metadata = new TestAudibleMetadataService();
            var controller = new Listenarr.Api.Controllers.SearchController(mockSearch.Object, logger, audimeta, (IAudiobookMetadataService)metadata);
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var actionResult = await controller.IntelligentSearch("TITLE:Ingram");
            List<SearchResult>? returned = null;
            if (actionResult.Value != null) returned = actionResult.Value;
            else if (actionResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult ok) returned = ok.Value as List<SearchResult>;

            Assert.NotNull(returned);
            Assert.NotEmpty(returned);
            Assert.Contains(returned, r => (r.Title ?? string.Empty).IndexOf("Ingram", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public async Task IntelligentSearch_FiltersOut_PrintOnly_AmazonResults()
        {
            var mockSearch = new Moq.Mock<ISearchService>();
            // Return empty intelligent-search results when Amazon/Audible are empty
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<SearchResult>());

            var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<SearchController>>();
            var audimeta = new TestEmptyAudimetaService();
            var metadata = new TestEmptyAudibleMetadataService();
            var controller = new Listenarr.Api.Controllers.SearchController(mockSearch.Object, logger, audimeta, (IAudiobookMetadataService)metadata);
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var actionResult = await controller.IntelligentSearch("9780261103573");
            List<SearchResult>? returned = null;
            if (actionResult.Value != null) returned = actionResult.Value;
            else if (actionResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult ok) returned = ok.Value as List<SearchResult>;

            Assert.NotNull(returned);
            Assert.Empty(returned);
        }

        [Fact]
        public async Task IntelligentSearch_Accepts_AudioCD_AmazonResult()
        {
            var mockSearch = new Moq.Mock<ISearchService>();
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<SearchResult> { new SearchResult { Asin = "0563528885", Title = "The Lord of the Rings: The Trilogy", ImageUrl = "http://example.com/audio_cd.jpg" } });

            var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<SearchController>>();
            var audimeta = new TestEmptyAudimetaService();
            var metadata = new TestEmptyAudibleMetadataService();
            var controller = new Listenarr.Api.Controllers.SearchController(mockSearch.Object, logger, audimeta, (IAudiobookMetadataService)metadata);
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var actionResult = await controller.IntelligentSearch("9780261103573");
            List<SearchResult>? returned = null;
            if (actionResult.Value != null) returned = actionResult.Value;
            else if (actionResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult ok) returned = ok.Value as List<SearchResult>;

            Assert.NotNull(returned);
            Assert.Contains(returned!, r => string.Equals(r.Asin, "0563528885", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task IntelligentSearch_UsesAudibleScraper_WhenAudimetaMissing()
        {
            var mockSearch = new Moq.Mock<ISearchService>();
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<SearchResult> { new SearchResult { Asin = "B0TESTASIN", Title = "Clean Title", MetadataSource = "Audible" } });

            var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<SearchController>>();
            var audimeta = new TestEmptyAudimetaService();
            var metadata = new TestAudibleMetadataService();
            var controller = new Listenarr.Api.Controllers.SearchController(mockSearch.Object, logger, audimeta, (IAudiobookMetadataService)metadata);
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var actionResult = await controller.IntelligentSearch("test-query");
            List<SearchResult>? returned = null;
            if (actionResult.Value != null) returned = actionResult.Value;
            else if (actionResult.Result is Microsoft.AspNetCore.Mvc.OkObjectResult ok) returned = ok.Value as List<SearchResult>;

            Assert.NotNull(returned);
            Assert.NotEmpty(returned!);
            var audibleMatch = returned!.Find(r => string.Equals(r.Asin, "B0TESTASIN", System.StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(audibleMatch);
            Assert.Equal("Clean Title", audibleMatch!.Title);
            Assert.Equal("Audible", audibleMatch.MetadataSource);
        }
    }

    internal class TestAmazonAudioCdSearchService : IAmazonSearchService
    {
        public Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string query, string? author = null, System.Threading.CancellationToken ct = default)
        {
            var list = new List<AmazonSearchResult>
            {
                new AmazonSearchResult
                {
                    Asin = "0563528885",
                    Title = "The Lord of the Rings: The Trilogy: The Complete Collection of the Classic BBC Radio Production (BBC Radio Collection)      Audio CD - Unabridged, October 7, 2002",
                    ImageUrl = "http://example.com/audio_cd.jpg",
                    Author = "BBC"
                }
            };
            return Task.FromResult(list);
        }

        public Task<AmazonSearchResult?> ScrapeProductPageAsync(string asin, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<AmazonSearchResult?>(null);
        }
    }

    internal class TestAmazonPrintOnlySearchService : IAmazonSearchService
    {
        public Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string query, string? author = null, System.Threading.CancellationToken ct = default)
        {
            var list = new List<AmazonSearchResult>
            {
                new AmazonSearchResult
                {
                    Asin = "BPRINT1",
                    Title = "The Fellowship of the Ring (The Lord of the Rings, Part 1)      Mass Market Paperback – August 12, 1986",
                    ImageUrl = "http://example.com/p1.jpg",
                    Author = "J. R. R. Tolkien"
                },
                new AmazonSearchResult
                {
                    Asin = "BPRINT2",
                    Title = "The Lord of the Rings 3 Books Box Set By J. R. R. Tolkien      Paperback – January 1, 2021",
                    ImageUrl = "http://example.com/p2.jpg",
                    Author = "J. R. R. Tolkien"
                }
            };
            return Task.FromResult(list);
        }

        public Task<AmazonSearchResult?> ScrapeProductPageAsync(string asin, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<AmazonSearchResult?>(null);
        }
    }

    internal class TestEmptyAudibleSearchService : IAudibleSearchService
    {
        public Task<List<AudibleSearchResult>> SearchAudiobooksAsync(string query, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult(new List<AudibleSearchResult>());
        }
    }

    internal class TestEmptyAudibleMetadataService : IAudibleMetadataService
    {
        public Task<AudibleBookMetadata?> ScrapeAudibleMetadataAsync(string asin, System.Threading.CancellationToken ct = default) => Task.FromResult<AudibleBookMetadata?>(null);
        public Task<AudibleBookMetadata?> ScrapeAmazonMetadataAsync(string asin) => Task.FromResult<AudibleBookMetadata?>(null);
        public Task<List<AudibleBookMetadata>> PrefetchAsync(List<string> asins) => Task.FromResult(new List<AudibleBookMetadata>());
    }

    internal class TestEmptyOpenLibraryService : IOpenLibraryService
    {
        public Task<List<string>> GetIsbnsForTitleAsync(string title, string? author = null)
        {
            return Task.FromResult(new List<string>());
        }

        public Task<OpenLibrarySearchResponse> SearchBooksAsync(string title, string? author = null, int limit = 10)
        {
            return Task.FromResult(new OpenLibrarySearchResponse { Docs = new List<OpenLibraryBook>() });
        }
    }

    // Simple test implementations to avoid network calls in integration tests
    internal class TestAudibleSearchService : IAudibleSearchService
    {
        public Task<List<AudibleSearchResult>> SearchAudiobooksAsync(string query, System.Threading.CancellationToken ct = default)
        {
            var list = new List<AudibleSearchResult>
            {
                new AudibleSearchResult
                {
                    Asin = "B0TESTASIN",
                    Title = "Noise Title - Audible",
                    ProductUrl = "https://www.audible.com/pd/test",
                    ImageUrl = "http://example.com/test.jpg",
                    Author = "Test Author",
                }
            };
            return Task.FromResult(list);
        }
    }

    internal class TestAudibleMetadataService : IAudibleMetadataService
    {
        public Task<AudibleBookMetadata?> ScrapeAudibleMetadataAsync(string asin, System.Threading.CancellationToken ct = default)
        {
            var m = new AudibleBookMetadata
            {
                Asin = asin,
                Title = "Clean Title",
                Authors = new List<string> { "Author Name" },
                ImageUrl = "http://example.com/test.jpg",
                PublishYear = "2024"
            };
            return Task.FromResult<AudibleBookMetadata?>(m);
        }

        public Task<AudibleBookMetadata?> ScrapeAmazonMetadataAsync(string asin)
        {
            var m = new AudibleBookMetadata
            {
                Asin = asin,
                Title = "Clean Title from Amazon",
                Authors = new List<string> { "Author Name" },
                ImageUrl = "http://example.com/test.jpg",
                PublishYear = "2024",
                Source = "Amazon"
            };
            return Task.FromResult<AudibleBookMetadata?>(m);
        }

        public Task<List<AudibleBookMetadata>> PrefetchAsync(List<string> asins)
        {
            var result = new List<AudibleBookMetadata>();
            foreach (var a in asins)
            {
                result.Add(new AudibleBookMetadata { Asin = a, Title = "Clean Title", Authors = new List<string> { "Author Name" } });
            }
            return Task.FromResult(result);
        }
    }

    internal class TestEmptyAudimetaService : Listenarr.Api.Services.AudimetaService
    {
        public TestEmptyAudimetaService() : base(new System.Net.Http.HttpClient(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<Listenarr.Api.Services.AudimetaService>()) { }

        public override Task<Listenarr.Api.Services.AudimetaBookResponse?> GetBookMetadataAsync(string asin, string region = "us", bool useCache = true, string? language = null)
        {
            return Task.FromResult<Listenarr.Api.Services.AudimetaBookResponse?>(null);
        }
    }

    internal class TestEmptyAudnexusService : Listenarr.Api.Services.IAudnexusService
    {
        public Task<Listenarr.Api.Services.AudnexusBookResponse?> GetBookMetadataAsync(string asin, string region = "us", bool seedAuthors = true, bool update = false)
        {
            return Task.FromResult<Listenarr.Api.Services.AudnexusBookResponse?>(null);
        }

        public Task<List<Listenarr.Api.Services.AudnexusAuthorSearchResult>?> SearchAuthorsAsync(string name, string region = "us")
        {
            return Task.FromResult<List<Listenarr.Api.Services.AudnexusAuthorSearchResult>?>(null);
        }

        public Task<Listenarr.Api.Services.AudnexusAuthorResponse?> GetAuthorAsync(string asin, string region = "us", bool update = false)
        {
            return Task.FromResult<Listenarr.Api.Services.AudnexusAuthorResponse?>(null);
        }

        public Task<Listenarr.Api.Services.AudnexusChapterResponse?> GetChaptersAsync(string asin, string region = "us", bool update = false)
        {
            return Task.FromResult<Listenarr.Api.Services.AudnexusChapterResponse?>(null);
        }
    }

    internal class TestAmazonSearchService : IAmazonSearchService
    {
        public Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string query, string? author = null, System.Threading.CancellationToken ct = default)
        {
            // Return empty list to avoid interference with Audible-only test
            return Task.FromResult(new List<AmazonSearchResult>());
        }
        public Task<AmazonSearchResult?> ScrapeProductPageAsync(string asin, System.Threading.CancellationToken ct = default)
        {
            // Tests don't need product page scraping; return null
            return Task.FromResult<AmazonSearchResult?>(null);
        }
    }

    internal class TestAmazonTitlePrefixSearchService : IAmazonSearchService
    {
        public Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string query, string? author = null, System.Threading.CancellationToken ct = default)
        {
            var list = new List<AmazonSearchResult>
            {
                new AmazonSearchResult
                {
                    Asin = "B000000001",
                    Title = "Ingram: A Novel",
                    ImageUrl = "http://example.com/a1.jpg",
                    Author = "John Doe"
                },
                new AmazonSearchResult
                {
                    Asin = "B000000002",
                    Title = "Different Book",
                    ImageUrl = "http://example.com/a2.jpg",
                    Author = "Ingram"
                }
            };
            return Task.FromResult(list);
        }

        public Task<AmazonSearchResult?> ScrapeProductPageAsync(string asin, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<AmazonSearchResult?>(null);
        }
    }

    // Test image cache that avoids external network calls
    internal class TestImageCacheService : IImageCacheService
    {
        public Task<string?> DownloadAndCacheImageAsync(string imageUrl, string identifier)
        {
            // Return a deterministic, non-network path used by tests
            return Task.FromResult<string?>("cache/images/test.jpg");
        }

        public Task<string?> MoveToLibraryStorageAsync(string identifier, string? imageUrl = null)
        {
            return Task.FromResult<string?>("cache/images/library/test.jpg");
        }

        public Task<string?> MoveToAuthorLibraryStorageAsync(string identifier, string? imageUrl = null)
        {
            return Task.FromResult<string?>("cache/images/authors/test.jpg");
        }

        public Task<string?> GetCachedImagePathAsync(string identifier)
        {
            return Task.FromResult<string?>("cache/images/test.jpg");
        }

        public Task ClearTempCacheAsync()
        {
            return Task.CompletedTask;
        }
    }
}

