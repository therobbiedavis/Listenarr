
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Listenarr.Api.Controllers;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class SearchControllerUnifiedTests
    {
        [Fact]
        public async Task AdvancedSearch_TitleOnly_Uses_Audimeta_SearchByTitleAsync()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            var sample = new AudimetaSearchResponse
            {
                Results = new List<AudimetaSearchResult>
                {
                    new AudimetaSearchResult { Asin = "BTEST1", Title = "T" }
                },
                TotalResults = 1
            };

            stubAudimeta.ResponseToReturn = sample;
            mockMeta.Setup(m => m.GetAudimetaMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new AudimetaBookResponse { Asin = "BTEST1", Title = "T" });

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Title = "T", Pagination = new Pagination { Page = 1, Limit = 10 } };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            // Advanced requests are routed through the unified IntelligentSearch pipeline
            mockSearch.Verify(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AdvancedSearch_TitleAndAuthor_Uses_AuthorFlow()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta2 = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            var sample = new AudimetaSearchResponse
            {
                Results = new List<AudimetaSearchResult>
                {
                    new AudimetaSearchResult { Asin = "BAUTH1", Title = "Title" }
                },
                TotalResults = 1
            };

            stubAudimeta2.ResponseToReturn = sample;
            mockMeta.Setup(m => m.GetAudimetaMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(new AudimetaBookResponse { Asin = "BAUTH1", Title = "Title" });

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta2, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Title = "Title", Author = "Author", Pagination = new Pagination { Page = 1, Limit = 20 } };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            // Author+Title advanced searches are processed by the intelligent search pipeline
            mockSearch.Verify(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AdvancedSearch_IsbnOnly_Uses_SearchByIsbnAsync()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta3 = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            var sample = new AudimetaSearchResponse
            {
                Results = new List<AudimetaSearchResult>
                {
                    new AudimetaSearchResult { Asin = "BISBN1", Title = "ISBNTitle" }
                },
                TotalResults = 1
            };

            stubAudimeta3.ResponseToReturn = sample;

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta3, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Isbn = "9780000000", Pagination = new Pagination { Page = 1, Limit = 10 } };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            // ISBN advanced searches are routed through the unified intelligent search pipeline
            mockSearch.Verify(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AdvancedSearch_AsinOnly_Uses_GetBookMetadataAsync()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta4 = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            stubAudimeta4.BookResponseToReturn = new AudimetaBookResponse { Asin = "BASIN", Title = "ASIN Title" };

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta4, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Asin = "BASIN" };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            Assert.Equal("GetBookMetadataAsync", stubAudimeta4.LastMethod);
            Assert.Equal("BASIN", stubAudimeta4.LastTitle);
        }

        [Fact]
        public async Task AdvancedSearch_SeriesName_With_Asin_Property_Uses_SeriesAsin()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            // Simulate series search returning an array with 'asin' property
            stubAudimeta.SeriesResponseToReturn = new[] { new { asin = "B0SERIES1234", region = "us" } };

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Title = "Title", Series = "Some Series" };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            // Series-only search should resolve an ASIN and fetch books for that ASIN
            Assert.Equal("GetBooksBySeriesAsinAsync", stubAudimeta.LastMethod);
            Assert.Equal("B0SERIES1234", stubAudimeta.LastSeriesAsin);
        }

        [Fact]
        public async Task AdvancedSearch_SeriesName_With_Url_Fallback_Extracts_Asin_And_Uses_It()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            // Simulate series search returning an array without 'asin' but with 'url' containing ASIN
            stubAudimeta.SeriesResponseToReturn = new[] { new { url = "https://audimeta.de/series/B0FALLBACK123", region = "us" } };

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Title = "Title", Series = "Some Series" };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            // Series-only search should resolve an ASIN and fetch books for that ASIN
            Assert.Equal("GetBooksBySeriesAsinAsync", stubAudimeta.LastMethod);
            Assert.Equal("B0FALLBACK123", stubAudimeta.LastSeriesAsin);
        }

        [Fact]
        public async Task AdvancedSearch_AuthorAndSeries_Uses_AuthorFlow_And_Filters_By_Series()
        {
            var mockSearch = new Mock<ISearchService>();
            var stubAudimeta = new StubAudimetaService();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            // Simulate IntelligentSearch returning two metadata records, only one in the requested series
            var md1 = new MetadataSearchResult { Asin = "B1", Title = "Book One", Series = "Target Series" };
            var md2 = new MetadataSearchResult { Asin = "B2", Title = "Book Two", Series = "Other Series" };
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                      .ReturnsAsync(new List<MetadataSearchResult> { md1, md2 });

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, stubAudimeta, mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Advanced, Author = "Some Author", Series = "Target Series" };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            // Ensure the author flow (intelligent search) was used
            mockSearch.Verify(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);

            // Validate returned results were filtered by series (response is Audimeta-shaped objects)
            var ok = Assert.IsType<OkObjectResult>(res.Result);
            var serialized = System.Text.Json.JsonSerializer.Serialize(ok.Value);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(serialized);
            Assert.NotNull(parsed);
            Assert.Single(parsed);
            var first = parsed![0];
            Assert.True(first.TryGetProperty("asin", out var asinProp));
            Assert.Equal("B1", asinProp.GetString());
        }

        [Fact]
        public async Task SimpleSearch_Returns_Rich_Audimeta_When_MetadataAvailable()
        {
            var mockSearch = new Mock<ISearchService>();
            var mockMeta = new Mock<IAudiobookMetadataService>();

            var md = new MetadataSearchResult { Asin = "BAUD1", Title = "Title", IsEnriched = true };
            mockSearch.Setup(s => s.IntelligentSearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
                      .ReturnsAsync(new List<MetadataSearchResult> { md });

            var audResp = new AudimetaBookResponse
            {
                Asin = "BAUD1",
                Title = "Title",
                Authors = new List<AudimetaAuthor> { new AudimetaAuthor { Asin = "A1", Name = "Author Name", Region = "us" } },
                Narrators = new List<AudimetaNarrator> { new AudimetaNarrator { Name = "Narrator Name" } },
                Genres = new List<AudimetaGenre> { new AudimetaGenre { Asin = "G1", Name = "Fiction", Type = "Fiction" } },
                Series = new List<AudimetaSeries> { new AudimetaSeries { Asin = "S1", Name = "Series Name", Position = "1" } },
                ImageUrl = "http://example.com/cover.jpg",
                LengthMinutes = 600,
                ReleaseDate = "2021-05-04T00:00:00.000Z",
                Explicit = false
            };

            mockMeta.Setup(m => m.GetAudimetaMetadataAsync("BAUD1", It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(audResp);

            var logger = new NullLogger<SearchController>();
            var controller = new SearchController(mockSearch.Object, logger, new StubAudimetaService(), mockMeta.Object, null);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            var req = new SearchRequest { Mode = SearchMode.Simple, Query = "q" };
            var reqJson = System.Text.Json.JsonSerializer.SerializeToElement(req);
            var res = await controller.Search(reqJson);

            Assert.NotNull(res);
            var ok = Assert.IsType<OkObjectResult>(res.Result);
            var serialized = System.Text.Json.JsonSerializer.Serialize(ok.Value);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(serialized);
            Assert.NotNull(parsed);
            Assert.Single(parsed);
            var first = parsed![0];

            Assert.True(first.TryGetProperty("authors", out var aProp));
            var authors = aProp.EnumerateArray();
            var firstAuthor = authors.First();
            Assert.Equal("Author Name", firstAuthor.GetProperty("name").GetString());
            Assert.Equal("A1", firstAuthor.GetProperty("asin").GetString());

            Assert.True(first.TryGetProperty("genres", out var gProp));
            var genres = gProp.EnumerateArray();
            var firstGenre = genres.First();
            Assert.Equal("G1", firstGenre.GetProperty("asin").GetString());

            Assert.True(first.TryGetProperty("series", out var sProp));
            var series = sProp.EnumerateArray();
            var firstSeries = series.First();
            Assert.Equal("S1", firstSeries.GetProperty("asin").GetString());
        }
    }

    internal class StubAudimetaService : AudimetaService
    {
        public string? LastMethod { get; set; }
        public string? LastTitle { get; set; }
        public string? LastAuthor { get; set; }
        public int LastPage { get; set; }
        public int LastLimit { get; set; }
        public AudimetaSearchResponse? ResponseToReturn { get; set; }
        public AudimetaBookResponse? BookResponseToReturn { get; set; }

        public object? SeriesResponseToReturn { get; set; }
        public string? LastSeriesAsin { get; set; }

        public StubAudimetaService() : base(new HttpClient(), new NullLogger<AudimetaService>()) { }

        public override Task<AudimetaSearchResponse?> SearchByTitleAsync(string title, int page = 1, int limit = 50, string region = "us", string? language = null)
        {
            LastMethod = "SearchByTitleAsync";
            LastTitle = title;
            LastPage = page;
            LastLimit = limit;
            return Task.FromResult(ResponseToReturn);
        }

        public override Task<object?> SearchSeriesByNameAsync(string name, string region = "us")
        {
            LastMethod = "SearchSeriesByNameAsync";
            LastTitle = name;
            return Task.FromResult(SeriesResponseToReturn);
        }

        public override async Task<object?> GetBooksBySeriesAsinAsync(string seriesAsin, string region = "us")
        {
            LastMethod = "GetBooksBySeriesAsinAsync";
            LastSeriesAsin = seriesAsin;
            // Return a minimal AudimetaSearchResponse-like envelope for downstream parsing
            var response = new AudimetaSearchResponse
            {
                Results = new System.Collections.Generic.List<AudimetaSearchResult>
                {
                    new AudimetaSearchResult { Asin = seriesAsin, Title = "Book in series" }
                },
                TotalResults = 1
            };
            return await Task.FromResult<object>(response);
        }

        public override Task<AudimetaSearchResponse?> SearchByTitleAndAuthorPagedAsync(string title, string author, int page = 1, int limit = 50, string region = "us", string? language = null)
        {
            LastMethod = "SearchByTitleAndAuthorPagedAsync";
            LastTitle = title;
            LastAuthor = author;
            LastPage = page;
            LastLimit = limit;
            return Task.FromResult(ResponseToReturn);
        }

        public override Task<AudimetaSearchResponse?> SearchByIsbnAsync(string isbn, int page = 1, int limit = 50, string region = "us", string? language = null)
        {
            LastMethod = "SearchByIsbnAsync";
            LastTitle = isbn;
            LastPage = page;
            LastLimit = limit;
            return Task.FromResult(ResponseToReturn);
        }

        public override Task<AudimetaBookResponse?> GetBookMetadataAsync(string asin, string region = "us", bool useCache = true, string? language = null)
        {
            LastMethod = "GetBookMetadataAsync";
            LastTitle = asin;
            return Task.FromResult(BookResponseToReturn);
        }
    }
}

