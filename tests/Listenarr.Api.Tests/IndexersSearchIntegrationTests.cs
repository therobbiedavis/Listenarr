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
    public class IndexersSearchIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public IndexersSearchIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task IndexersSearch_ReturnsIndexerResults_WhenSearchServiceIsMocked()
        {
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the ISearchService with a test implementation that returns deterministic indexer results
                    services.AddSingleton<ISearchService>(sp => new TestSearchService());
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var resp = await client.GetAsync("/api/search/indexers?query=test-indexer&category=audio&sortBy=Size&sortDirection=Ascending");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var results = JsonSerializer.Deserialize<List<SearchResult>>(body, options);

            Assert.NotNull(results);
            Assert.Single(results);
            var first = results![0];
            Assert.Equal("i1", first.Id);
            // Title encodes the forwarded parameters from the request (query|category|sortBy|sortDirection)
            Assert.Equal("test-indexer|audio|Size|Ascending", first.Title);
            Assert.Equal("MockIndexer", first.Source);
        }
    }

    // Minimal test implementation of ISearchService that returns deterministic indexer results
    internal class TestSearchService : ISearchService
    {
        public Task<List<SearchResult>> SearchAsync(string query, string? category = null, List<string>? apiIds = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false)
        {
            return Task.FromResult(new List<SearchResult>());
        }

        public Task<List<SearchResult>> IntelligentSearchAsync(string query)
        {
            return Task.FromResult(new List<SearchResult>());
        }

        public Task<List<SearchResult>> SearchByApiAsync(string apiId, string query, string? category = null)
        {
            return Task.FromResult(new List<SearchResult>());
        }

        public Task<bool> TestApiConnectionAsync(string apiId)
        {
            return Task.FromResult(true);
        }

        public Task<List<SearchResult>> SearchIndexersAsync(string query, string? category = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false)
        {
            // Encode the received parameters into the Title so the test can assert they were forwarded
            var title = $"{query}|{(category ?? "null")}|{sortBy}|{sortDirection}";
            var list = new List<SearchResult>
            {
                new SearchResult { Id = "i1", Title = title, Source = "MockIndexer" }
            };
            return Task.FromResult(list);
        }
    }
}
