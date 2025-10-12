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
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public interface IImageCacheService
    {
        Task<string?> DownloadAndCacheImageAsync(string imageUrl, string identifier);
        Task<string?> MoveToLibraryStorageAsync(string identifier);
        Task<string?> GetCachedImagePathAsync(string identifier);
        Task ClearTempCacheAsync();
    }

    public class ImageCacheService : IImageCacheService
    {
        private readonly ILogger<ImageCacheService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _tempCachePath;
        private readonly string _libraryImagePath;

        public ImageCacheService(ILogger<ImageCacheService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            
            // Set up cache directories
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "config");
            _tempCachePath = Path.Combine(baseDir, "cache", "images", "temp");
            _libraryImagePath = Path.Combine(baseDir, "cache", "images", "library");
            
            // Ensure directories exist
            Directory.CreateDirectory(_tempCachePath);
            Directory.CreateDirectory(_libraryImagePath);
        }

        /// <summary>
        /// Downloads an image from a URL and caches it temporarily
        /// </summary>
        public async Task<string?> DownloadAndCacheImageAsync(string imageUrl, string identifier)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(identifier))
            {
                _logger.LogWarning("Cannot cache image: URL or identifier is empty");
                return null;
            }

            try
            {
                // Check if already cached
                var existingPath = GetImagePath(identifier, _tempCachePath);
                if (File.Exists(existingPath))
                {
                    _logger.LogInformation("Image already cached: {Identifier}", identifier);
                    return GetRelativePath(existingPath);
                }

                // Check library storage
                var libraryPath = GetImagePath(identifier, _libraryImagePath);
                if (File.Exists(libraryPath))
                {
                    _logger.LogInformation("Image already in library storage: {Identifier}", identifier);
                    return GetRelativePath(libraryPath);
                }

                _logger.LogInformation("Downloading image from {Url} for {Identifier}", imageUrl, identifier);
                
                // Download image
                var response = await _httpClient.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();

                // Determine file extension from content type or URL
                var extension = GetImageExtension(imageUrl, response.Content.Headers.ContentType?.MediaType);
                var fileName = $"{SanitizeFileName(identifier)}{extension}";
                var filePath = Path.Combine(_tempCachePath, fileName);

                // Save to temp cache
                await using var fileStream = File.Create(filePath);
                await response.Content.CopyToAsync(fileStream);

                _logger.LogInformation("Image cached successfully: {FilePath}", filePath);
                return GetRelativePath(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download and cache image from {Url}", imageUrl);
                return null;
            }
        }

        /// <summary>
        /// Moves an image from temp cache to permanent library storage
        /// </summary>
        public Task<string?> MoveToLibraryStorageAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                _logger.LogWarning("Cannot move image: identifier is empty");
                return Task.FromResult<string?>(null);
            }

            try
            {
                // Check if already in library storage
                var libraryPath = GetImagePath(identifier, _libraryImagePath);
                if (File.Exists(libraryPath))
                {
                    _logger.LogInformation("Image already in library storage: {Identifier}", identifier);
                    return Task.FromResult<string?>(GetRelativePath(libraryPath));
                }

                // Find the temp cached file
                var tempPath = GetImagePath(identifier, _tempCachePath);
                if (!File.Exists(tempPath))
                {
                    _logger.LogWarning("Temp cached image not found for {Identifier}", identifier);
                    return Task.FromResult<string?>(null);
                }

                // Move to library storage
                Directory.CreateDirectory(_libraryImagePath);
                File.Move(tempPath, libraryPath, overwrite: true);
                
                _logger.LogInformation("Image moved to library storage: {Identifier}", identifier);
                return Task.FromResult<string?>(GetRelativePath(libraryPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move image to library storage for {Identifier}", identifier);
                return Task.FromResult<string?>(null);
            }
        }

        /// <summary>
        /// Gets the cached image path if it exists
        /// </summary>
        public Task<string?> GetCachedImagePathAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return Task.FromResult<string?>(null);

            // Check library storage first
            var libraryPath = GetImagePath(identifier, _libraryImagePath);
            if (File.Exists(libraryPath))
                return Task.FromResult<string?>(GetRelativePath(libraryPath));

            // Check temp cache
            var tempPath = GetImagePath(identifier, _tempCachePath);
            if (File.Exists(tempPath))
                return Task.FromResult<string?>(GetRelativePath(tempPath));

            return Task.FromResult<string?>(null);
        }

        /// <summary>
        /// Clears all temporary cached images
        /// </summary>
        public Task ClearTempCacheAsync()
        {
            try
            {
                _logger.LogInformation("Clearing temp image cache");
                
                if (Directory.Exists(_tempCachePath))
                {
                    var files = Directory.GetFiles(_tempCachePath);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete cached file: {File}", file);
                        }
                    }
                    _logger.LogInformation("Temp cache cleared: {Count} files deleted", files.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear temp cache");
            }

            return Task.CompletedTask;
        }

        private string GetImagePath(string identifier, string basePath)
        {
            // Try to find existing file with any extension
            var sanitized = SanitizeFileName(identifier);
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            
            foreach (var ext in extensions)
            {
                var path = Path.Combine(basePath, sanitized + ext);
                if (File.Exists(path))
                    return path;
            }

            // Default to .jpg if not found
            return Path.Combine(basePath, sanitized + ".jpg");
        }

        private string GetRelativePath(string fullPath)
        {
            var baseDir = Directory.GetCurrentDirectory();
            var relativePath = fullPath.Replace(baseDir, "").Replace("\\", "/").TrimStart('/');
            return relativePath;
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        private string GetImageExtension(string url, string? contentType)
        {
            // Try to get extension from content type
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("jpeg")) return ".jpg";
                if (contentType.Contains("png")) return ".png";
                if (contentType.Contains("webp")) return ".webp";
                if (contentType.Contains("gif")) return ".gif";
            }

            // Try to get extension from URL
            var urlExtension = Path.GetExtension(url).ToLower();
            if (!string.IsNullOrEmpty(urlExtension) && urlExtension.Length <= 5)
                return urlExtension;

            // Default to .jpg
            return ".jpg";
        }
    }
}
