using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Listenarr.Api.Services;
using Listenarr.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Listenarr.Api.Tests
{
    public class AudioFileService_UpdateAudiobookFieldsTests
    {
        [Fact]
        public async Task EnsureAudiobookFileAsync_PopulatesAudiobookFilePathAndSize()
        {
            // Arrange - create in-memory db
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var audiobook = new Audiobook { Title = "Test Book", Monitored = true };
            db.Audiobooks.Add(audiobook);
            await db.SaveChangesAsync();

            // Build service provider with required services
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            services.AddSingleton<MetadataExtractionLimiter>();
            services.AddMemoryCache();
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var logger = new NullLogger<AudioFileService>();
            var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var limiter = provider.GetRequiredService<MetadataExtractionLimiter>();

            var svc = new AudioFileService(scopeFactory, logger, memoryCache, limiter);

            // Use temp file
            var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"afs-test-{Guid.NewGuid()}.m4b");
            System.IO.File.WriteAllText(tempFile, "dummy");

            // Act
            var created = await svc.EnsureAudiobookFileAsync(audiobook.Id, tempFile, "test");

            // Assert
            Assert.True(created);
            var updated = await db.Audiobooks.FindAsync(audiobook.Id);
            Assert.NotNull(updated.FilePath);
            Assert.True(updated.FilePath.Contains(System.IO.Path.GetFileName(tempFile)) || updated.FilePath == tempFile);
            Assert.True(updated.FileSize > 0);

            // Cleanup
            System.IO.File.Delete(tempFile);
        }
    }
}
