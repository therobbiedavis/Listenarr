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
                        _logger.LogInformation("Extracted image from Amazon: {ImageUrl}", metadata.ImageUrl);
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
    }

    public interface IAmazonMetadataService
    {
        Task<AudibleBookMetadata?> ScrapeAmazonMetadataAsync(string asin);
    }
}
