/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Text.Json.Serialization;

namespace Listenarr.Domain.Models
{
    /// <summary>
    /// Base class for all search results with common properties
    /// </summary>
    public abstract class BaseSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        // Backwards-compatibility: some tests and callers expect `Author` property name.
        public string Author { get => Artist; set => Artist = value; }
        public string Album { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? SourceLink { get; set; } // Direct link to the source
        public string PublishedDate { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public int Score { get; set; }
    }

    /// <summary>
    /// Search result from torrent/NZB indexers
    /// </summary>
    public class IndexerSearchResult : BaseSearchResult
    {
        public long Size { get; set; }
        public int Seeders { get; set; }
        public int Leechers { get; set; }
        public string MagnetLink { get; set; } = string.Empty;
        public string TorrentUrl { get; set; } = string.Empty;
        public string NzbUrl { get; set; } = string.Empty;
        [JsonIgnore]
        public byte[]? TorrentFileContent { get; set; }
        [JsonIgnore]
        public string? TorrentFileName { get; set; }
        public string DownloadType { get; set; } = string.Empty; // "Torrent", "Usenet", or "DDL"
        public string Quality { get; set; } = string.Empty;

        // Indexer metadata used to resolve tracker-specific downloads
        public int? IndexerId { get; set; }
        public string? IndexerImplementation { get; set; }

        // Link to the indexer page for this result
        public string? ResultUrl { get; set; }

        // Lightweight metadata occasionally parsed from indexer responses
        public string? Description { get; set; }
        public string? Language { get; set; }
        public string? Publisher { get; set; }
        public string? Narrator { get; set; }
    }

    /// <summary>
    /// Search result from audiobook metadata sources (Audimeta, Audnexus, etc.)
    /// </summary>
    public class MetadataSearchResult : BaseSearchResult
    {
        // Additional properties for enhanced audiobook metadata
        public string? Description { get; set; }
        public string? Publisher { get; set; }
        // Subtitle provided by metadata sources (e.g., Audible/Audimeta)
        public string? Subtitle { get; set; }
        // Publish year as provided by metadata (convenience for UI)
        public string? PublishYear { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; }
        public string? Narrator { get; set; }
        public string? ImageUrl { get; set; }
        public string? Asin { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        public string? ProductUrl { get; set; } // Direct link to Amazon/Audible product page
        // Indicates this result had a successful full metadata enrichment pass
        public bool IsEnriched { get; set; }
        // Tracks which metadata API was used to enrich this result
        public string? MetadataSource { get; set; }
        public string? Subtitles { get; set; }
    }

    /// <summary>
    /// Legacy SearchResult class - kept for backwards compatibility
    /// Combines both indexer and metadata properties
    /// </summary>
    public class SearchResult : BaseSearchResult
    {
        // Indexer-specific properties
        public long Size { get; set; }
        public int Seeders { get; set; }
        public int Leechers { get; set; }
        public string MagnetLink { get; set; } = string.Empty;
        public string TorrentUrl { get; set; } = string.Empty;
        public string NzbUrl { get; set; } = string.Empty;
        [JsonIgnore]
        public byte[]? TorrentFileContent { get; set; }
        [JsonIgnore]
        public string? TorrentFileName { get; set; }
        public string DownloadType { get; set; } = string.Empty; // "Torrent", "Usenet", or "DDL"
        public string Quality { get; set; } = string.Empty;

        // Indexer metadata used to resolve tracker-specific downloads
        public int? IndexerId { get; set; }
        public string? IndexerImplementation { get; set; }

        // Link to the indexer page for this result
        public string? ResultUrl { get; set; }

        // Metadata-specific properties
        public string? Description { get; set; }
        public string? Publisher { get; set; }
        // Subtitle provided by metadata sources (e.g., Audible/Audimeta)
        public string? Subtitle { get; set; }
        // Publish year as provided by metadata (convenience for UI)
        public string? PublishYear { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; }
        public string? Narrator { get; set; }
        public string? ImageUrl { get; set; }
        public string? Asin { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        public string? ProductUrl { get; set; } // Direct link to Amazon/Audible product page
        // Indicates this result had a successful full metadata enrichment pass (Audible product scrape)
        public bool IsEnriched { get; set; }
        // Tracks which metadata API was used to enrich this result (e.g., "Audimeta", "Audnexus", "Audible (Scraped)")
        public string? MetadataSource { get; set; }
        public string? Subtitles { get; set; }
    }

    public class SearchAndDownloadResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? DownloadId { get; set; }
        public string? IndexerUsed { get; set; }
        public string? DownloadClientUsed { get; set; }
        public SearchResult? SearchResult { get; set; }
    }

    /// <summary>
    /// <summary>
    /// Response wrapper for search operations that can contain different types of results
    /// </summary>
    public class SearchResponse
    {
        public List<IndexerSearchResult> IndexerResults { get; set; } = new();
        public List<MetadataSearchResult> MetadataResults { get; set; } = new();
        public int TotalCount => IndexerResults.Count + MetadataResults.Count;
    }

    /// <summary>
    /// Conversion methods for search result types
    /// </summary>
    public static class SearchResultConverters
    {
        public static MetadataSearchResult ToMetadata(SearchResult result)
        {
            return new MetadataSearchResult
            {
                Id = result.Id,
                Title = result.Title,
                Artist = result.Artist,
                Album = result.Album,
                Category = result.Category,
                Source = result.Source,
                SourceLink = result.SourceLink,
                PublishedDate = result.PublishedDate,
                Format = result.Format,
                Score = result.Score,
                Description = result.Description,
                Subtitle = result.Subtitle,
                Publisher = result.Publisher,
                Language = result.Language,
                Runtime = result.Runtime,
                Narrator = result.Narrator,
                ImageUrl = result.ImageUrl,
                Asin = result.Asin,
                Series = result.Series,
                SeriesNumber = result.SeriesNumber,
                ProductUrl = result.ProductUrl,
                IsEnriched = result.IsEnriched,
                MetadataSource = result.MetadataSource
            };
        }

        public static SearchResult ToSearchResult(IndexerSearchResult result)
        {
            return new SearchResult
            {
                Id = result.Id,
                Title = result.Title,
                Artist = result.Artist,
                Album = result.Album,
                Category = result.Category,
                Source = result.Source,
                SourceLink = result.SourceLink,
                PublishedDate = result.PublishedDate,
                Format = result.Format,
                Score = result.Score,
                Size = result.Size,
                Seeders = result.Seeders,
                Leechers = result.Leechers,
                MagnetLink = result.MagnetLink,
                TorrentUrl = result.TorrentUrl,
                NzbUrl = result.NzbUrl,
                DownloadType = result.DownloadType,
                Quality = result.Quality,
                ResultUrl = result.ResultUrl
            };
        }

        public static IndexerSearchResult ToIndexerSearchResult(SearchResult result)
        {
            return new IndexerSearchResult
            {
                Id = result.Id,
                Title = result.Title,
                Artist = result.Artist,
                Album = result.Album,
                Category = result.Category,
                Source = result.Source,
                SourceLink = result.SourceLink,
                PublishedDate = result.PublishedDate,
                Format = result.Format,
                Score = result.Score,
                Size = result.Size,
                Seeders = result.Seeders,
                Leechers = result.Leechers,
                MagnetLink = result.MagnetLink,
                TorrentUrl = result.TorrentUrl,
                NzbUrl = result.NzbUrl,
                DownloadType = result.DownloadType,
                Quality = result.Quality,
                ResultUrl = result.ResultUrl
            };
        }

        public static SearchResult ToSearchResult(MetadataSearchResult result)
        {
            return new SearchResult
            {
                Id = result.Id,
                Title = result.Title,
                Artist = result.Artist,
                Album = result.Album,
                Category = result.Category,
                Source = result.Source,
                SourceLink = result.SourceLink,
                PublishedDate = result.PublishedDate,
                Format = result.Format,
                Score = result.Score,
                // Metadata properties
                Description = result.Description,
                Subtitle = result.Subtitle,
                Publisher = result.Publisher,
                Language = result.Language,
                Runtime = result.Runtime,
                Narrator = result.Narrator,
                ImageUrl = result.ImageUrl,
                Asin = result.Asin,
                Series = result.Series,
                SeriesNumber = result.SeriesNumber,
                ProductUrl = result.ProductUrl,
                IsEnriched = result.IsEnriched,
                MetadataSource = result.MetadataSource
            };
        }

        public static List<MetadataSearchResult> ToMetadataList(IEnumerable<SearchResult> results)
        {
            return results.Select(ToMetadata).ToList();
        }
    }
}
