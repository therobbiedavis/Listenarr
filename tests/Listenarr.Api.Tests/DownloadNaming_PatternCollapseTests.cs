using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Moq;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class DownloadNaming_PatternCollapseTests
    {
        [Fact]
        public void ApplyNamingPattern_CollapsesAdjacentDuplicateComponents()
        {
            // Test the FileNamingService.ApplyNamingPattern method directly
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<FileNamingService>>();
            var configMock = new Mock<IConfigurationService>();
            var fileNamingService = new FileNamingService(configMock.Object, loggerMock.Object);

            // Pattern that produces duplicate components
            var pattern = "{Author}/{Series}/{Title} ({Year})";
            var variables = new Dictionary<string, object>
            {
                { "Author", "Jane Austen" },
                { "Series", null },
                { "Title", "Pride and Prejudice" },
                { "Year", "2015" }
            };

            var result = fileNamingService.ApplyNamingPattern(pattern, variables);

            // Should collapse adjacent duplicates: "Jane Austen/Pride and Prejudice/Pride and Prejudice (2015)" -> "Jane Austen/Pride and Prejudice (2015)"
            var expected = Path.Combine("Jane Austen", "Pride and Prejudice (2015)");
            Assert.Equal(expected, result);
            Assert.DoesNotContain("Pride and Prejudice" + Path.DirectorySeparatorChar + "Pride and Prejudice", result);
        }
    }
}