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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Models
{
    /// <summary>
    /// Quality profile for automatic download selection
    /// </summary>
    public class QualityProfile
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// Ordered list of quality definitions with cutoff point
        /// </summary>
        public List<QualityDefinition> Qualities { get; set; } = new();

        /// <summary>
        /// The quality level to stop upgrading at (cutoff)
        /// </summary>
        public string? CutoffQuality { get; set; }

        /// <summary>
        /// Minimum file size in MB (0 = no minimum)
        /// </summary>
        public int MinimumSize { get; set; } = 0;

        /// <summary>
        /// Maximum file size in MB (0 = no maximum)
        /// </summary>
        public int MaximumSize { get; set; } = 0;

        /// <summary>
        /// Preferred file formats in order of preference
        /// </summary>
        public List<string> PreferredFormats { get; set; } = new() { "m4b", "mp3", "m4a", "flac", "opus" };

        /// <summary>
        /// Words/phrases that increase score (e.g., "unabridged", "retail")
        /// </summary>
        public List<string> PreferredWords { get; set; } = new();

        /// <summary>
        /// Words/phrases that must NOT be in the title (e.g., "abridged", "sample")
        /// </summary>
        public List<string> MustNotContain { get; set; } = new();

        /// <summary>
        /// Words/phrases that must be in the title
        /// </summary>
        public List<string> MustContain { get; set; } = new();

        /// <summary>
        /// Preferred languages in order of preference (e.g., "English", "Spanish")
        /// </summary>
        public List<string> PreferredLanguages { get; set; } = new() { "English" };

        /// <summary>
        /// Minimum number of seeders for torrents (0 = no minimum)
        /// </summary>
        public int MinimumSeeders { get; set; } = 1;

        /// <summary>
        /// Whether this is the default profile for new audiobooks
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Whether to prefer newer releases (higher age score)
        /// </summary>
        public bool PreferNewerReleases { get; set; } = true;

        /// <summary>
        /// Maximum age in days for releases (0 = no limit)
        /// </summary>
        public int MaximumAge { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Quality definition with priority
    /// </summary>
    public class QualityDefinition
    {
        /// <summary>
        /// Quality identifier (e.g., "320kbps", "192kbps", "64kbps", "lossless", "unknown")
        /// </summary>
        [Required]
        public string Quality { get; set; } = string.Empty;

        /// <summary>
        /// Whether this quality is allowed for downloads
        /// </summary>
        public bool Allowed { get; set; } = true;

        /// <summary>
        /// Priority order (lower number = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Scoring result for a search result
    /// </summary>
    public class QualityScore
    {
        public SearchResult SearchResult { get; set; } = new();
        public int TotalScore { get; set; }
        public Dictionary<string, int> ScoreBreakdown { get; set; } = new();
        public List<string> RejectionReasons { get; set; } = new();
        public bool IsRejected => RejectionReasons.Count > 0;
    }
}
