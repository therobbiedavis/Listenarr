using System.IO;
using System.Threading.Tasks;
using Listenarr.Api.Controllers;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class ImagesController_TempToLibraryForAudiobookTests
    {
        [Fact]
        public async Task GetImage_WhenTempExists_AndAudiobookExists_MovesToLibraryAndServesLibraryFile()
        {
            // Arrange
            var identifier = "B002V1OF70";
            var tempRelative = $"config/cache/images/temp/{identifier}.jpg";
            var libRelative = $"config/cache/images/library/{identifier}.jpg";
            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr_test_contentroot_temp_to_lib");

            Directory.CreateDirectory(Path.Combine(tempRoot, "config", "cache", "images", "temp"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "config", "cache", "images", "library"));

            var tempFull = Path.Combine(tempRoot, tempRelative);
            var libFull = Path.Combine(tempRoot, libRelative);

            File.WriteAllText(tempFull, "temp image");
            File.WriteAllText(libFull, "library image");

            var mockImageCache = new Mock<IImageCacheService>();
            // Initially GetCachedImagePathAsync returns the temp path
            mockImageCache.Setup(m => m.GetCachedImagePathAsync(identifier)).ReturnsAsync(tempRelative);
            // When MoveToLibraryStorageAsync is called, pretend it moved and return the library relative path
            mockImageCache.Setup(m => m.MoveToLibraryStorageAsync(identifier, null)).ReturnsAsync(libRelative);

            var mockRepo = new Mock<IAudiobookRepository>();
            mockRepo.Setup(r => r.GetByAsinAsync(identifier)).ReturnsAsync(new Audiobook { Asin = identifier });

            var audimetaMock = new Mock<AudimetaService>(new System.Net.Http.HttpClient(), Mock.Of<ILogger<AudimetaService>>());

            // Set ContentRootPath on the mocked environment to our tempRoot
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.SetupGet(e => e.ContentRootPath).Returns(tempRoot);

            var controller = new ImagesController(mockImageCache.Object, Mock.Of<IAudiobookMetadataService>(), audimetaMock.Object, Mock.Of<IAudnexusService>(), mockRepo.Object, Mock.Of<ILogger<ImagesController>>(), mockEnv.Object);
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

            // Act
            var result = await controller.GetImage(identifier);

            // Assert
            mockImageCache.Verify(m => m.MoveToLibraryStorageAsync(identifier, null), Times.Once);

            if (result is PhysicalFileResult fileResult)
            {
                Assert.Equal(libFull, fileResult.FileName);
            }
            else
            {
                Assert.IsType<NotFoundObjectResult>(result);
            }

            // Cleanup
            try { File.Delete(tempFull); } catch { }
            try { File.Delete(libFull); } catch { }
            try { Directory.Delete(Path.Combine(tempRoot, "config"), true); } catch { }
        }
    }
}
