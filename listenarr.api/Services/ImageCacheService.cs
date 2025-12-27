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
        Task<string?> MoveToLibraryStorageAsync(string identifier, string? imageUrl = null);
        Task<string?> MoveToAuthorLibraryStorageAsync(string identifier, string? imageUrl = null);
        Task<string?> GetCachedImagePathAsync(string identifier);
        Task ClearTempCacheAsync();
    }

    public class ImageCacheService : IImageCacheService
    {
        private readonly ILogger<ImageCacheService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _tempCachePath;
        private readonly string _libraryImagePath;
        private readonly string _authorImagePath;
    private readonly string _contentRootPath;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.SemaphoreSlim> _downloadLocks = new();
    public ImageCacheService(ILogger<ImageCacheService> logger, IHttpClientFactory httpClientFactory, string contentRootPath)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _contentRootPath = contentRootPath;

        // Set up cache directories relative to content root
        var baseDir = Path.Combine(contentRootPath, "config");
        _tempCachePath = Path.Combine(baseDir, "cache", "images", "temp");
        _libraryImagePath = Path.Combine(baseDir, "cache", "images", "library");
        _authorImagePath = Path.Combine(baseDir, "cache", "images", "authors");

        // Ensure directories exist
        Directory.CreateDirectory(_tempCachePath);
        Directory.CreateDirectory(_libraryImagePath);
        Directory.CreateDirectory(_authorImagePath);
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
                // Check library storage first
                var libraryPath = GetImagePath(identifier, _libraryImagePath);
                if (File.Exists(libraryPath))
                {
                    _logger.LogInformation("Image already in library storage: {Identifier}", identifier);
                    return GetRelativePath(libraryPath);
                }

                // Also check authors storage (author images may be stored separately)
                var authorPath = GetImagePath(identifier, _authorImagePath);
                if (File.Exists(authorPath))
                {
                    _logger.LogInformation("Image already in author storage: {Identifier}", identifier);
                    return GetRelativePath(authorPath);
                }

                // Check temp cache for a valid (non-placeholder) image
                var tempExisting = GetBestTempImagePathIfValid(identifier);
                if (!string.IsNullOrEmpty(tempExisting))
                {
                    _logger.LogInformation("Image already cached: {Identifier}", identifier);
                    return GetRelativePath(tempExisting);
                }

                _logger.LogInformation("Downloading image from {Url} for {Identifier}", imageUrl, identifier);

                // Skip known Amazon placeholder URL to avoid caching tiny grey-pixel images
                if (imageUrl.Contains("grey-pixel.gif", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping known grey-pixel placeholder URL for {Identifier}", identifier);
                    return null;
                }

                // Use per-identifier lock to prevent concurrent downloads for same identifier
                var sem = _downloadLocks.GetOrAdd(identifier, _ => new System.Threading.SemaphoreSlim(1, 1));
                await sem.WaitAsync();
                try
                {
                    // Re-check after acquiring lock
                    libraryPath = GetImagePath(identifier, _libraryImagePath);
                    if (File.Exists(libraryPath))
                    {
                        _logger.LogInformation("Image already in library storage (after wait): {Identifier}", identifier);
                        return GetRelativePath(libraryPath);
                    }

                    // Also check author storage after lock
                    authorPath = GetImagePath(identifier, _authorImagePath);
                    if (File.Exists(authorPath))
                    {
                        _logger.LogInformation("Image already in author storage (after wait): {Identifier}", identifier);
                        return GetRelativePath(authorPath);
                    }

                    tempExisting = GetBestTempImagePathIfValid(identifier);
                    if (!string.IsNullOrEmpty(tempExisting))
                    {
                        _logger.LogInformation("Image already cached (after wait): {Identifier}", identifier);
                        return GetRelativePath(tempExisting);
                    }

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
                finally
                {
                    sem.Release();
                }
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
        public async Task<string?> MoveToLibraryStorageAsync(string identifier, string? imageUrl = null)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                _logger.LogWarning("Cannot move image: identifier is empty");
                return null;
            }

            try
            {
                // Check if already in library storage
                var libraryPath = GetImagePath(identifier, _libraryImagePath);
                if (File.Exists(libraryPath))
                {
                    _logger.LogInformation("Image already in library storage: {Identifier}", identifier);
                    return GetRelativePath(libraryPath);
                }

                // Find the temp cached file
                var tempPath = GetImagePath(identifier, _tempCachePath);
                if (!File.Exists(tempPath))
                {
                    _logger.LogWarning("Temp cached image not found for {Identifier}", identifier);
                    // If imageUrl provided, attempt to download to temp cache using the identifier
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                    {
                        _logger.LogInformation("Attempting to download image for {Identifier} from provided URL", identifier);
                        var cached = await DownloadAndCacheImageAsync(imageUrl, identifier);
                        if (string.IsNullOrWhiteSpace(cached))
                        {
                            _logger.LogWarning("Download to temp cache failed for {Identifier}", identifier);
                            return null;
                        }

                        // Recompute tempPath after download
                        tempPath = GetImagePath(identifier, _tempCachePath);
                        if (!File.Exists(tempPath))
                        {
                            _logger.LogWarning("Downloaded file not found in temp cache for {Identifier}", identifier);
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                // Move to library storage
                Directory.CreateDirectory(_libraryImagePath);
                File.Move(tempPath, libraryPath, overwrite: true);

                _logger.LogInformation("Image moved to library storage: {Identifier}", identifier);
                return GetRelativePath(libraryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move image to library storage for {Identifier}", identifier);
                return null;
            }
        }

        /// <summary>
        /// Moves an image from temp cache to permanent authors storage
        /// </summary>
        public async Task<string?> MoveToAuthorLibraryStorageAsync(string identifier, string? imageUrl = null)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                _logger.LogWarning("Cannot move author image: identifier is empty");
                return null;
            }

            try
            {
                // Check if already in author storage
                var authorPath = GetImagePath(identifier, _authorImagePath);
                if (File.Exists(authorPath))
                {
                    _logger.LogInformation("Author image already in author storage: {Identifier}", identifier);
                    return GetRelativePath(authorPath);
                }

                // Find the temp cached file
                var tempPath = GetImagePath(identifier, _tempCachePath);
                if (!File.Exists(tempPath))
                {
                    _logger.LogWarning("Temp cached author image not found for {Identifier}", identifier);
                    // If imageUrl provided, attempt to download to temp cache using the identifier
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                    {
                        _logger.LogInformation("Attempting to download author image for {Identifier} from provided URL", identifier);
                        var cached = await DownloadAndCacheImageAsync(imageUrl, identifier);
                        if (string.IsNullOrWhiteSpace(cached))
                        {
                            _logger.LogWarning("Download to temp cache failed for {Identifier}", identifier);
                            return null;
                        }

                        // Recompute tempPath after download
                        tempPath = GetImagePath(identifier, _tempCachePath);
                        if (!File.Exists(tempPath))
                        {
                            _logger.LogWarning("Downloaded file not found in temp cache for {Identifier}", identifier);
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                // Move to author storage
                Directory.CreateDirectory(_authorImagePath);
                File.Move(tempPath, authorPath, overwrite: true);

                _logger.LogInformation("Author image moved to author storage: {Identifier}", identifier);
                return GetRelativePath(authorPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move author image to author storage for {Identifier}", identifier);
                return null;
            }
        }

        /// <summary>
        /// Gets the cached image path if it exists
        /// </summary>
        public Task<string?> GetCachedImagePathAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return Task.FromResult<string?>(null);

            // Special-case for built-in unavailable cover asset
            if (string.Equals(identifier, "cover-unavailable", StringComparison.OrdinalIgnoreCase))
            {
                var staticPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cover-unavailable.svg");
                if (File.Exists(staticPath))
                    return Task.FromResult<string?>(GetRelativePath(staticPath));
            }


            // Check library storage first
            var libraryPath = GetImagePath(identifier, _libraryImagePath);
            if (File.Exists(libraryPath))
                return Task.FromResult<string?>(GetRelativePath(libraryPath));

            // Check authors storage next
            var authorPath = GetImagePath(identifier, _authorImagePath);
            if (File.Exists(authorPath))
                return Task.FromResult<string?>(GetRelativePath(authorPath));

            // Check temp cache and prefer non-placeholder images
            var tempBest = GetBestTempImagePathIfValid(identifier);
            if (!string.IsNullOrEmpty(tempBest))
                return Task.FromResult<string?>(GetRelativePath(tempBest));

            return Task.FromResult<string?>(null);
        }

        private string? GetBestTempImagePathIfValid(string identifier)
        {
            var sanitized = SanitizeFileName(identifier);
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg" };

            foreach (var ext in extensions)
            {
                var path = Path.Combine(_tempCachePath, sanitized + ext);
                if (!File.Exists(path)) continue;

                // If it's a GIF that is very small, treat it as a placeholder and ignore it
                if (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var fi = new FileInfo(path);
                        if (fi.Length < 2048)
                        {
                            _logger.LogInformation("Ignoring small GIF placeholder in cache for {Identifier}: {Path} ({Length} bytes)", identifier, path, fi.Length);
                            continue;
                        }
                    }
                    catch
                    {
                        // If anything goes wrong inspecting the file, fall back to using it
                    }
                }

                return path;
            }

            return null;
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
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg" };

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
            var relativePath = Path.GetRelativePath(_contentRootPath, fullPath).Replace("\\", "/");
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
                if (contentType.Contains("svg+xml")) return ".svg";
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
