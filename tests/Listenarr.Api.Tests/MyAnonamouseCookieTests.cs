using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using Listenarr.Api.Services;
using Listenarr.Api.Models;
using Listenarr.Infrastructure.Models;
using System.Linq;
using System.Reflection;
using Listenarr.Domain.Models;
using System.Text;
using System;

namespace Listenarr.Api.Tests
{
    public class MyAnonamouseCookieTests
    {
        [Fact]
        public async Task SearchMyAnonamouse_Persists_MamIdFromSetCookie()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ListenArrDbContext(options);
            var indexer = new Indexer { Name = "MyAnonamouse1", Url = "https://www.myanonamouse.net", Implementation = "MyAnonamouse", Type = "Torrent", IsEnabled = true, EnableInteractiveSearch = true, AdditionalSettings = "{ \"mam_id\": \"old_mam\" }" };
            db.Indexers.Add(indexer);
            db.SaveChanges();

            Uri? capturedUri = null;
            var handler = new DelegatingHandlerMock((req, ct) => {
                capturedUri = req.RequestUri;
                var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
                resp.Headers.Add("Set-Cookie", "mam_id=\"new_mam\"; Path=/; HttpOnly");
                return Task.FromResult(resp);
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.myanonamouse.net") };
            var service = new SearchService(httpClient, null, NullLogger<SearchService>.Instance, null, null, null, null, null, null, db, null, null, null, null, null, null, null, null, null, null, null, null, null);

            var results = await service.SearchIndexersAsync("Test Title", null, request: null);

            // Verify the db indexer was updated with new mam_id
            var updated = db.Indexers.First(i => i.Id == indexer.Id);
            Assert.Contains("new_mam", updated.AdditionalSettings);
        }

        [Fact]
        public async Task TryPrepareMyAnonamouseTorrent_SendsCookieWhenHostDiffers()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ListenArrDbContext(options);
            var idx = new Indexer { Name = "MyAnonamouse1", Url = "https://www.myanonamouse.net", Implementation = "MyAnonamouse", Type = "Torrent", IsEnabled = true, EnableInteractiveSearch = true, AdditionalSettings = "{ \"mam_id\": \"test_mam\" }" };
            db.Indexers.Add(idx);
            db.SaveChanges();

            HttpRequestMessage? capturedRequest = null;
            var handler = new DelegatingHandlerMock((req, ct) => {
                capturedRequest = req;
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes("dummy-torrent"));
                content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "file.torrent" };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            });

            var httpClient = new HttpClient(handler);

            // Simple IHttpClientFactory that returns our client
            var httpFactory = new SimpleHttpClientFactory(httpClient);

            // Build a minimal service provider for dependencies
            var provider = TestServiceFactory.BuildServiceProvider(services => {
                services.AddSingleton<IHttpClientFactory>(httpFactory);
                services.AddSingleton<IDbContextFactory<ListenArrDbContext>>(sp => new TestDbFactory(options));
                // Provide a minimal HubContext and memory cache for DownloadService construction
                services.AddSingleton<IHubContext<DownloadHub>>(new TestHubContext());
                services.AddMemoryCache();
                // Minimal audiobook repository for DownloadService
                services.AddSingleton<Listenarr.Api.Services.IAudiobookRepository>(new TestAudiobookRepository());
            });

            // Resolve dependencies with fallbacks to minimal test implementations when not registered
            var hubContext = provider.GetService<IHubContext<DownloadHub>>()!;
            var audiobookRepo = provider.GetService<IAudiobookRepository>()!;
            var configSvc = provider.GetService<IConfigurationService>() ?? new TestConfigurationService();
            var dbFactorySvc = provider.GetService<IDbContextFactory<ListenArrDbContext>>()!;
            var httpFactorySvc = provider.GetService<IHttpClientFactory>()!;
            var scopeFactorySvc = provider.GetService<IServiceScopeFactory>()!;
            var pathMappingSvc = provider.GetService<IRemotePathMappingService>() ?? new TestRemotePathMappingService();
            var importSvc = provider.GetService<IImportService>() ?? new TestImportService();
            var searchSvc = provider.GetService<ISearchService>() ?? new TestSearchService();
            var clientGatewaySvc = provider.GetService<IDownloadClientGateway>();
            var cacheSvc = provider.GetService<IMemoryCache>()!;
            var dqSvc = provider.GetService<IDownloadQueueService>() ?? new TestDownloadQueueService();
            var completedProc = provider.GetService<ICompletedDownloadProcessor>() ?? new TestCompletedDownloadProcessor();
            var metricsSvc = provider.GetService<IAppMetricsService>() ?? new TestAppMetricsService();
            var notificationSvc = provider.GetService<NotificationService>()!;
            var hubBroadcaster = provider.GetService<Listenarr.Application.Services.IHubBroadcaster>();

            var downloadService = new Listenarr.Api.Services.DownloadService(
                hubContext,
                audiobookRepo,
                configSvc,
                dbFactorySvc,
                NullLogger<Listenarr.Api.Services.DownloadService>.Instance,
                httpFactorySvc,
                scopeFactorySvc,
                pathMappingSvc,
                importSvc,
                searchSvc,
                clientGatewaySvc,
                cacheSvc,
                dqSvc,
                completedProc,
                metricsSvc,
                notificationSvc,
                hubBroadcaster );

            // Build a SearchResult that references the indexer and uses a different host for torrent URL
            var sr = new SearchResult
            {
                Title = "Test Book",
                TorrentUrl = "https://47.39.239.96/tor/download.php/abc",
                IndexerId = idx.Id,
                TorrentFileContent = null
            };

            // Call the non-public TryPrepareMyAnonamouseTorrentAsync via reflection
            var method = typeof(Listenarr.Api.Services.DownloadService).GetMethod("TryPrepareMyAnonamouseTorrentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var task = (Task)method!.Invoke(downloadService, new object[] { sr })!;
            await task;

            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest.Headers.TryGetValues("Cookie", out var cookieVals));
            Assert.Contains("mam_id=test_mam", cookieVals.First());

            // Also assert the torrent content was saved into SearchResult
            Assert.NotNull(sr.TorrentFileContent);
            Assert.NotEmpty(sr.TorrentFileContent);
        }

        // Simple IHttpClientFactory implementation for tests
        private class SimpleHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;
            public SimpleHttpClientFactory(HttpClient client) => _client = client;
            public HttpClient CreateClient(string name) => _client;
        }

        // Simple IDbContextFactory for tests that returns new contexts from the same options
        private class TestDbFactory : IDbContextFactory<ListenArrDbContext>
        {
            private readonly DbContextOptions<ListenArrDbContext> _opts;
            public TestDbFactory(DbContextOptions<ListenArrDbContext> opts) { _opts = opts; }
            public ListenArrDbContext CreateDbContext() => new ListenArrDbContext(_opts);
            public ValueTask<ListenArrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => new ValueTask<ListenArrDbContext>(Task.FromResult(new ListenArrDbContext(_opts)));
        }

        // Minimal IHubContext implementation for tests
        private class TestClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class TestHubClients : IHubClients
        {
            private readonly IClientProxy _proxy = new TestClientProxy();
            public IClientProxy All => _proxy;
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
            public IClientProxy Client(string connectionId) => _proxy;
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
            public IClientProxy Group(string groupName) => _proxy;
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
            public IClientProxy User(string userId) => _proxy;
            public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
        }

        private class TestGroupManager : IGroupManager
        {
            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class TestHubContext : IHubContext<DownloadHub>
        {
            public IHubClients Clients { get; } = new TestHubClients();
            public IGroupManager Groups { get; } = new TestGroupManager();
        }

        // Minimal in-memory IAudiobookRepository
        private class TestAudiobookRepository : Listenarr.Api.Services.IAudiobookRepository
        {
            private readonly List<Audiobook> _store = new List<Audiobook>();
            public Task AddAsync(Audiobook audiobook) { _store.Add(audiobook); return Task.CompletedTask; }
            public Task<int> DeleteBulkAsync(List<int> ids) { var c = _store.RemoveAll(a => ids.Contains(a.Id)); return Task.FromResult(c); }
            public Task<bool> DeleteAsync(Audiobook audiobook) { return Task.FromResult(_store.Remove(audiobook)); }
            public Task<bool> DeleteByIdAsync(int id) { var a = _store.FirstOrDefault(x => x.Id == id); if (a==null) return Task.FromResult(false); _store.Remove(a); return Task.FromResult(true); }
            public Task<List<Audiobook>> GetAllAsync() => Task.FromResult(_store.ToList());
            public Task<Audiobook?> GetByAsinAsync(string asin) => Task.FromResult(_store.FirstOrDefault(a => a.Asin == asin));
            public Task<Audiobook?> GetByIdAsync(int id) => Task.FromResult(_store.FirstOrDefault(a => a.Id == id));
            public Task<Audiobook?> GetByIsbnAsync(string isbn) => Task.FromResult(_store.FirstOrDefault(a => a.Isbn == isbn));
            public Task<bool> UpdateAsync(Audiobook audiobook) { var idx = _store.FindIndex(a => a.Id == audiobook.Id); if (idx<0) return Task.FromResult(false); _store[idx]=audiobook; return Task.FromResult(true); }
        }

        // Minimal test stubs for other services used by DownloadService
        private class TestRemotePathMappingService : IRemotePathMappingService
        {
            public Task<RemotePathMapping> CreateAsync(RemotePathMapping mapping) => Task.FromResult(mapping);
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Task<List<RemotePathMapping>> GetAllAsync() => Task.FromResult(new List<RemotePathMapping>());
            public Task<RemotePathMapping?> GetByIdAsync(int id) => Task.FromResult<RemotePathMapping?>(null);
            public Task<List<RemotePathMapping>> GetByClientIdAsync(string downloadClientId) => Task.FromResult(new List<RemotePathMapping>());
            public Task<string> TranslatePathAsync(string downloadClientId, string remotePath) => Task.FromResult(remotePath);
            public Task<bool> RequiresTranslationAsync(string downloadClientId, string remotePath) => Task.FromResult(false);
            public Task<RemotePathMapping> UpdateAsync(RemotePathMapping mapping) => Task.FromResult(mapping);
        }

        private class TestImportService : IImportService
        {
            public Task<ImportResult> ImportSingleFileAsync(string downloadId, int? audiobookId, string sourcePath, ApplicationSettings settings, CancellationToken ct = default) => Task.FromResult(new ImportResult { Success = true });
            public Task<List<ImportResult>> ImportFilesFromDirectoryAsync(string downloadId, int? audiobookId, IEnumerable<string> files, ApplicationSettings settings, CancellationToken ct = default) => Task.FromResult(new List<ImportResult>());
            public Task<ImportResult> ReprocessExistingFileAsync(string downloadId, int? audiobookId, string sourcePath, ApplicationSettings settings, CancellationToken ct = default) => Task.FromResult(new ImportResult { Success = true });
        }

        private class TestSearchService : ISearchService
        {
            public Task<List<SearchResult>> SearchAsync(string query, string? category = null, List<string>? apiIds = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false) => Task.FromResult(new List<SearchResult>());
            public Task<List<MetadataSearchResult>> IntelligentSearchAsync(string query, int candidateLimit = 50, int returnLimit = 50, string containmentMode = "Relaxed", bool requireAuthorAndPublisher = false, double fuzzyThreshold = 0.7, string region = "us", string? language = null, CancellationToken ct = default) => Task.FromResult(new List<MetadataSearchResult>());
            public Task<List<SearchResult>> SearchByApiAsync(string apiId, string query, string? category = null) => Task.FromResult(new List<SearchResult>());
            public Task<List<Listenarr.Domain.Models.IndexerSearchResult>> SearchIndexerResultsAsync(string apiId, string query, string? category = null, Listenarr.Api.Models.SearchRequest? request = null) => Task.FromResult(new List<Listenarr.Domain.Models.IndexerSearchResult>());
            public Task<bool> TestApiConnectionAsync(string apiId) => Task.FromResult(true);
            public Task<List<Listenarr.Domain.Models.IndexerSearchResult>> SearchIndexersAsync(string query, string? category = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false, Listenarr.Api.Models.SearchRequest? request = null) => Task.FromResult(new List<Listenarr.Domain.Models.IndexerSearchResult>());
            public Task<List<ApiConfiguration>> GetEnabledMetadataSourcesAsync() => Task.FromResult(new List<ApiConfiguration>());
        }

        private class TestDownloadQueueService : IDownloadQueueService
        {
            public Task<List<QueueItem>> GetQueueAsync() => Task.FromResult(new List<QueueItem>());
        }

        private class TestCompletedDownloadProcessor : ICompletedDownloadProcessor
        {
            public Task ProcessCompletedAsync(Download download) => Task.CompletedTask;
            public Task ProcessCompletedDownloadAsync(string downloadId, string finalPath) => Task.CompletedTask;
        }

        private class TestAppMetricsService : IAppMetricsService
        {
            public void Increment(string key) { }
            public void Increment(string key, double value) { }
            public void Gauge(string key, double value) { }
            public void Timing(string key, TimeSpan duration) { }
            public void Timer(string key, TimeSpan duration) { }
        }
    }
}
