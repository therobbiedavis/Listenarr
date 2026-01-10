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
                // RPC call - expect core.add_torrent_url
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
        public async Task Add_Url_DownloadsTorrent_And_CallsCoreAddTorrentFile()
        {
            string captured = null!;
            var fetched = false;
            var factory = BuildFactory(async req =>
            {
                // If request path contains "/download" (simulating Prowlarr), return torrent bytes
                if (req.RequestUri != null && req.RequestUri.AbsoluteUri.Contains("/download"))
                {
                    fetched = true;
                    var torrentBytes = Encoding.UTF8.GetBytes("d8:announce13:http://tracker/4:infod6:lengthi123e");
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(torrentBytes)
                    };
                    resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-bittorrent");
                    return await Task.FromResult(resp);
                }

                // Otherwise, capture RPC payload and return id for add_torrent_file
                captured = await req.Content.ReadAsStringAsync();
                var body = "{\"result\": \"file-id\"}";
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());

            var client = BuildClientConfig();

            var result = new SearchResult { Title = "Test URL Fetch", TorrentUrl = "https://prowlarr.local/download?apikey=abc&file=t.torrent" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("file-id", id);
            Assert.Contains("core.add_torrent_file", captured, StringComparison.OrdinalIgnoreCase);
            Assert.True(fetched, "Expected the adapter to fetch the torrent URL before calling Deluge RPC");
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
            Assert.Equal(100.0, item.Progress);
            Assert.Equal(1024, item.Downloaded);
        }

        [Fact]
        public async Task GetQueue_ProgressAsPercent_IsHandledCorrectly()
        {
            var factory = BuildFactory(async req =>
            {
                var resultObj = new Dictionary<string, object>
                {
                    ["id2"] = new Dictionary<string, object>
                    {
                        ["name"] = "PercentBook",
                        ["progress"] = 50.0,
                        ["total_size"] = 2000,
                        ["state"] = "downloading",
                        ["save_path"] = "/downloads",
                        ["time_added"] = 1600000000,
                        ["ratio"] = 0.0,
                        ["download_payload_rate"] = 100.0
                    }
                };

                var wrapper = new Dictionary<string, object> { ["result"] = resultObj };
                var body = JsonSerializer.Serialize(wrapper);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));
            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var q = await adapter.GetQueueAsync(client);
            Assert.Single(q);
            var it = q[0];
            Assert.Equal(50.0, it.Progress);
            Assert.Equal(1000, it.Downloaded);
        }

        [Fact]
        public async Task GetQueue_ProgressAsFraction_IsHandledCorrectly()
        {
            var factory = BuildFactory(async req =>
            {
                var resultObj = new Dictionary<string, object>
                {
                    ["id3"] = new Dictionary<string, object>
                    {
                        ["name"] = "FracBook",
                        ["progress"] = 0.5,
                        ["total_size"] = 3000,
                        ["state"] = "downloading",
                        ["save_path"] = "/downloads",
                        ["time_added"] = 1600000000,
                        ["ratio"] = 0.0,
                        ["download_payload_rate"] = 200.0
                    }
                };

                var wrapper = new Dictionary<string, object> { ["result"] = resultObj };
                var body = JsonSerializer.Serialize(wrapper);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));
            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var q = await adapter.GetQueueAsync(client);
            Assert.Single(q);
            var it = q[0];
            Assert.Equal(50.0, it.Progress);
            Assert.Equal(1500, it.Downloaded);
        }

        [Fact]
        public async Task Add_ReturnsNonStringThenDiscoveryFindsTorrent()
        {
            var rpcBodies = new List<string>();
            var getCount = 0;
            var factory = BuildFactory(async req =>
            {
                // read payload to see which RPC method was called
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : string.Empty;
                rpcBodies.Add(body ?? string.Empty);

                // If this is get_torrents_status, return empty on first call and a result on second
                if (body != null && body.Contains("core.get_torrents_status"))
                {
                    getCount++;
                    if (getCount == 1)
                    {
                        var empty = new Dictionary<string, object> { ["result"] = new Dictionary<string, object>() };
                        var respBodyEmpty = JsonSerializer.Serialize(empty);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respBodyEmpty, Encoding.UTF8, "application/json") };
                    }

                    var resultObj = new Dictionary<string, object>
                    {
                        ["foundId"] = new Dictionary<string, object>
                        {
                            ["name"] = "Test Title",
                            ["progress"] = 0.0,
                            ["total_size"] = 100,
                            ["state"] = "downloading",
                            ["save_path"] = "/downloads",
                            ["time_added"] = 1600000000,
                            ["ratio"] = 0.0,
                            ["download_payload_rate"] = 0.0
                        }
                    };
                    var wrapper = new Dictionary<string, object> { ["result"] = resultObj };
                    var respBody = JsonSerializer.Serialize(wrapper);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respBody, Encoding.UTF8, "application/json") };
                }

                // Otherwise - simulate add returning a boolean true (non-string)
                var addBody = "{\"result\": true}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(addBody, Encoding.UTF8, "application/json") };
            });
            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var result = new SearchResult { Title = "Test Title", TorrentUrl = "http://example.com/t.torrent" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("foundId", id);
            Assert.True(rpcBodies.Any(b => b != null && (b.Contains("core.add_torrent_url") || b.Contains("core.add_torrent_file") || b.Contains("core.add_torrent_magnet"))), "Expected an add RPC to be invoked");
            // Ensure discovery retried at least once (get_torrents_status called multiple times)
            Assert.True(getCount >= 2, "Expected multiple get_torrents_status calls during discovery retries");
        }

        [Fact]
        public async Task Add_Url_FileAddReturnsNonStringThenUrlFallbackReturnsId()
        {
            var rpcBodies = new List<string>();
            var factory = BuildFactory(async req =>
            {
                // If request path contains "/download" (simulating Prowlarr), return torrent bytes for the initial fetch
                if (req.RequestUri != null && req.RequestUri.AbsoluteUri.Contains("/download"))
                {
                    var torrentBytes = Encoding.UTF8.GetBytes("d8:announce13:http://tracker/4:infod6:lengthi123e");
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(torrentBytes)
                    };
                    resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-bittorrent");
                    return await Task.FromResult(resp);
                }

                // Capture RPC payload and return boolean true for the first add_torrent_file invocation, then return string id for add_torrent_url fallback
                var captured = req.Content != null ? await req.Content.ReadAsStringAsync() : string.Empty;
                rpcBodies.Add(captured ?? string.Empty);
                if (captured != null && captured.Contains("core.add_torrent_file"))
                {
                    var body = "{\"result\": true}";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                }

                if (captured != null && captured.Contains("core.add_torrent_url"))
                {
                    var body = "{\"result\": \"url-fallback-id\"}";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                }

                // Default fallback
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\": true}", Encoding.UTF8, "application/json") };
            });
            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));

            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var result = new SearchResult { Title = "Test URL Fallback", TorrentUrl = "https://prowlarr.local/download?apikey=abc&file=t.torrent" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("url-fallback-id", id);
            Assert.True(rpcBodies.Any(b => b != null && b.Contains("core.add_torrent_file")), "Expected an add RPC via file upload to be invoked");
            Assert.True(rpcBodies.Any(b => b != null && b.Contains("core.add_torrent_url")), "Expected fallback add RPC via URL to be invoked");
        }

        [Fact]
        public async Task Add_AuthLoginOnNotAuthenticated_RetriesAndSucceeds()
        {
            var bodies = new List<string>();
            var stage = 0;
            var factory = BuildFactory(async req =>
            {
                var content = req.Content != null ? await req.Content.ReadAsStringAsync() : string.Empty;
                bodies.Add(content ?? string.Empty);
                if (content != null && content.Contains("core.add_torrent_url"))
                {
                    if (stage == 0)
                    {
                        stage = 1;
                        var err = "{\"result\": null, \"error\": {\"message\": \"Not authenticated\", \"code\": 1}, \"id\": 1}";
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(err, Encoding.UTF8, "application/json") };
                    }
                    else
                    {
                        var ok = "{\"result\": \"ok-id\"}";
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ok, Encoding.UTF8, "application/json") };
                    }
                }

                if (content != null && content.Contains("auth.login"))
                {
                    var body = "{\"result\": true}";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\": true}", Encoding.UTF8, "application/json") };
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));
            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();
            var result = new SearchResult { Title = "AuthTest", TorrentUrl = "http://example/t.torrent" };

            var id = await adapter.AddAsync(client, result);
            Assert.Equal("ok-id", id);
            Assert.Contains(bodies, b => b.Contains("auth.login"));
        }

        [Fact]
        public async Task GetQueue_RetriesAndReturnsEntries()
        {
            var getCount = 0;
            var capturedBodies = new List<string>();
            var factory = BuildFactory(async req =>
            {
                var body = req.Content != null ? await req.Content.ReadAsStringAsync() : string.Empty;
                if (body != null && body.Contains("core.get_torrents_status"))
                {
                    getCount++;
                    capturedBodies.Add(body ?? string.Empty);
                    if (getCount == 1)
                    {
                        var empty = new Dictionary<string, object> { ["result"] = new Dictionary<string, object>() };
                        var respBodyEmpty = JsonSerializer.Serialize(empty);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respBodyEmpty, Encoding.UTF8, "application/json") };
                    }

                    var resultObj = new Dictionary<string, object>
                    {
                        ["id1"] = new Dictionary<string, object>
                        {
                            ["name"] = "Retried",
                            ["progress"] = 0.0,
                            ["total_size"] = 200,
                            ["state"] = "downloading",
                            ["save_path"] = "/downloads",
                            ["time_added"] = 1600000000,
                            ["ratio"] = 0.0,
                            ["download_payload_rate"] = 0.0
                        }
                    };
                    var wrapper = new Dictionary<string, object> { ["result"] = resultObj };
                    var respBody = JsonSerializer.Serialize(wrapper);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respBody, Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\": true}", Encoding.UTF8, "application/json") };
            });

            var pathMock = new Mock<Listenarr.Api.Services.IRemotePathMappingService>();
            pathMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string p) => Task.FromResult<string>(p));
            var adapter = new DelugeAdapter(factory, pathMock.Object, Mock.Of<ILogger<DelugeAdapter>>());
            var client = BuildClientConfig();

            var q = await adapter.GetQueueAsync(client);
            Assert.Single(q);
            Assert.Equal("id1", q[0].Id);
            Assert.Equal("Retried", q[0].Title);
            Assert.All(capturedBodies, b => Assert.Contains("total_size", b));
        }
    }
}
