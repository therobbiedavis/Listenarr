using System;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class SearchServiceScoringTests
    {
        [Fact]
        public void QualityShouldBeatLargeSeederAdvantage()
        {
            var service = new SearchService(null, null, NullLogger<SearchService>.Instance, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

            var flac = new SearchResult
            {
                Title = "Flac Release",
                Quality = "FLAC",
                Seeders = 1,
                Size = 200 * 1024 * 1024,
                PublishedDate = DateTime.UtcNow.ToString("o")
            };

            var mp3HighSeed = new SearchResult
            {
                Title = "MP3 High Seed",
                Quality = "MP3 320kbps",
                Seeders = 5000,
                Size = 200 * 1024 * 1024,
                PublishedDate = DateTime.UtcNow.ToString("o")
            };

            var flacScore = service.CalculateProwlarrStyleScore(flac);
            var mp3Score = service.CalculateProwlarrStyleScore(mp3HighSeed);

            Assert.True(flacScore > mp3Score, $"Expected FLAC ({flacScore}) to score higher than MP3 ({mp3Score})");
        }

        [Fact]
        public void UsenetWithGrabsScoresReasonably()
        {
            var service = new SearchService(null, null, NullLogger<SearchService>.Instance, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

            var usenet = new SearchResult
            {
                Title = "Usenet release",
                NzbUrl = "http://example.nzb/file.nzb",
                Grabs = 50,
                Quality = null,
                Size = 800 * 1024 * 1024,
                PublishedDate = DateTime.UtcNow.AddDays(-2).ToString("o")
            };

            var torrentLowSeed = new SearchResult
            {
                Title = "Torrent low seed",
                Seeders = 1,
                Quality = null, // missing quality
                Size = 20 * 1024 * 1024, // small suspicious size
                PublishedDate = DateTime.UtcNow.AddYears(-2).ToString("o") // old
            };

            var usenetScore = service.CalculateProwlarrStyleScore(usenet);
            var torrentScore = service.CalculateProwlarrStyleScore(torrentLowSeed);

            Assert.True(usenetScore > torrentScore, $"Expected Usenet ({usenetScore}) to score higher than low-seed torrent ({torrentScore})");
        }
    }
}
