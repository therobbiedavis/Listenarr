using System;
using System.Threading.Tasks;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Api.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Listenarr.Api.Tests
{
    public class QualityProfileScoringTests
    {
        private QualityProfileService CreateService()
        {
            // We don't need a real DbContext for scoring logic in this test - pass null and a null logger.
            // The service only uses DbContext for profile CRUD; ScoreSearchResult doesn't require it.
            return new QualityProfileService(null!, new NullLogger<QualityProfileService>());
        }

        [Fact]
        public async Task PerfectMatch_ShouldBeHighScore()
        {
            var service = CreateService();
            var profile = new QualityProfile
            {
                MinimumSize = 0,
                MaximumSize = 0,
                PreferredFormats = new System.Collections.Generic.List<string> { "mp3" },
                PreferredWords = new System.Collections.Generic.List<string> { "unabridged" },
                MustNotContain = new System.Collections.Generic.List<string>(),
                MustContain = new System.Collections.Generic.List<string>(),
                PreferredLanguages = new System.Collections.Generic.List<string> { "English" },
                MinimumSeeders = 0,
                MaximumAge = 3650
            };

            var result = new SearchResult
            {
                Title = "Author - Great Book (unabridged)",
                Size = 150 * 1024 * 1024,
                Format = "mp3",
                Quality = "320",
                Language = "English",
                DownloadType = "torrent",
                Seeders = 5,
                PublishedDate = DateTime.UtcNow
            };

            var score = await service.ScoreSearchResult(result, profile);
            Assert.True(score.TotalScore >= 80, $"Expected high score, got {score.TotalScore}");
        }

        [Fact]
        public async Task MissingQuality_ShouldBePenalized()
        {
            var service = CreateService();
            var profile = new QualityProfile
            {
                PreferredFormats = new System.Collections.Generic.List<string> { "m4b" },
                PreferredWords = new System.Collections.Generic.List<string>(),
                MustNotContain = new System.Collections.Generic.List<string>(),
                MustContain = new System.Collections.Generic.List<string>(),
                PreferredLanguages = new System.Collections.Generic.List<string> { "English" },
                MinimumSeeders = 0,
                MaximumAge = 3650
            };

            var result = new SearchResult
            {
                Title = "Some Book",
                Size = 120 * 1024 * 1024,
                Format = "m4b",
                Quality = null,
                Language = "English",
                DownloadType = "torrent",
                Seeders = 2,
                PublishedDate = DateTime.UtcNow
            };

            var score = await service.ScoreSearchResult(result, profile);
            Assert.True(score.TotalScore < 100, "Expected score less than 100 when quality missing");
            Assert.Contains("QualityMissing", score.ScoreBreakdown.Keys);
        }

        [Fact]
        public async Task LanguageMismatch_ShouldBePenalized()
        {
            var service = CreateService();
            var profile = new QualityProfile
            {
                PreferredFormats = new System.Collections.Generic.List<string> { "mp3" },
                PreferredWords = new System.Collections.Generic.List<string>(),
                MustNotContain = new System.Collections.Generic.List<string>(),
                MustContain = new System.Collections.Generic.List<string>(),
                PreferredLanguages = new System.Collections.Generic.List<string> { "English" },
                MinimumSeeders = 0,
                MaximumAge = 3650
            };

            var result = new SearchResult
            {
                Title = "Foreign Book",
                Size = 120 * 1024 * 1024,
                Format = "mp3",
                Quality = "256",
                Language = "Spanish",
                DownloadType = "torrent",
                Seeders = 2,
                PublishedDate = DateTime.UtcNow
            };

            var score = await service.ScoreSearchResult(result, profile);
            Assert.True(score.TotalScore <= 85, $"Expected penalty for language mismatch, got {score.TotalScore}");
            Assert.Contains("LanguageMismatch", score.ScoreBreakdown.Keys);
        }

        [Fact]
        public async Task ForbiddenWord_ShouldReject()
        {
            var service = CreateService();
            var profile = new QualityProfile
            {
                MustNotContain = new System.Collections.Generic.List<string> { "abridged" },
                PreferredFormats = new System.Collections.Generic.List<string>(),
                PreferredWords = new System.Collections.Generic.List<string>(),
                MustContain = new System.Collections.Generic.List<string>(),
                PreferredLanguages = new System.Collections.Generic.List<string>(),
                MinimumSeeders = 0,
                MaximumAge = 3650
            };

            var result = new SearchResult
            {
                Title = "Abridged Sample Book",
                Size = 50 * 1024 * 1024,
                Format = "mp3",
                Quality = "128",
                Language = "English",
                DownloadType = "torrent",
                Seeders = 2,
                PublishedDate = DateTime.UtcNow
            };

            var score = await service.ScoreSearchResult(result, profile);
            Assert.True(score.RejectionReasons.Count > 0, "Expected rejection reasons for forbidden word");
            Assert.True(score.TotalScore < 0, "Expected negative score for rejected result");
        }
    }
}
