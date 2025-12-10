using System;
using System.IO;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class FinalizePathHelperTests
    {
        [Fact]
        public void BuildMultiFileDestination_WithAuthorInTitle_SplitsAuthorAndTitle()
        {
            var settings = new ApplicationSettings { OutputPath = Path.Combine("C:", "Library") };
            var download = new Download { Title = "William Faulkner - The Sound and the Fury", Artist = null, Series = null };

            var dest = FinalizePathHelper.BuildMultiFileDestination(settings, download, "William Faulkner - The Sound and the Fury");

            Assert.Contains("William Faulkner", dest);
            Assert.Contains("The Sound and the Fury", dest);
            Assert.StartsWith(Path.Combine("C:", "Library"), dest, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BuildMultiFileDestination_WithSeries_IncludesSeriesFolder()
        {
            var settings = new ApplicationSettings { OutputPath = Path.Combine("C:", "Library") };
            var download = new Download { Title = "The Sound and the Fury", Artist = "William Faulkner", Series = "Modern Classics" };

            var dest = FinalizePathHelper.BuildMultiFileDestination(settings, download, "The Sound and the Fury");

            // Expect: C:\Library\William Faulkner\Modern Classics\The Sound and the Fury
            Assert.StartsWith(Path.Combine("C:", "Library"), dest, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine("William Faulkner"), dest);
            Assert.Contains(Path.Combine("Modern Classics"), dest);
            Assert.Contains(Path.Combine("The Sound and the Fury"), dest);
        }
    }
}

