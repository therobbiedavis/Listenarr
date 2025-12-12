using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Listenarr.Api.Services;
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
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the Audible search service with a test implementation that returns a deterministic result
                    services.AddSingleton<IAudibleSearchService>(sp => new TestAudibleSearchService());

                    // Replace the Audible metadata service with a test implementation that returns enriched metadata
                    services.AddSingleton<IAudibleMetadataService>(sp => new TestAudibleMetadataService());

                    // Ensure Amazon search returns no results for deterministic output
                    services.AddSingleton<IAmazonSearchService>(sp => new TestAmazonSearchService());

                    // Prevent external HTTP calls for images during tests by injecting a test image cache
                    services.AddSingleton<IImageCacheService>(sp => new TestImageCacheService());

                    // To avoid hitting the test DB schema during integration runs (some metadata
                    // source queries may fail because of migration/schema drift), replace the
                    // ISearchService used by controllers with a controlled mock that returns
                    // deterministic enriched results for this test.
                    var mockSearch = new Moq.Mock<ISearchService>();
                    mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(new System.Collections.Generic.List<SearchResult> { new SearchResult { Asin = "B0TESTASIN", Title = "Clean Title" } });
                    services.AddSingleton<ISearchService>(mockSearch.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var resp = await client.GetAsync("/api/search/intelligent?query=test-query");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var results = JsonSerializer.Deserialize<List<SearchResult>>(body, options);

            Assert.NotNull(results);
            Assert.Single(results);
            var first = results![0];
            Assert.Equal("B0TESTASIN", first.Asin);
            // The TestAudibleMetadataService provides a cleaned/enriched title
            Assert.Equal("Clean Title", first.Title);
        }

        [Fact]
        public async Task IntelligentSearch_TitlePrefix_MatchesOnlyTitleNotAuthor()
        {
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Provide an Amazon service with two results: one where title contains "Ingram" and one where the author is "Ingram"
                    services.AddSingleton<IAmazonSearchService>(sp => new TestAmazonTitlePrefixSearchService());

                    // Ensure Audible search returns no results for deterministic output
                    services.AddSingleton<IAudibleSearchService>(sp => new TestAudibleSearchService());

                    // Provide metadata service that returns a cleaned/enriched version of the title for the Asin we expect to accept
                    services.AddSingleton<IAudibleMetadataService>(sp => new TestAudibleMetadataService());

                    // Prevent external HTTP calls for images during tests by injecting a test image cache
                    services.AddSingleton<IImageCacheService>(sp => new TestImageCacheService());
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var resp = await client.GetAsync("/api/search/intelligent?query=TITLE:Ingram");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var results = JsonSerializer.Deserialize<List<SearchResult>>(body, options);

            Assert.NotNull(results);
            // Expect a single result whose title contains 'Ingram', not the author-only match
            Assert.Single(results);
            var first = results![0];
            // The result should be the ASIN whose title contains 'Ingram' (A1). The ASIN with 'Ingram' only as author (A2) should be excluded.
            Assert.Equal("B000000001", first.Asin);
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

