using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;
using Listenarr.Api.Models;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class MetadataIntegrationTests
    {
        [Fact]
        public async Task EnsureAudiobookFileAsync_PersistsMetadataFromMetadataService()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var book = new Audiobook { Title = "IntegrationTest" };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            var testFile = Path.Combine(Path.GetTempPath(), $"meta-int-{Guid.NewGuid()}.m4b");
            await File.WriteAllTextAsync(testFile, "dummy");

            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Duration = TimeSpan.FromSeconds(3210), Format = "m4b", Bitrate = 64000, SampleRate = 32000, Channels = 1 });

            var services = new ServiceCollection();
            services.AddSingleton<IMetadataService>(metadataMock.Object);
            services.AddSingleton(db);
            services.AddSingleton<MetadataExtractionLimiter>();
            services.AddMemoryCache();

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<AudioFileService>>();
            var svc = new AudioFileService(scopeFactory, loggerMock.Object, provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), provider.GetRequiredService<MetadataExtractionLimiter>());

            var created = await svc.EnsureAudiobookFileAsync(book.Id, testFile, "test");
            Assert.True(created);

            var file = await db.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == book.Id && f.Path == testFile);
            Assert.NotNull(file);
            Assert.Equal(3210, (int)file.DurationSeconds!.Value);
            Assert.Equal("m4b", file.Format);
            Assert.Equal(64000, file.Bitrate);
            Assert.Equal(32000, file.SampleRate);
            Assert.Equal(1, file.Channels);
        }
    }
}
