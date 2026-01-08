using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Moq;
using Listenarr.Infrastructure.Models;
using Listenarr.Api.Repositories;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    public class RootFolderServiceTests
    {
        [Fact]
        public async Task Create_Throws_WhenPathDuplicate()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            db.RootFolders.Add(new RootFolder { Name = "A", Path = "C:\\books" });
            await db.SaveChangesAsync();

            var dbFactory = new TestDbFactory(options);
            var repo = new EfRootFolderRepository(dbFactory, null!);
            var svc = new RootFolderService(repo, dbFactory, null!);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(new RootFolder { Name = "B", Path = "C:\\books" }));
        }

        [Fact]
        public async Task Delete_Throws_WhenReferencedWithoutReassign()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var root = new RootFolder { Name = "A", Path = "C:\\books" };
            db.RootFolders.Add(root);
            db.Audiobooks.Add(new Domain.Models.Audiobook { Title = "T", BasePath = "C:\\books" });
            await db.SaveChangesAsync();

            var dbFactory = new TestDbFactory(options);
            var repo = new EfRootFolderRepository(dbFactory, null!);
            var svc = new RootFolderService(repo, dbFactory, null!);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAsync(root.Id));
        }

        // Rename behavior tests
        [Fact]
        public async Task Update_RenameWithoutMove_UpdatesAudiobookBasePaths()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var root = new RootFolder { Name = "R", Path = "C:\\root" };
            db.RootFolders.Add(root);
            db.Audiobooks.Add(new Domain.Models.Audiobook { Title = "A1", BasePath = "C:\\root\\Author\\Title" });
            db.Audiobooks.Add(new Domain.Models.Audiobook { Title = "A2", BasePath = "C:\\root" });
            await db.SaveChangesAsync();

            var dbFactory = new TestDbFactory(options);
            var repo = new EfRootFolderRepository(dbFactory, null!);
            var svc = new RootFolderService(repo, dbFactory, null!);

            var updated = await svc.UpdateAsync(new RootFolder { Id = root.Id, Name = "R2", Path = "D:\\newroot" }, moveFiles: false);

            // Verify audiobooks' basepaths updated (use a fresh context)
            using (var verifyDb = new ListenArrDbContext(options))
            {
                var a1 = verifyDb.Audiobooks.First(a => a.Title == "A1").BasePath;
                var a2 = verifyDb.Audiobooks.First(a => a.Title == "A2").BasePath;
                if (a1 != "D:\\newroot\\Author\\Title" || a2 != "D:\\newroot")
                {
                    var dump = string.Join("; ", verifyDb.Audiobooks.Select(a => $"{a.Title} => {a.BasePath}"));
                    Assert.True(false, $"Unexpected audiobook base paths after root update. Dump: {dump}");
                }
                Assert.Equal("D:\\newroot\\Author\\Title", a1);
                Assert.Equal("D:\\newroot", a2);
            }
        }

        [Fact]
        public async Task Update_RenameWithMove_EnqueuesMovesAndUpdatesDB()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);
            var root = new RootFolder { Name = "R", Path = "C:\\root" };
            db.RootFolders.Add(root);
            var ab1 = new Domain.Models.Audiobook { Id = 1, Title = "A1", BasePath = "C:\\root\\Author\\Title" };
            var ab2 = new Domain.Models.Audiobook { Id = 2, Title = "A2", BasePath = "C:\\root" };
            db.Audiobooks.AddRange(ab1, ab2);
            await db.SaveChangesAsync();

            var dbFactory = new TestDbFactory(options);
            var repo = new EfRootFolderRepository(dbFactory, null!);

            var mockMove = new Moq.Mock<IMoveQueueService>();
            mockMove.Setup(m => m.EnqueueMoveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid());

            var svc = new RootFolderService(repo, dbFactory, null!, mockMove.Object);

            var updated = await svc.UpdateAsync(new RootFolder { Id = root.Id, Name = "R2", Path = "D:\\newroot" }, moveFiles: true);

            // Verify DB changed (use fresh context)
            using (var verifyDb = new ListenArrDbContext(options))
            {
                var a1 = verifyDb.Audiobooks.First(a => a.Title == "A1").BasePath;
                var a2 = verifyDb.Audiobooks.First(a => a.Title == "A2").BasePath;
                if (a1 != "D:\\newroot\\Author\\Title" || a2 != "D:\\newroot")
                {
                    var dump = string.Join("; ", verifyDb.Audiobooks.Select(a => $"{a.Title} => {a.BasePath}"));
                    Assert.True(false, $"Unexpected audiobook base paths after root update (with move). Dump: {dump}");
                }
                Assert.Equal("D:\\newroot\\Author\\Title", a1);
                Assert.Equal("D:\\newroot", a2);
            }

            // Ensure moves enqueued for both audiobooks
            mockMove.Verify(m => m.EnqueueMoveAsync(1, "D:\\newroot\\Author\\Title", "C:\\root\\Author\\Title"), Times.Once);
            mockMove.Verify(m => m.EnqueueMoveAsync(2, "D:\\newroot", "C:\\root"), Times.Once);
        }

        // Minimal test db factory to satisfy IDbContextFactory<T>
        private class TestDbFactory : IDbContextFactory<ListenArrDbContext>
        {
            private readonly DbContextOptions<ListenArrDbContext> _options;
            public TestDbFactory(DbContextOptions<ListenArrDbContext> options) { _options = options; }
            public Task<ListenArrDbContext> CreateDbContextAsync() => Task.FromResult(new ListenArrDbContext(_options));
            public ListenArrDbContext CreateDbContext() => new ListenArrDbContext(_options);
        }
    }
}