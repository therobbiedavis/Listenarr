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

using System.Net.Http;
using System.Threading;
using System.Buffers.Binary;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Listenarr.Api.Services.Extractors;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Service for scraping Amazon product pages for audiobook metadata.
    /// Inherits from AudibleMetadataService to reuse HTML fetching and helper methods.
    /// </summary>
    public class AmazonMetadataService : AudibleMetadataService, IAmazonMetadataService
    {
        public AmazonMetadataService(HttpClient httpClient, ILogger<AmazonMetadataService> logger, IMemoryCache? cache = null, ILoggerFactory? loggerFactory = null)
            : base(httpClient, logger as ILogger<AudibleMetadataService> ?? LoggerFactory.Create(_ => { }).CreateLogger<AudibleMetadataService>(), cache, loggerFactory)
        {
        }

        public new async Task<AudibleBookMetadata?> ScrapeAmazonMetadataAsync(string asin)
        {
            if (string.IsNullOrWhiteSpace(asin)) return new AudibleBookMetadata();

            var cacheKey = $"amazon:metadata:{asin}";
            if (_cache != null && _cache.TryGetValue(cacheKey, out AudibleBookMetadata? cached) && cached != null)
            {
                _logger.LogDebug("Cache hit for ASIN {Asin}", asin);
                return cached;
            }

            // Scrape Amazon.com
            var url = $"https://www.amazon.com/dp/{asin}";

            try
            {
                var (html, statusCode) = await GetHtmlAsync(url);

                // If Amazon returns 404, return null
                if (statusCode == 404)
                {
                    _logger.LogDebug("Amazon returned 404 for ASIN {Asin}", asin);
                    return null;
                }

                if (string.IsNullOrEmpty(html)) return null;

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var metadata = new AudibleBookMetadata();

                // Attempt to pull structured JSON-LD first
                var jsonLd = SharedMetadataExtractors.ExtractFromJsonLd(doc, _logger);
                if (jsonLd != null)
                {
                    if (!string.IsNullOrWhiteSpace(jsonLd.Title)) metadata.Title = jsonLd.Title;
                    if (!string.IsNullOrWhiteSpace(jsonLd.Description)) metadata.Description = jsonLd.Description;
                    if (!string.IsNullOrWhiteSpace(jsonLd.ImageUrl)) metadata.ImageUrl = jsonLd.ImageUrl;
                    if (!string.IsNullOrWhiteSpace(jsonLd.PublishYear)) metadata.PublishYear = jsonLd.PublishYear;
                    if (!string.IsNullOrWhiteSpace(jsonLd.Language)) metadata.Language = jsonLd.Language;
                    if (!string.IsNullOrWhiteSpace(jsonLd.Publisher)) metadata.Publisher = jsonLd.Publisher;
                    if (jsonLd.Authors != null && jsonLd.Authors.Any()) metadata.Authors = jsonLd.Authors;
                    if (jsonLd.Narrators != null && jsonLd.Narrators.Any()) metadata.Narrators = jsonLd.Narrators;
                }

                // Check if page is unavailable
                if (SharedMetadataExtractors.IsUnavailablePage(doc))
                {
                    _logger.LogDebug("Amazon page for ASIN {Asin} appears unavailable", asin);
                    return null;
                }

                // ASIN - set canonical property
                metadata.Asin = asin;

                // Set source to Amazon
                metadata.Source = "Amazon";

                // Extract title if not already set
                if (string.IsNullOrWhiteSpace(metadata.Title))
                {
                    metadata.Title = AmazonMetadataExtractors.ExtractTitle(doc);
                    if (!string.IsNullOrWhiteSpace(metadata.Title))
                    {
                        _logger.LogInformation("Extracted title from Amazon: {Title}", metadata.Title);
                    }
                }

                // Extract image if not already set
                if (string.IsNullOrWhiteSpace(metadata.ImageUrl))
                {
                    metadata.ImageUrl = AmazonMetadataExtractors.ExtractImageUrl(doc, _logger);
                    if (!string.IsNullOrWhiteSpace(metadata.ImageUrl))
                    {
                        // Validate image dimensions: ignore 1x1 or non-square images
                        try
                        {
                            var isSquare = await IsImageSquareAsync(metadata.ImageUrl!);
                            if (!isSquare)
                            {
                                _logger.LogInformation("Discarding non-square or placeholder image for ASIN {Asin}: {ImageUrl}", metadata.Asin, metadata.ImageUrl);
                                metadata.ImageUrl = null;
                            }
                            else
                            {
                                _logger.LogInformation("Extracted image from Amazon: {ImageUrl}", metadata.ImageUrl);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error validating image for ASIN {Asin}; keeping image URL: {ImageUrl}", metadata.Asin, metadata.ImageUrl);
                        }
                    }
                }

                // Extract from Amazon's audibleproductdetails_feature_div
                AmazonMetadataExtractors.ExtractFromAmazonAudibleDetails(doc, metadata, _logger);

                // Extract using Amazon-specific methods
                if (string.IsNullOrWhiteSpace(metadata.Language))
                {
                    metadata.Language = AmazonMetadataExtractors.ExtractLanguage(doc);
                }

                if (string.IsNullOrWhiteSpace(metadata.Description))
                {
                    metadata.Description = AmazonMetadataExtractors.ExtractDescription(doc);
                }

                if (string.IsNullOrWhiteSpace(metadata.PublishYear))
                {
                    metadata.PublishYear = AmazonMetadataExtractors.ExtractPublishYear(doc);
                }

                // Parse series from title if present
                SharedMetadataExtractors.ParseSeriesFromTitle(metadata, _logger);

                // Cache the result
                if (_cache != null && metadata != null)
                {
                    _cache.Set(cacheKey, metadata, TimeSpan.FromHours(24));
                }

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping Amazon metadata for ASIN {Asin}", asin);
                return null;
            }
        }

        /// <summary>
        /// Downloads the image at the given URL and returns true if it's square and larger than 1x1.
        /// Returns false for placeholders, non-square or 1x1 images, or on any error.
        /// </summary>
        private async Task<bool> IsImageSquareAsync(string imageUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return false;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode) return false;

                using var stream = await resp.Content.ReadAsStreamAsync(ct);
                // Read up to first 64KB which is more than enough to find headers for common formats
                var buffer = new byte[64 * 1024];
                var read = 0;
                while (read < buffer.Length)
                {
                    var r = await stream.ReadAsync(buffer, read, buffer.Length - read, ct);
                    if (r == 0) break;
                    read += r;
                }

                var content = new byte[read];
                Array.Copy(buffer, content, read);

                var dims = TryGetImageDimensions(content);
                if (dims == null) return false;
                var (w, h) = dims.Value;
                if (w <= 1 || h <= 1) return false;
                return w == h;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IsImageSquareAsync failed for URL {Url}", imageUrl);
                return false;
            }
        }

        private static (int width, int height)? TryGetImageDimensions(byte[] data)
        {
            if (data == null || data.Length < 10) return null;

            // PNG: signature 89 50 4E 47 0D 0A 1A 0A, IHDR chunk contains width/height at offset 16..23 (big-endian)
            if (data.Length >= 24 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            {
                try
                {
                    var width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
                    var height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
                    return (width, height);
                }
                catch { return null; }
            }

            // GIF: header GIF87a or GIF89a; width/height at offset 6..9 little-endian 16-bit
            if (data.Length >= 10 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
            {
                try
                {
                    var width = BitConverter.ToUInt16(data, 6);
                    var height = BitConverter.ToUInt16(data, 8);
                    return ((int)width, (int)height);
                }
                catch { return null; }
            }

            // JPEG: need to parse segments to find SOF0/2 marker which contains height/width
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
            {
                int offset = 2;
                while (offset + 9 < data.Length)
                {
                    if (data[offset] != 0xFF) break;
                    var marker = data[offset + 1];
                    // SOF0 (0xC0), SOF2 (0xC2) contain frame header
                    if (marker == 0xC0 || marker == 0xC2)
                    {
                        try
                        {
                            // length = next two bytes
                            var length = (data[offset + 2] << 8) + data[offset + 3];
                            if (offset + 5 + 4 >= data.Length) return null;
                            var height = (data[offset + 5] << 8) + data[offset + 6];
                            var width = (data[offset + 7] << 8) + data[offset + 8];
                            return (width, height);
                        }
                        catch { return null; }
                    }
                    else
                    {
                        // Skip this segment
                        if (offset + 4 >= data.Length) break;
                        var segLen = (data[offset + 2] << 8) + data[offset + 3];
                        if (segLen < 2) break;
                        offset += 2 + segLen;
                    }
                }
            }

            return null;
        }
    }

    public interface IAmazonMetadataService
    {
        Task<AudibleBookMetadata?> ScrapeAmazonMetadataAsync(string asin);
    }
}
