using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Api.Services.Adapters;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class DelugeAdapterTests
    {
        private class TestHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

            public TestHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
            {
                _responder = responder ?? throw new ArgumentNullException(nameof(responder));
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _responder(request);
            }
        }

        private static IHttpClientFactory BuildFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            var handler = new TestHandler(responder);
            var client = new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost:8112"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(x => x.CreateClient(It.Is<string>(s => s == "deluge"))).Returns(client);
            return mockFactory.Object;
        }

        private static DownloadClientConfiguration BuildClientConfig()
        {
            return new DownloadClientConfiguration
            {
                Id = "d1",
                Name = "deluge",
                Host = "localhost",
                Port = 8112,
                Type = "deluge",
                Username = string.Empty,
                Password = "pwd",
                UseSSL = false
            };
        }

        [Fact]
        public async Task TestConnection_WithAuthTrue_ReturnsSuccess()
        {
            var factory = BuildFactory(async req =>
            {
                var body = "{\"result\": true}";
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var res = await adapter.TestConnectionAsync(client);
            Assert.True(res.Success);
        }

        [Fact]
        public async Task TestConnection_Unauthorized_Fails()
        {
            var factory = BuildFactory(async req =>
            {
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(string.Empty)
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var res = await adapter.TestConnectionAsync(client);
            Assert.False(res.Success);
            Assert.Contains("Deluge returned", res.Message);
        }

        [Fact]
        public async Task Add_TorrentFile_ReturnsId_And_PayloadContainsMethod()
        {
            string captured = null!;

            var factory = BuildFactory(async req =>
            {
                captured = await req.Content.ReadAsStringAsync();
                var body = "{\"result\": \"abc123\"}";
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());

            var client = BuildClientConfig();
            var result = new SearchResult { Title = "Test", TorrentFileContent = Encoding.UTF8.GetBytes("torrentdata"), TorrentFileName = "t.torrent" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("abc123", id);
            Assert.Contains("core.add_torrent_file", captured, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("t.torrent", captured, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Add_Magnet_CallsCoreAddTorrentMagnet()
        {
            string captured = null!;
            var factory = BuildFactory(async req =>
            {
                captured = await req.Content.ReadAsStringAsync();
                var body = "{\"result\": \"magnet-id\"}";
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());

            var client = BuildClientConfig();
            var result = new SearchResult { Title = "Test Magnet", MagnetLink = "magnet:?xt=urn:btih:xyz" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("magnet-id", id);
            Assert.Contains("core.add_torrent_magnet", captured, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Add_Url_CallsCoreAddTorrentUrl()
        {
            string captured = null!;
            var factory = BuildFactory(async req =>
            {
                captured = await req.Content.ReadAsStringAsync();
                var body = "{\"result\": \"url-id\"}";
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());

            var client = BuildClientConfig();

            var result = new SearchResult { Title = "Test URL", TorrentUrl = "http://example.com/torrent" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("url-id", id);
            Assert.Contains("core.add_torrent_url", captured, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("http://example.com/torrent", captured, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetQueue_ReturnsMappedQueueItem()
        {
            var factory = BuildFactory(async req =>
            {
                // return result object with a single torrent mapping
                var resultObj = new Dictionary<string, object>
                {
                    ["id1"] = new Dictionary<string, object>
                    {
                        ["name"] = "The Book",
                        ["progress"] = 1.0,
                        ["total_size"] = 1024,
                        ["state"] = "seeding",
                        ["save_path"] = "/downloads",
                        ["time_added"] = 1600000000,
                        ["ratio"] = 1.5,
                        ["download_payload_rate"] = 123.0
                    }
                };

                var wrapper = new Dictionary<string, object> { ["result"] = resultObj };
                var body = JsonSerializer.Serialize(wrapper);
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var q = await adapter.GetQueueAsync(client);
            Assert.Single(q);
            var item = q[0];
            Assert.Equal("id1", item.Id);
            Assert.Equal("The Book", item.Title);
            Assert.Equal(1024, item.Size);
            Assert.Equal("seeding", item.Status);
        }
    }
}
