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

using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Models
{
    /// <summary>
    /// Represents a path mapping between a download client's path and Listenarr's path.
    /// Used when download clients run in different containers/systems with different mount points.
    /// </summary>
    public class RemotePathMapping
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The download client this mapping applies to
        /// </summary>
        [Required]
        public string DownloadClientId { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly name for this mapping
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The path as seen by the download client (e.g., "/downloads/sonarr/")
        /// </summary>
        [Required]
        public string RemotePath { get; set; } = string.Empty;

        /// <summary>
        /// The path as seen by Listenarr (e.g., "/server/downloads/complete/sonarr/")
        /// </summary>
        [Required]
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// When this mapping was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this mapping was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Normalize path separators for consistency
        /// </summary>
        public void NormalizePaths()
        {
            RemotePath = NormalizePath(RemotePath);
            LocalPath = NormalizePath(LocalPath);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Replace backslashes with forward slashes
            path = path.Replace('\\', '/');

            // Ensure trailing slash
            if (!path.EndsWith('/'))
                path += '/';

            return path;
        }
    }
}
