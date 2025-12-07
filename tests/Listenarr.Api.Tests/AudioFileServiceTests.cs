using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;
using Listenarr.Domain.Models;
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

        [Fact]
        public async Task EnsureAudiobookFileAsync_RefusesFileOutsideAudiobookFolder_AndCreatesHistory()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);

            // Create audiobook with a legacy FilePath in Author/BookA folder
            var bookA = new Audiobook { Title = "Book A", Authors = new System.Collections.Generic.List<string> { "Author" }, FilePath = Path.Combine(Path.GetTempPath(), "Author", "BookA", "track1.m4b") };
            db.Audiobooks.Add(bookA);
            await db.SaveChangesAsync();

            // Ensure the audiobook directory exists on disk for the containment check
            var bookADir = Path.GetDirectoryName(bookA.FilePath);
            if (!Directory.Exists(bookADir)) Directory.CreateDirectory(bookADir!);

            // Create a file in a sibling folder Author/BookB which should be refused
            var rejectedDir = Path.Combine(Path.GetTempPath(), "Author", "BookB");
            if (!Directory.Exists(rejectedDir)) Directory.CreateDirectory(rejectedDir);
            var rejectedFile = Path.Combine(rejectedDir, $"rejected-{Guid.NewGuid()}.m4b");
            await File.WriteAllTextAsync(rejectedFile, "dummy");

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

            var result = await svc.EnsureAudiobookFileAsync(bookA.Id, rejectedFile, "test-scan");
            Assert.False(result);

            // History entry should be created
            var history = await db.History.FirstOrDefaultAsync(h => h.AudiobookId == bookA.Id && h.EventType == "File Association Refused");
            Assert.NotNull(history);
            Assert.Contains("Refused to associate file", history.Message);
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

