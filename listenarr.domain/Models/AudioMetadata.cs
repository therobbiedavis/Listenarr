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

namespace Listenarr.Domain.Models
{
    public class AudioMetadata
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumArtist { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public int? Year { get; set; }
        public int? TrackNumber { get; set; }
        public int? TrackTotal { get; set; }
        public int? DiscNumber { get; set; }
        public int? DiscTotal { get; set; }
        public TimeSpan Duration { get; set; }
    public string? Format { get; set; }
    // Container (human-friendly): e.g., M4B, MP3, FLAC
    public string? Container { get; set; }
    // Audio codec: e.g., aac, mp3, opus
    public string? Codec { get; set; }
    public int? Bitrate { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
        public string? Isbn { get; set; }
        public string? Asin { get; set; }
        public string? Description { get; set; }
        public string? Narrator { get; set; }
        public string? Publisher { get; set; }
        public DateTime? PublishDate { get; set; }
        public string? Language { get; set; }
        public string? Series { get; set; }
        public decimal? SeriesPosition { get; set; }
        public byte[]? CoverArt { get; set; }
        public string? CoverArtUrl { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}
