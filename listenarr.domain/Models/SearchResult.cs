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
    public class SearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
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
        public string Source { get; set; } = string.Empty;
        public string? SourceLink { get; set; } // Direct link to the source (Amazon/Audible product page, indexer page, etc.)
        public string DownloadType { get; set; } = string.Empty; // "Torrent", "Usenet", or "DDL"
        public DateTime PublishedDate { get; set; }
        public string Quality { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;

        // Indexer metadata used to resolve tracker-specific downloads
        public int? IndexerId { get; set; }
        public string? IndexerImplementation { get; set; }

        // Additional properties for enhanced audiobook metadata
        public string? Description { get; set; }
        public string? Publisher { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; }
        public string? Narrator { get; set; }
        public string? ImageUrl { get; set; }
        public string? Asin { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        public string? ProductUrl { get; set; } // Direct link to Amazon/Audible product page
        // Link to the indexer page for this result (e.g., Internet Archive details page, indexer item page)
        public string? ResultUrl { get; set; }
        // Indicates this result had a successful full metadata enrichment pass (Audible product scrape)
        public bool IsEnriched { get; set; }
        public int Score { get; set; }
        // Tracks which metadata API was used to enrich this result (e.g., "Audimeta", "Audnexus", "Audible (Scraped)")
        public string? MetadataSource { get; set; }
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
}
