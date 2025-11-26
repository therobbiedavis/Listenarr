using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;

namespace Listenarr.Api.Tests
{
    public class DownloadMonitorFinalizationTests
    {
        private ListenArrDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ListenArrDbContext(options);
        }

        private ServiceProvider BuildServiceProvider(ListenArrDbContext db, Mock<IDownloadService> downloadServiceMock, ApplicationSettings settings)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            // Provide a minimal file naming service that won't be used (metadata disabled in settings)
            var fileNamingMock = new Mock<IFileNamingService>();
            services.AddSingleton<IFileNamingService>(fileNamingMock.Object);
            // metadata service
            var metadataMock = new Mock<IMetadataService>();
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task PollSABnzbd_Queue_StringFields_UpdateProgress()
        {
            var db = CreateInMemoryDb();

            // Seed download (simulating a SABnzbd download record with DownloadClientId set to the NZO ID)
            var download = new Download
            {
                Id = "dq1",
                Title = "William Faulkner - The Sound and the Fury",
                Status = DownloadStatus.Queued,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                DownloadClientId = "SABnzbd_nzo_20f9svw_",
                StartedAt = DateTime.UtcNow,
                TotalSize = (long)(100 * 1024 * 1024) // 100 MB
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Setup fake HTTP handler that returns queue JSON where numeric values are strings
            var handler = new DelegatingHandlerMock((req, ct) =>
            {
                var q = req.RequestUri?.Query ?? string.Empty;
                if (q.Contains("mode=queue"))
                {
                    var queueJson = "{\"queue\":{\"slots\":[{\"nzo_id\":\"SABnzbd_nzo_20f9svw_\",\"filename\":\"William Faulkner - The Sound and the Fury\",\"percentage\":\"50.5\",\"mb\":\"100.0\",\"mbleft\":\"49.5\",\"status\":\"Downloading\"}]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(queueJson) });
                }

                if (q.Contains("mode=history"))
                {
                    // keep history empty
                    var historyJson = "{\"history\":{\"slots\":[]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(historyJson) });
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            var settings = new ApplicationSettings { OutputPath = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString()), EnableMetadataProcessing = false, CompletedFileAction = "Move", AllowedFileExtensions = new List<string>{ ".m4b" } };
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            var downloadServiceMock = new Mock<IDownloadService>();
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            var fileNamingMock = new Mock<IFileNamingService>();
            services.AddSingleton<IFileNamingService>(fileNamingMock.Object);
            var metadataMock = new Mock<IMetadataService>();
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object);

            // Invoke private PollSABnzbdAsync via reflection
            var method = typeof(DownloadMonitorService).GetMethod("PollSABnzbdAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c-queue-test", Name = "Sabnzbd", Host = "localhost", Port = 8080, UseSSL = false, Settings = new Dictionary<string, object> { { "apiKey", "apikey" } }, DownloadPath = "/downloads/complete" };

            var downloads = new List<Download> { download };

            var task = (Task?)method.Invoke(monitor, new object[] { clientConfig, downloads, db, CancellationToken.None });
            if (task != null) await task;

            // Verify the DB download was updated with progress ~50.5 (progress is stored as decimal)
            var updated = await db.Downloads.FindAsync(download.Id);
            Assert.NotNull(updated);
            Assert.True(updated.Progress > 50 && updated.Progress < 51);
            // downloaded size should reflect ~50.5% of 100 MB -> ~50.5 MB
            Assert.True(updated.DownloadedSize > 50 * 1024 * 1024 && updated.DownloadedSize < 51 * 1024 * 1024);
        }
        [Fact]
        public async Task PollSABnzbd_DoesNotThrow_WhenClientDownloadPathEmpty()
        {
            var db = CreateInMemoryDb();

            // Seed download (simulating a SABnzbd download record with DownloadClientId set to the NZO ID)
            var download = new Download
            {
                Id = "d5",
                Title = "William Faulkner - The Sound and the Fury",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                DownloadClientId = "SABnzbd_nzo_9plcy_gj",
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Settings: move to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Move", AllowedFileExtensions = new List<string>{ ".m4b" } };

            var downloadServiceMock = new Mock<IDownloadService>();
            // We expect ProcessCompletedDownloadAsync not to be called because no file will be found
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            // Setup fake HTTP handler that returns history JSON responses
            var remotePath = "/downloads/complete/listenarr/William Faulkner - The Sound and the Fury.4";
            var handler = new DelegatingHandlerMock((req, ct) =>
            {
                var q = req.RequestUri?.Query ?? string.Empty;
                if (q.Contains("mode=queue"))
                {
                    var queueJson = "{\"queue\":{\"slots\":[]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(queueJson) });
                }

                if (q.Contains("mode=history"))
                {
                    // history 'storage' contains the remote path with the .4 suffix
                    var historyJson = $"{{\"history\":{{\"slots\":[{{\"nzo_id\":\"SABnzbd_nzo_9plcy_gj\",\"name\":\"William Faulkner - The Sound and the Fury\",\"status\":\"Completed\",\"storage\":\"{remotePath.Replace("\\","\\\\")}\",\"completed\":1600000000}}]}}}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(historyJson) });
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Build DI provider with a path mapping mock that maps the remote path to a non-existing local path
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            var fileNamingMock = new Mock<IFileNamingService>();
            services.AddSingleton<IFileNamingService>(fileNamingMock.Object);
            var metadataMock = new Mock<IMetadataService>();
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            var pathMappingMock = new Mock<IRemotePathMappingService>();
            var mappedLocal = Path.Combine("Z:", "Server", "Test", "William Faulkner - The Sound and the Fury.4");
            pathMappingMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("/William Faulkner - The Sound and the Fury.4"))))
                .ReturnsAsync(mappedLocal);

            services.AddSingleton<IRemotePathMappingService>(pathMappingMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();
            var metricsMock = new Mock<IAppMetricsService>();

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object, metricsMock.Object);

            // Set completion candidate to an older timestamp so it will finalize immediately
            var field = typeof(DownloadMonitorService).GetField("_completionCandidates", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var candidates = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            candidates[download.Id] = DateTime.UtcNow - TimeSpan.FromSeconds(20);

            // Invoke private PollSABnzbdAsync via reflection with client.DownloadPath empty
            var method = typeof(DownloadMonitorService).GetMethod("PollSABnzbdAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "1763948475200-ywwemp9kd", Name = "Sabnzbd", Host = "localhost", Port = 8080, UseSSL = false, Settings = new Dictionary<string, object> { { "apiKey", "apikey" } }, DownloadPath = string.Empty };

            var downloads = new List<Download> { download };

            // If FinalizeDownloadAsync throws due to unguarded Replace calls when DownloadPath is empty, this will blow up.
            var task = (Task?)method.Invoke(monitor, new object[] { clientConfig, downloads, db, CancellationToken.None });
            if (task != null) await task;

            // Finalization should have completed gracefully and candidate should be removed
            var candidatesAfter = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            Assert.False(candidatesAfter.ContainsKey(download.Id));

            // Ensure that ProcessCompletedDownloadAsync was not invoked (no file found/mapped)
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        [Fact]
        public async Task PollSABnzbd_Mapping_StripsNumericSuffix_AndFinalizesDownload()
        {
            var db = CreateInMemoryDb();

            // Seed download (simulating a SABnzbd download record with DownloadClientId set to the NZO ID)
            var download = new Download
            {
                Id = "d4",
                Title = "William Faulkner - The Sound and the Fury",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                DownloadClientId = "SABnzbd_nzo_9plcy_gj",
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create a file under a directory WITHOUT the numeric suffix (this is the real local layout)
            var root = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(root);

            var realDir = Path.Combine(root, "William Faulkner - The Sound and the Fury");
            Directory.CreateDirectory(realDir);

            var sourceFile = Path.Combine(realDir, "The Sound and the Fury.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Settings: move to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Move", AllowedFileExtensions = new List<string>{ ".m4b" } };

            var downloadServiceMock = new Mock<IDownloadService>();
            // Use TaskCompletionSource so test waits deterministically for finalization instead of relying on fixed delays
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => tcs.TrySetResult(true))
                .Returns(Task.CompletedTask);

            // Build DI provider with a path mapping mock that *maps* a remote path with a '.1' suffix
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            var fileNamingMock = new Mock<IFileNamingService>();
            services.AddSingleton<IFileNamingService>(fileNamingMock.Object);
            var metadataMock = new Mock<IMetadataService>();
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            var pathMappingMock = new Mock<IRemotePathMappingService>();
            // When asked to translate the remote path which contains the '.1' suffix,
            // return a local path with the same suffix so our heuristic must strip it.
            var remotePath = "/downloads/complete/listenarr/William Faulkner - The Sound and the Fury.1";
            var mappedLocal = Path.Combine(root, "William Faulkner - The Sound and the Fury.1");
            pathMappingMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("/William Faulkner - The Sound and the Fury.1"))))
                .ReturnsAsync(mappedLocal);

            services.AddSingleton<IRemotePathMappingService>(pathMappingMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();

            // Setup fake HTTP handler that returns queue and history JSON responses
            var handler = new DelegatingHandlerMock((req, ct) =>
            {
                var q = req.RequestUri?.Query ?? string.Empty;
                if (q.Contains("mode=queue"))
                {
                    var queueJson = "{\"queue\":{\"slots\":[]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(queueJson) });
                }

                if (q.Contains("mode=history"))
                {
                    // Note: history 'storage' contains the remote path WITH the .1 suffix
                    var historyJson = $"{{\"history\":{{\"slots\":[{{\"nzo_id\":\"SABnzbd_nzo_9plcy_gj\",\"name\":\"William Faulkner - The Sound and the Fury\",\"status\":\"Completed\",\"storage\":\"{remotePath.Replace("\\","\\\\")}\",\"completed\":1600000000}}]}}}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(historyJson) });
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var metricsMock = new Mock<IAppMetricsService>();
            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object, metricsMock.Object);

            // Set completion candidate to an older timestamp so it will finalize immediately
            var field = typeof(DownloadMonitorService).GetField("_completionCandidates", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var candidates = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            candidates[download.Id] = DateTime.UtcNow - TimeSpan.FromSeconds(20);

            // Invoke private PollSABnzbdAsync via reflection
            var method = typeof(DownloadMonitorService).GetMethod("PollSABnzbdAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "1763948475200-ywwemp9kd", Name = "Sabnzbd", Host = "localhost", Port = 8080, UseSSL = false, Settings = new Dictionary<string, object> { { "apiKey", "apikey" } } };

            var downloads = new List<Download> { download };

            var task = (Task?)method.Invoke(monitor, new object[] { clientConfig, downloads, db, CancellationToken.None });
            if (task != null) await task;

            // After finalization, the completion candidate should be removed and the ProcessCompletedDownloadAsync should have been invoked
            var candidatesAfter = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            Assert.False(candidatesAfter.ContainsKey(download.Id));

            // Ensure our mocked ProcessCompletedDownloadAsync was called (finalization worked)
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(download.Id, It.IsAny<string>()), Times.AtLeastOnce);

            // Validate metrics: stripping numeric suffix should have been used
            metricsMock.Verify(m => m.Increment("finalize.heuristic.strip_suffix", It.IsAny<double>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task PollSABnzbd_SchedulesRetry_AndFinalizes_WhenFileArrives()
        {
            var db = CreateInMemoryDb();

            // Seed download
            var download = new Download
            {
                Id = "d-retry",
                Title = "William Faulkner - The Sound and the Fury",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                DownloadClientId = "SABnzbd_nzo_retry",
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Build DI provider with settings that enable quick retries
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings
            {
                OutputPath = outDir,
                EnableMetadataProcessing = false,
                CompletedFileAction = "Move",
                AllowedFileExtensions = new List<string>{ ".m4b" },
                MissingSourceRetryInitialDelaySeconds = 1,
                MissingSourceMaxRetries = 3
            };

            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            var mappedLocal = Path.Combine(tempRoot, "William Faulkner - The Sound and the Fury");
            // Note: Do NOT create directory yet; initial finalize will not find files

            // Use TaskCompletionSource so tests can deterministically wait for finalization
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var downloadServiceMock = new Mock<IDownloadService>();
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => tcs.TrySetResult(true))
                .Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            var fileNamingMock = new Mock<IFileNamingService>();
            services.AddSingleton<IFileNamingService>(fileNamingMock.Object);
            var metadataMock = new Mock<IMetadataService>();
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            var pathMappingMock = new Mock<IRemotePathMappingService>();
            var remotePath = "/downloads/complete/listenarr/William Faulkner - The Sound and the Fury";
            pathMappingMock.Setup(p => p.TranslatePathAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("William Faulkner - The Sound and the Fury"))))
                .ReturnsAsync(mappedLocal);
            services.AddSingleton<IRemotePathMappingService>(pathMappingMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();
            var metricsMock = new Mock<IAppMetricsService>();

            // Setup fake HTTP handler that returns history JSON with the remote path
            var handler = new DelegatingHandlerMock((req, ct) =>
            {
                var q = req.RequestUri?.Query ?? string.Empty;
                if (q.Contains("mode=queue"))
                {
                    var queueJson = "{\"queue\":{\"slots\":[]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(queueJson) });
                }

                if (q.Contains("mode=history"))
                {
                    var historyJson = $"{{\"history\":{{\"slots\":[{{\"nzo_id\":\"SABnzbd_nzo_retry\",\"name\":\"William Faulkner - The Sound and the Fury\",\"status\":\"Completed\",\"storage\":\"{remotePath.Replace("\\","\\\\")}\",\"completed\":1600000000}}]}}}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(historyJson) });
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object, metricsMock.Object);

            // Set completion candidate to an older timestamp so it will finalize immediately
            var field = typeof(DownloadMonitorService).GetField("_completionCandidates", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var candidates = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            candidates[download.Id] = DateTime.UtcNow - TimeSpan.FromSeconds(20);

            var clientConfig = new DownloadClientConfiguration { Id = "c-retry", Name = "Sabnzbd", Host = "localhost", Port = 8080, UseSSL = false, Settings = new Dictionary<string, object> { { "apiKey", "apikey" } }, DownloadPath = string.Empty };

            // Start poll (initial run) which should detect missing file and schedule a retry
            var method = typeof(DownloadMonitorService).GetMethod("PollSABnzbdAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var downloads = new List<Download> { download };

            var task = (Task?)method.Invoke(monitor, new object[] { clientConfig, downloads, db, CancellationToken.None });
            if (task != null) await task;

            // Wait a short time then create the file so the scheduled retry will find it
            await Task.Delay(200);
            Directory.CreateDirectory(mappedLocal);
            var sourceFile = Path.Combine(mappedLocal, "The Sound and the Fury.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Wait for the finalization to occur (timeout after a short window so CI is faster)
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(completed == tcs.Task, "ProcessCompletedDownloadAsync was not invoked within expected time");
        }
        [Fact]
        public async Task FinalizeDownload_MovesFile_WhenSettingIsMove()
        {
            var db = CreateInMemoryDb();

            // Seed download
            var download = new Download
            {
                Id = "d1",
                Title = "Test Move",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create source file
            var tempDir = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "Test Move.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Settings: move to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Move", AllowedFileExtensions = new List<string>{ ".m4b" } };

            var downloadServiceMock = new Mock<IDownloadService>();
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            var provider = BuildServiceProvider(db, downloadServiceMock, settings);
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object);

            // verify factory usage not required for this test

            // Sanity-check: ensure the monitor has the expected IHttpClientFactory instance
            var factoryField = typeof(DownloadMonitorService).GetField("_httpClientFactory", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(factoryField);
            var factoryVal = factoryField.GetValue(monitor);
            Assert.Equal(httpFactoryMock.Object, factoryVal);

            // (No-op) - http factory used only for qBittorrent test here

            // Invoke private FinalizeDownloadAsync via reflection
            var method = typeof(DownloadMonitorService).GetMethod("FinalizeDownloadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c1", Name = "Local", DownloadPath = tempDir };

            var task = (Task?)method.Invoke(monitor, new object[] { download, tempDir, clientConfig, CancellationToken.None });
            if (task != null) await task;

            // No factory usage expected for direct finalization test

            // Expect file moved to output
            var destFile = Path.Combine(outDir, Path.GetFileName(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.False(File.Exists(sourceFile));
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(download.Id, It.Is<string>(s => s == destFile)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task PollSABnzbd_MatchesHistoryByNzoId_AndFinalizesDownload()
        {
            var db = CreateInMemoryDb();

            // Seed download (simulating a SABnzbd download record with DownloadClientId set to the NZO ID)
            var download = new Download
            {
                Id = "d3",
                Title = "Test NZO",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                DownloadClientId = "SABnzbd_nzo_abc123",
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create a file in a temporary directory to represent the completed file
            var tempDir = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "Test NZO.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Settings: move to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Move", AllowedFileExtensions = new List<string>{ ".m4b" } };

            var downloadServiceMock = new Mock<IDownloadService>();
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            var provider = BuildServiceProvider(db, downloadServiceMock, settings);
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();

            // Setup fake HTTP handler that returns queue and history JSON responses
            var handler = new DelegatingHandlerMock((req, ct) =>
            {
                var q = req.RequestUri?.Query ?? string.Empty;
                if (q.Contains("mode=queue"))
                {
                    var queueJson = "{\"queue\":{\"slots\":[]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(queueJson) });
                }

                if (q.Contains("mode=history"))
                {
                    var historyJson = $"{{\"history\":{{\"slots\":[{{\"nzo_id\":\"SABnzbd_nzo_abc123\",\"name\":\"Test NZO\",\"status\":\"Completed\",\"storage\":\"{sourceFile.Replace("\\","\\\\")}\",\"completed\":1600000000}}]}}}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(historyJson) });
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Callback<string?>(name => Console.WriteLine($"Mock factory CreateClient called with name: '{name ?? "<null>"}'"))
                .Returns(httpClient);
            // Ensure CreateClient(null) also returns our HttpClient (CreateClient may be called without a name)
            httpFactoryMock.Setup(f => f.CreateClient((string?)null)).Returns(httpClient);

            // Sanity-check our mock HttpClient handler works as expected
            var selfResp = await httpClient.GetAsync($"http://localhost:8080/api?mode=history&output=json&apikey=apikey");
            Assert.True(selfResp.IsSuccessStatusCode);

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object);

            // Set completion candidate to an older timestamp so it will finalize immediately
            var field = typeof(DownloadMonitorService).GetField("_completionCandidates", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var candidates = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            candidates[download.Id] = DateTime.UtcNow - TimeSpan.FromSeconds(20);

            // Invoke private PollSABnzbdAsync via reflection
            var method = typeof(DownloadMonitorService).GetMethod("PollSABnzbdAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c3", Name = "Sabnzbd", Host = "localhost", Port = 8080, UseSSL = false, Settings = new Dictionary<string, object> { { "apiKey", "apikey" } } };

            var downloads = new List<Download> { download };

            var task = (Task?)method.Invoke(monitor, new object[] { clientConfig, downloads, db, CancellationToken.None });
            if (task != null) await task;

            // We expect the completion candidate to be removed (finalization attempted)
            var candidatesAfter = (Dictionary<string, DateTime>)field.GetValue(monitor)!;
            Assert.False(candidatesAfter.ContainsKey(download.Id));
        }

        // Simple DelegatingHandler mock helper
        private class DelegatingHandlerMock : DelegatingHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

            public DelegatingHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
            {
                _handlerFunc = handlerFunc;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Instrument the handler for debugging
                try
                {
                    Console.WriteLine($"DelegatingHandlerMock invoked for: {request.Method} {request.RequestUri}");
                }
                catch { }
                return _handlerFunc(request, cancellationToken);
            }
        }

        

        [Fact]
        public async Task FinalizeDownload_CopiesFile_WhenSettingIsCopy()
        {
            var db = CreateInMemoryDb();

            // Seed download
            var download = new Download
            {
                Id = "d2",
                Title = "Test Copy",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create source file
            var tempDir = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "Test Copy.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Settings: copy to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Copy" };

            var downloadServiceMock = new Mock<IDownloadService>();
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            var provider = BuildServiceProvider(db, downloadServiceMock, settings);
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object);

            var method = typeof(DownloadMonitorService).GetMethod("FinalizeDownloadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c2", Name = "Local", DownloadPath = tempDir };

            var task = (Task?)method.Invoke(monitor, new object[] { download, tempDir, clientConfig, CancellationToken.None });
            if (task != null) await task;

            // Expect file copied to output
            var destFile = Path.Combine(outDir, Path.GetFileName(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.True(File.Exists(sourceFile));
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(download.Id, It.Is<string>(s => s == destFile)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task FinalizeDownload_SkipsError_WhenBackgroundJobActive()
        {
            var db = CreateInMemoryDb();

            var download = new Download
            {
                Id = "skip-1",
                Title = "Test Skip",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                StartedAt = DateTime.UtcNow
            };

            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);

            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Move", AllowedFileExtensions = new System.Collections.Generic.List<string>{ ".m4b" } };

            var downloadServiceMock = new Mock<IDownloadService>();
            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();
            var metricsMock = new Mock<IAppMetricsService>();

            // Build DI provider and register a processing queue service that returns an active job
            var processingQueueMock = new Mock<IDownloadProcessingQueueService>();
            var job = new DownloadProcessingJob { Id = Guid.NewGuid().ToString(), DownloadId = download.Id, Status = ProcessingJobStatus.Processing };
            processingQueueMock.Setup(q => q.GetJobsForDownloadAsync(download.Id)).ReturnsAsync(new System.Collections.Generic.List<DownloadProcessingJob> { job });

            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            services.AddSingleton<IFileNamingService>(new Mock<IFileNamingService>().Object);
            services.AddSingleton<IMetadataService>(new Mock<IMetadataService>().Object);
            services.AddSingleton<IDownloadProcessingQueueService>(processingQueueMock.Object);
            services.AddSingleton<IAppMetricsService>(metricsMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object, httpFactoryMock.Object, metricsMock.Object);

            // Call FinalizeDownloadAsync with an empty client path so no source file is found
            var method = typeof(DownloadMonitorService).GetMethod("FinalizeDownloadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c-skip", Name = "Local", DownloadPath = string.Empty };

            var task = (Task?)method.Invoke(monitor, new object[] { download, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), clientConfig, CancellationToken.None });
            if (task != null) await task;

            // Since a processing job was present, we expect the monitor to NOT increment the file_not_found metric
            metricsMock.Verify(m => m.Increment("finalize.failed.file_not_found", It.IsAny<double>()), Times.Never);
            // Also ensure ProcessCompletedDownloadAsync wasn't called
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
