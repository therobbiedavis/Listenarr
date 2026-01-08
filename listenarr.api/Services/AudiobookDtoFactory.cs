using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Listenarr.Api.Models;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services
{
    public static class AudiobookDtoFactory
    {
        public static AudiobookDto BuildFromEntity(ListenArrDbContext db, Listenarr.Domain.Models.Audiobook audiobook)
        {
            if (audiobook == null) return null!;

            var files = audiobook.Files?.Select(f => new AudiobookFileDto
            {
                Id = f.Id,
                Path = f.Path,
                Size = f.Size,
                DurationSeconds = f.DurationSeconds,
                Format = f.Format,
                Container = f.Container,
                Codec = f.Codec,
                Bitrate = f.Bitrate,
                SampleRate = f.SampleRate,
                Channels = f.Channels,
                CreatedAt = f.CreatedAt,
                Source = f.Source
            }).ToArray();

            var dto = new AudiobookDto
            {
                Id = audiobook.Id,
                Title = audiobook.Title,
                Subtitle = audiobook.Subtitle,
                Authors = audiobook.Authors?.ToArray(),
                Narrators = audiobook.Narrators?.ToArray(),
                Asin = audiobook.Asin,
                Isbn = audiobook.Isbn,
                Language = audiobook.Language,
                Genres = audiobook.Genres?.ToArray(),
                Tags = audiobook.Tags?.ToArray(),
                Description = audiobook.Description,
                PublishYear = audiobook.PublishYear,
                Series = audiobook.Series,
                SeriesNumber = audiobook.SeriesNumber,
                Monitored = audiobook.Monitored,
                FilePath = audiobook.FilePath,
                FileSize = audiobook.FileSize,
                BasePath = audiobook.BasePath,
                Files = files,
                ImageUrl = audiobook.ImageUrl,
                Quality = audiobook.Quality,
                QualityProfileId = audiobook.QualityProfileId,
                Version = audiobook.Version,
                Abridged = audiobook.Abridged,
                Explicit = audiobook.Explicit
            };

            // Compute wanted flag (treat presence of file records as authoritative for "not wanted")
            dto.Wanted = audiobook.Monitored && (dto.Files == null || !dto.Files.Any() || !dto.Files.Any(f => !string.IsNullOrEmpty(f.Path)));


            return dto;
        }
    }
}