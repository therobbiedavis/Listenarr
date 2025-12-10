using System;
using System.IO;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class FileMoverFallbackTests : IDisposable
    {
        private readonly string _root;

        public FileMoverFallbackTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "listenarr_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        [Fact]
        public async Task MoveDirectoryAsync_WhenDestinationExists_UsesCopyAndDeleteFallback()
        {
            var source = Path.Combine(_root, "sourceDir");
            var dest = Path.Combine(_root, "destDir");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dest); // cause Directory.Move to throw (destination exists)

            var fileInSource = Path.Combine(source, "track1.mp3");
            await File.WriteAllTextAsync(fileInSource, "dummy");

            var mover = new FileMover(new NullLogger<FileMover>());

            var result = await mover.MoveDirectoryAsync(source, dest);

            Assert.True(result, "MoveDirectoryAsync should succeed via fallback");
            // Source should be removed
            Assert.False(Directory.Exists(source));
            // Destination should contain the file
            var copied = Path.Combine(dest, "track1.mp3");
            Assert.True(File.Exists(copied));
        }

        [Fact]
        public async Task MoveFileAsync_MovesFileSuccessfully()
        {
            var sourceFile = Path.Combine(_root, "a.mp3");
            var destFile = Path.Combine(_root, "b.mp3");
            await File.WriteAllTextAsync(sourceFile, "content");

            var mover = new FileMover(new NullLogger<FileMover>());
            var ok = await mover.MoveFileAsync(sourceFile, destFile);

            Assert.True(ok);
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
        }
    }
}
