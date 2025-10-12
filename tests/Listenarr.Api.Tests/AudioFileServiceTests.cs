using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;
using Listenarr.Api.Models;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class AudioFileServiceTests
    {
        [Fact]
        public async Task EnsureAudiobookFileAsync_CreatesFileRecord_HappyPath()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var book = new Audiobook { Title = "Test" };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            var testFile = Path.Combine(Path.GetTempPath(), $"afs-test-{Guid.NewGuid()}.m4b");
            await File.WriteAllTextAsync(testFile, "dummy");

            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Duration = TimeSpan.FromSeconds(1234), Format = "m4b", Bitrate = 64000 });

            // Build service provider with required services
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
            Assert.Equal("m4b", file.Format);
        }

        [Fact]
        public async Task EnsureAudiobookFileAsync_HandlesUniqueConstraintViolation_ReturnsFalse()
        {
            // Create a fake DbContext that throws DbUpdateException on SaveChangesAsync
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            // subclass ListenArrDbContext to override SaveChangesAsync
            var db = new ThrowingSaveChangesDbContext(options);

            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>())).ReturnsAsync(new AudioMetadata());

            var services = new ServiceCollection();
            services.AddSingleton<IMetadataService>(metadataMock.Object);
            services.AddSingleton<ListenArrDbContext>(db);
            services.AddSingleton<MetadataExtractionLimiter>();
            services.AddMemoryCache();

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<AudioFileService>>();
            var svc = new AudioFileService(scopeFactory, loggerMock.Object, provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), provider.GetRequiredService<MetadataExtractionLimiter>());

            var result = await svc.EnsureAudiobookFileAsync(1, "C:\\fake\\path.m4b", "test");
            Assert.False(result);
        }

        // Test helper DbContext that throws on SaveChangesAsync
        private class ThrowingSaveChangesDbContext : ListenArrDbContext
        {
            public ThrowingSaveChangesDbContext(DbContextOptions<ListenArrDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                throw new DbUpdateException("Constraint failed", new Exception("UNIQUE constraint failed: AudiobookFiles.AudiobookId, Path"));
            }
        }
    }
}
