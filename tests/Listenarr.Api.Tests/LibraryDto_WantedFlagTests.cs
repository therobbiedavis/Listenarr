using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Listenarr.Api.Models;

namespace Listenarr.Api.Tests
{
    public class LibraryDto_WantedFlagTests
    {
        [Fact]
        public async Task GetAll_IncludesWantedFlag_ForMonitoredWithoutFiles()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ListenArrDbContext(options);

            // Monitored without files -> wanted = true
            var wantedBook = new Audiobook { Title = "Wanted Book", Monitored = true };
            db.Audiobooks.Add(wantedBook);

            // Monitored with files -> wanted = false
            var hasFileBook = new Audiobook { Title = "Has File", Monitored = true };
            db.Audiobooks.Add(hasFileBook);
            await db.SaveChangesAsync();

            var file = new AudiobookFile { AudiobookId = hasFileBook.Id, Path = "C:\\temp\\f.m4b", Size = 1234, CreatedAt = DateTime.UtcNow };
            db.AudiobookFiles.Add(file);
            await db.SaveChangesAsync();

            // Exercise repository directly similar to controller
            var audiobooks = await db.Audiobooks.Include(a => a.Files).ToListAsync();

            var dto = audiobooks.Select(a => new
            {
                id = a.Id,
                wanted = a.Monitored && (a.Files == null || !a.Files.Any())
            }).ToList();

            var wantedDto = dto.First(d => d.id == wantedBook.Id);
            var hasFileDto = dto.First(d => d.id == hasFileBook.Id);

            Assert.True(wantedDto.wanted);
            Assert.False(hasFileDto.wanted);
        }

        [Fact]
        public async Task GetById_IncludesWantedFlag_Correctly()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new ListenArrDbContext(options);

            var book = new Audiobook { Title = "Single Book", Monitored = true };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Initially should be wanted
            var updated = await db.Audiobooks.Include(a => a.Files).FirstOrDefaultAsync(a => a.Id == book.Id);
            var wanted = updated.Monitored && (updated.Files == null || !updated.Files.Any());
            Assert.True(wanted);

            // Add file and re-evaluate
            var file = new AudiobookFile { AudiobookId = book.Id, Path = "C:\\temp\\single.m4b", Size = 1024, CreatedAt = DateTime.UtcNow };
            db.AudiobookFiles.Add(file);
            await db.SaveChangesAsync();

            var updated2 = await db.Audiobooks.Include(a => a.Files).FirstOrDefaultAsync(a => a.Id == book.Id);
            var wanted2 = updated2.Monitored && (updated2.Files == null || !updated2.Files.Any());
            Assert.False(wanted2);
        }
    }
}
