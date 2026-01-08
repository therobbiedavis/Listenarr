using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    public class LegacyOutputPathMigratorTests
    {
        [Fact]
        public async Task Migrate_CreatesRoot_WhenNoExistingAndOutputPathPresent()
        {
            var mockConfig = new Mock<IConfigurationService>();
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new Listenarr.Domain.Models.ApplicationSettings { OutputPath = "C:\\books" });

            var mockRootService = new Mock<IRootFolderService>();
            mockRootService.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RootFolder>());
            mockRootService.Setup(r => r.CreateAsync(It.IsAny<RootFolder>())).ReturnsAsync((RootFolder r) => r);

            var migrator = new LegacyOutputPathMigrator(mockConfig.Object, mockRootService.Object, new NullLogger<LegacyOutputPathMigrator>());

            await migrator.MigrateAsync();

            mockRootService.Verify(r => r.CreateAsync(It.Is<RootFolder>(rf => rf.Name == "Default" && rf.Path == "C:\\books" && rf.IsDefault)), Times.Once);
        }

        [Fact]
        public async Task Migrate_DoesNotCreate_WhenRootsExist()
        {
            var mockConfig = new Mock<IConfigurationService>();
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new Listenarr.Domain.Models.ApplicationSettings { OutputPath = "C:\\books" });

            var mockRootService = new Mock<IRootFolderService>();
            mockRootService.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RootFolder> { new RootFolder { Name = "X", Path = "C:\\other" } });

            var migrator = new LegacyOutputPathMigrator(mockConfig.Object, mockRootService.Object, new NullLogger<LegacyOutputPathMigrator>());

            await migrator.MigrateAsync();

            mockRootService.Verify(r => r.CreateAsync(It.IsAny<RootFolder>()), Times.Never);
        }

        [Fact]
        public async Task Migrate_DoesNotCreate_WhenOutputPathEmpty()
        {
            var mockConfig = new Mock<IConfigurationService>();
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new Listenarr.Domain.Models.ApplicationSettings { OutputPath = "" });

            var mockRootService = new Mock<IRootFolderService>();
            mockRootService.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RootFolder>());

            var migrator = new LegacyOutputPathMigrator(mockConfig.Object, mockRootService.Object, new NullLogger<LegacyOutputPathMigrator>());

            await migrator.MigrateAsync();

            mockRootService.Verify(r => r.CreateAsync(It.IsAny<RootFolder>()), Times.Never);
        }
    }
}