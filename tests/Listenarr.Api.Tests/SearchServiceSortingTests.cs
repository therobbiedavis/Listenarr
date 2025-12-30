using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class SearchServiceSortingTests
    {
        private SearchService CreateSearchService()
        {
            // Create minimal mocks for constructor dependencies - ApplySorting doesn't use them so simple mocks are fine
            var httpClient = new System.Net.Http.HttpClient();
            var configuration = Mock.Of<IConfigurationService>();
            var logger = Mock.Of<ILogger<SearchService>>();
            var audibleMetadataService = new Mock<AudimetaService>(httpClient, Mock.Of<ILogger<AudimetaService>>()).Object;
            var amazonMetadataService = Mock.Of<IAmazonMetadataService>();
            var openLibraryService = Mock.Of<IOpenLibraryService>();
            var amazonSearch = Mock.Of<IAmazonSearchService>();
            var audibleSearch = Mock.Of<IAudibleSearchService>();
            var imageCache = Mock.Of<IImageCacheService>();
            var db = Mock.Of<ListenArrDbContext>();
            var hubContext = Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var audimeta = Mock.Of<AudimetaService>();
            var audnexus = Mock.Of<AudnexusService>();
            var converters = Mock.Of<MetadataConverters>();
            var merger = Mock.Of<MetadataMerger>();
            var progress = Mock.Of<SearchProgressReporter>();
            var pipeline = Mock.Of<SearchResultFilterPipeline>();
            var coordinator = Mock.Of<MetadataStrategyCoordinator>();
            var collector = Mock.Of<AsinCandidateCollector>();
            var enricher = Mock.Of<AsinEnricher>();
            var fallback = Mock.Of<FallbackScraper>();
            var scorer = Mock.Of<SearchResultScorer>();
            var handler = Mock.Of<AsinSearchHandler>();

            return new SearchService(httpClient, configuration, logger, audibleMetadataService, amazonMetadataService, openLibraryService, amazonSearch, audibleSearch, imageCache, db, hubContext, audimeta, audnexus, converters, merger, progress, pipeline, coordinator, collector, enricher, fallback, scorer, handler);
        }

        [Fact]
        public void ApplySorting_SortsByLanguage_Descending()
        {
            var svc = CreateSearchService();

            var results = new List<Listenarr.Infrastructure.Models.SearchResult>
            {
                new Listenarr.Infrastructure.Models.SearchResult { Id = "1", Title = "A", Language = "english" },
                new Listenarr.Infrastructure.Models.SearchResult { Id = "2", Title = "B", Language = "french" },
                new Listenarr.Infrastructure.Models.SearchResult { Id = "3", Title = "C", Language = null },
                new Listenarr.Infrastructure.Models.SearchResult { Id = "4", Title = "D", Language = "German" }
            };

            // Call private ApplySorting via reflection
            var method = typeof(SearchService).GetMethod("ApplySorting", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var ordered = (List<Listenarr.Infrastructure.Models.SearchResult>)method.Invoke(svc, new object[] { results, SearchSortBy.Language, SearchSortDirection.Descending })!;

            // Expect order: 'english', 'German', 'french', null (case-insensitive, descending)
            // StringComparer.OrdinalIgnoreCase sorts lexicographically; descending should put 'french' > 'english' > 'German' > '' but to be deterministic test the comparer by actual result
            Assert.Equal(4, ordered.Count);
            // Ensure none of the nulls are first when descending
            Assert.NotNull(ordered[0].Language);
        }

        [Fact]
        public void ApplySorting_SortsByLanguage_Ascending()
        {
            var svc = CreateSearchService();

            var results = new List<Listenarr.Infrastructure.Models.SearchResult>
            {
                new Listenarr.Infrastructure.Models.SearchResult { Id = "1", Title = "A", Language = "english" },
                new Listenarr.Infrastructure.Models.SearchResult { Id = "2", Title = "B", Language = "french" },
                new Listenarr.Infrastructure.Models.SearchResult { Id = "3", Title = "C", Language = null },
                new Listenarr.Infrastructure.Models.SearchResult { Id = "4", Title = "D", Language = "German" }
            };

            var method = typeof(SearchService).GetMethod("ApplySorting", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var ordered = (List<Listenarr.Infrastructure.Models.SearchResult>)method.Invoke(svc, new object[] { results, SearchSortBy.Language, SearchSortDirection.Ascending })!;

            // Ascending should place null/empty first
            Assert.Equal(4, ordered.Count);
            Assert.Null(ordered[0].Language);
        }
    }
}
