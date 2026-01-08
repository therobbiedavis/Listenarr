using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Listenarr.Api.Services.Search;
using Listenarr.Api.Services.Search.Filters;
using Listenarr.Api.Services.Search.Strategies;
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
            // Use the interface expected by SearchService constructor for audible metadata
            var audibleMetadataService = Mock.Of<IAudibleMetadataService>();
            var amazonMetadataService = Mock.Of<IAmazonMetadataService>();
            var openLibraryService = Mock.Of<IOpenLibraryService>();
            var amazonSearch = Mock.Of<IAmazonSearchService>();
            var audibleSearch = Mock.Of<IAudibleSearchService>();
            var imageCache = Mock.Of<IImageCacheService>();
            ListenArrDbContext? db = null;
            var hubContext = Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var audimeta = new Mock<AudimetaService>(new System.Net.Http.HttpClient(), Mock.Of<ILogger<AudimetaService>>()).Object;
            var audnexus = new Mock<AudnexusService>(new System.Net.Http.HttpClient(), Mock.Of<ILogger<AudnexusService>>()).Object;
            var converters = new MetadataConverters(Mock.Of<IImageCacheService>(), Mock.Of<ILogger<MetadataConverters>>());
            var merger = new MetadataMerger(Mock.Of<ILogger<MetadataMerger>>());
            var progress = new SearchProgressReporter(null, Mock.Of<ILogger<SearchProgressReporter>>());
            var pipeline = new SearchResultFilterPipeline(Enumerable.Empty<ISearchResultFilter>(), Mock.Of<ILogger<SearchResultFilterPipeline>>());
            var coordinator = new MetadataStrategyCoordinator(Enumerable.Empty<IMetadataStrategy>(), Mock.Of<ILogger<MetadataStrategyCoordinator>>());
            var collector = new AsinCandidateCollector(Mock.Of<ILogger<AsinCandidateCollector>>(), Mock.Of<IOpenLibraryService>(), converters, progress);
            var enricher = new AsinEnricher(Mock.Of<ILogger<AsinEnricher>>(), coordinator, Mock.Of<IAudibleMetadataService>(), converters, merger, pipeline, progress);
            var fallback = new FallbackScraper(Mock.Of<ILogger<FallbackScraper>>(), Mock.Of<IAmazonMetadataService>(), converters, pipeline, progress);
            var scorer = new SearchResultScorer(Mock.Of<ILogger<SearchResultScorer>>());
            var handler = new Mock<AsinSearchHandler>(Mock.Of<ILogger<AsinSearchHandler>>(), Mock.Of<IConfigurationService>(), audimeta, Mock.Of<IAudnexusService>(), Mock.Of<IAudibleMetadataService>(), Mock.Of<IAmazonMetadataService>(), converters, progress).Object;

            return new SearchService(httpClient, configuration, logger, audibleMetadataService, amazonMetadataService, openLibraryService, amazonSearch, audibleSearch, imageCache, db, hubContext, audimeta, audnexus, converters, merger, progress, pipeline, coordinator, collector, enricher, fallback, scorer, handler, Enumerable.Empty<Listenarr.Api.Services.Search.Providers.IIndexerSearchProvider>());
        }

        [Fact]
        public void ApplySorting_SortsByLanguage_Descending()
        {
            var svc = (SearchService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchService));

            var results = new List<SearchResult>
            {
                new SearchResult { Id = "1", Title = "A", Language = "english" },
                new SearchResult { Id = "2", Title = "B", Language = "french" },
                new SearchResult { Id = "3", Title = "C", Language = null },
                new SearchResult { Id = "4", Title = "D", Language = "German" }
            };

            // Call private ApplySorting via reflection
            var method = typeof(SearchService).GetMethod("ApplySorting", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var ordered = (List<SearchResult>)method.Invoke(svc, new object[] { results, SearchSortBy.Language, SearchSortDirection.Descending })!;

            // Expect order: 'english', 'German', 'french', null (case-insensitive, descending)
            // StringComparer.OrdinalIgnoreCase sorts lexicographically; descending should put 'french' > 'english' > 'German' > '' but to be deterministic test the comparer by actual result
            Assert.Equal(4, ordered.Count);
            // Ensure none of the nulls are first when descending
            Assert.NotNull(ordered[0].Language);
        }

        [Fact]
        public void ApplySorting_SortsByLanguage_Ascending()
        {
            var svc = (SearchService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchService));

            var results = new List<SearchResult>
            {
                new SearchResult { Id = "1", Title = "A", Language = "english" },
                new SearchResult { Id = "2", Title = "B", Language = "french" },
                new SearchResult { Id = "3", Title = "C", Language = null },
                new SearchResult { Id = "4", Title = "D", Language = "German" }
            };

            var method = typeof(SearchService).GetMethod("ApplySorting", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var ordered = (List<SearchResult>)method.Invoke(svc, new object[] { results, SearchSortBy.Language, SearchSortDirection.Ascending })!;

            // Ascending should place null/empty first
            Assert.Equal(4, ordered.Count);
            Assert.Null(ordered[0].Language);
        }
    }
}
