using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
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
    }

    // Simple test implementations to avoid network calls in integration tests
    internal class TestAudibleSearchService : IAudibleSearchService
    {
        public Task<List<AudibleSearchResult>> SearchAudiobooksAsync(string query)
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
        public Task<AudibleBookMetadata> ScrapeAudibleMetadataAsync(string asin)
        {
            var m = new AudibleBookMetadata
            {
                Asin = asin,
                Title = "Clean Title",
                Authors = new List<string> { "Author Name" },
                ImageUrl = "http://example.com/test.jpg",
                PublishYear = "2024"
            };
            return Task.FromResult(m);
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
        public Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string query, string? author = null)
        {
            // Return empty list to avoid interference with Audible-only test
            return Task.FromResult(new List<AmazonSearchResult>());
        }
    }
}
