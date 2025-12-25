
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
            Assert.Equal("SearchByTitleAsync", stubAudimeta.LastMethod);
            Assert.Equal("T", stubAudimeta.LastTitle);
            Assert.Equal(1, stubAudimeta.LastPage);
            Assert.Equal(10, stubAudimeta.LastLimit);
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
            Assert.Equal("SearchByTitleAndAuthorPagedAsync", stubAudimeta2.LastMethod);
            Assert.Equal("Title", stubAudimeta2.LastTitle);
            Assert.Equal("Author", stubAudimeta2.LastAuthor);
            Assert.Equal(1, stubAudimeta2.LastPage);
            Assert.Equal(20, stubAudimeta2.LastLimit);
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
            Assert.Equal("SearchByIsbnAsync", stubAudimeta3.LastMethod);
            Assert.Equal("9780000000", stubAudimeta3.LastTitle);
            Assert.Equal(1, stubAudimeta3.LastPage);
            Assert.Equal(10, stubAudimeta3.LastLimit);
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

        public StubAudimetaService() : base(new HttpClient(), new NullLogger<AudimetaService>()) { }

        public override Task<AudimetaSearchResponse?> SearchByTitleAsync(string title, int page = 1, int limit = 50, string region = "us", string? language = null)
        {
            LastMethod = "SearchByTitleAsync";
            LastTitle = title;
            LastPage = page;
            LastLimit = limit;
            return Task.FromResult(ResponseToReturn);
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

