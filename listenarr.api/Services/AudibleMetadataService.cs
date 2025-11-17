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
using Listenarr.Api.Models;
using HtmlAgilityPack;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Playwright;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public class AudibleMetadataService : IAudibleMetadataService
    {
        // Playwright fallback counter
        private static long _playwrightFallbackCount = 0;

        private readonly HttpClient _httpClient;
        private readonly ILogger<AudibleMetadataService> _logger;
        private readonly IMemoryCache? _cache;
        private readonly ILoggerFactory _loggerFactory;

        public AudibleMetadataService(HttpClient httpClient, ILogger<AudibleMetadataService> logger, IMemoryCache? cache = null, ILoggerFactory? loggerFactory = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _loggerFactory = loggerFactory ?? LoggerFactory.Create(_ => { });
        }

        public async Task<AudibleBookMetadata> ScrapeAudibleMetadataAsync(string asin)
        {
            if (string.IsNullOrWhiteSpace(asin)) return new AudibleBookMetadata();

            var cacheKey = $"audible:metadata:{asin}";
            if (_cache != null && _cache.TryGetValue(cacheKey, out AudibleBookMetadata? cached) && cached != null)
            {
                _logger.LogDebug("Cache hit for ASIN {Asin}", asin);
                return cached;
            }

            // Try Audible product page first, then Amazon fallback on 404
            var tryUrls = new[] {
                $"https://www.audible.com/pd/{asin}",
                $"https://www.amazon.com/dp/{asin}",
                $"https://www.amazon.com/gp/product/{asin}"
            };

            foreach (var url in tryUrls)
            {
                try
                {
                    var (html, statusCode) = await GetHtmlAsync(url);
                    
                    // If Audible returns 404, immediately try Amazon
                    if (statusCode == 404 && url.Contains("audible.com"))
                    {
                        _logger.LogDebug("Audible returned 404 for ASIN {Asin}, trying Amazon fallback", asin);
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(html)) continue;
                    
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var metadata = new AudibleBookMetadata();

                    // Attempt to pull structured JSON-LD first (if present) — often contains reliable data
                    var jsonLd = ExtractFromJsonLd(doc);
                    if (jsonLd != null)
                    {
                        if (!string.IsNullOrWhiteSpace(jsonLd.Title)) metadata.Title = jsonLd.Title;
                        if (!string.IsNullOrWhiteSpace(jsonLd.Description)) metadata.Description = jsonLd.Description;
                        if (!string.IsNullOrWhiteSpace(jsonLd.ImageUrl)) metadata.ImageUrl = jsonLd.ImageUrl;
                        if (!string.IsNullOrWhiteSpace(jsonLd.PublishYear)) metadata.PublishYear = jsonLd.PublishYear;
                        if (!string.IsNullOrWhiteSpace(jsonLd.Language)) metadata.Language = jsonLd.Language;
                        if (!string.IsNullOrWhiteSpace(jsonLd.Publisher)) metadata.Publisher = jsonLd.Publisher;
                        if (jsonLd.Authors != null && jsonLd.Authors.Any()) metadata.Authors = jsonLd.Authors;
                        if (jsonLd.Narrators != null && jsonLd.Narrators.Any()) 
                        {
                            metadata.Narrators = jsonLd.Narrators;
                            _logger.LogInformation("Extracted {Count} narrator(s) from JSON-LD: {Narrators}", jsonLd.Narrators.Count, string.Join(", ", jsonLd.Narrators));
                        }
                    }

                    // If the page explicitly indicates the audiobook is unavailable, skip accepting this page
                    if (IsUnavailablePage(doc))
                    {
                        _logger.LogDebug("Audible page for {Url} appears unavailable; skipping", url);
                        continue;
                    }

                    // ASIN - set canonical property only
                    metadata.Asin = asin;
                    
                    // Set source based on URL
                    if (url.Contains("audible.com"))
                    {
                        metadata.Source = "Audible";
                    }
                    else if (url.Contains("amazon."))
                    {
                        metadata.Source = "Amazon";
                    }

                    // Extract from adbl-product-details component FIRST (Audible's primary metadata container)
                    if (url.Contains("audible.com"))
                    {
                        // Try to extract from search result list item first (has clean structured data)
                        ExtractFromSearchResult(doc, metadata);
                        
                        // Then extract from product details page (will fill in missing fields)
                        ExtractFromProductDetails(doc, metadata);
                    }
                    
                    // Extract from Amazon's audibleproductdetails_feature_div section (requires Playwright rendering)
                    if (url.Contains("amazon.com"))
                    {
                        ExtractFromAmazonAudibleDetails(doc, metadata);
                    }

                    // Title (fall back to visible selectors if component didn't provide)
                    if (string.IsNullOrWhiteSpace(metadata.Title))
                    {
                        var titleNode = doc.DocumentNode.SelectSingleNode("//h1 | //h1//span | //span[@id='productTitle'] | //meta[@property='og:title']");
                        var title = titleNode?.InnerText?.Trim() ?? titleNode?.GetAttributeValue("content", null)?.Trim();
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            // Normalize whitespace and clean up title
                            title = System.Text.RegularExpressions.Regex.Replace(title, "\\s+", " ").Trim();
                            
                            // Remove common Amazon title suffixes for cleaner display
                            title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*[:-]\s*(?:Audible Audio Edition|Audible Audiobook|Audio CD|MP3 CD|Kindle Edition|Paperback|Hardcover)\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            
                            metadata.Title = title;
                        }
                    }
                    
                    // Parse series information from title (e.g., "Fourth Wing: Empyrean, Book 1")
                    // Only parse if series not already set
                    if (string.IsNullOrWhiteSpace(metadata.Series))
                    {
                        ParseSeriesFromTitle(metadata);
                    }
                    
                    // Description - fallback to older selectors if not found in product-details
                    if (string.IsNullOrWhiteSpace(metadata.Description))
                    {
                        metadata.Description = ExtractDescription(doc);
                    }

                    // Language / PublishYear - fallback if not from product-details
                    if (string.IsNullOrWhiteSpace(metadata.Language))
                    {
                        metadata.Language = ExtractLanguage(doc);
                    }
                    if (string.IsNullOrWhiteSpace(metadata.PublishYear))
                    {
                        metadata.PublishYear = ExtractPublishYear(doc);
                    }
                    
                    // Clean language field - remove currency codes
                    if (!string.IsNullOrWhiteSpace(metadata.Language))
                    {
                        metadata.Language = CleanLanguage(metadata.Language);
                    }

                    // Image (only if not already extracted from component)
                    if (string.IsNullOrWhiteSpace(metadata.ImageUrl))
                    {
                        // Try multiple selectors for images - prioritize product images
                        var img = doc.DocumentNode.SelectSingleNode("//img[contains(@id,'ebooksImgBlkFront')]")?.GetAttributeValue("src", null)
                            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@id,'imgBlkFront')]")?.GetAttributeValue("src", null)
                            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@data-a-dynamic-image,'')]")?.GetAttributeValue("data-a-dynamic-image", null)
                            ?? doc.DocumentNode.SelectSingleNode("//div[@id='img-canvas']//img")?.GetAttributeValue("src", null)
                            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@class,'a-dynamic-image')]")?.GetAttributeValue("src", null)
                            ?? doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null)
                            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@class,'cover') or contains(@id,'main-image')]")?.GetAttributeValue("src", null);
                        
                        // Handle data-a-dynamic-image JSON attribute
                        if (!string.IsNullOrWhiteSpace(img) && img.StartsWith("{"))
                        {
                            try
                            {
                                // Parse the dynamic image JSON to get the first URL
                                var jsonMatch = System.Text.RegularExpressions.Regex.Match(img, @"""(https://[^""]+)""");
                                if (jsonMatch.Success)
                                {
                                    img = jsonMatch.Groups[1].Value;
                                }
                            }
                            catch { }
                        }
                        
                        if (!string.IsNullOrWhiteSpace(img))
                        {
                            // Clean Amazon image URLs (remove social share overlays and logos)
                            var cleanedImg = CleanImageUrl(img);
                            if (!string.IsNullOrWhiteSpace(cleanedImg))
                            {
                                metadata.ImageUrl = cleanedImg;
                            }
                            else
                            {
                                _logger.LogDebug("Image URL filtered out after cleaning: {Url}", img);
                            }
                        }
                    }

                    // Authors (only if not already extracted from component)
                    if (metadata.Authors == null || !metadata.Authors.Any())
                    {
                        var authorNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/author')]|//span[contains(@class,'author')]|//li[contains(.,'By')]");
                        if (authorNodes != null)
                        {
                            var authors = authorNodes
                                .Select(n => NormalizeNameString(n.InnerText))
                                .Where(s => !string.IsNullOrWhiteSpace(s) && !IsAuthorNoise(s))
                                .Distinct()
                                .Take(10)
                                .ToList();
                            if (authors.Any()) metadata.Authors = authors!;
                        }
                    }

                    // Narrators - target specific product page elements, avoid recommendation sections
                    if (metadata.Narrators == null || !metadata.Narrators.Any())
                    {
                        var narratorNodes = doc.DocumentNode.SelectNodes(
                            "//li[contains(@class,'narratorLabel')]//a | " +
                            "//span[contains(@class,'narratorLabel')]//a | " +
                            "//li[contains(.,'Narrated by:')]//a | " +
                            "//span[contains(.,'Narrated by:')]/following-sibling::a[position() <= 5] | " +
                            "//a[contains(@href,'/narrator/')] | " +
                            "//span[contains(text(),'Narrated by')]/following-sibling::span//a");
                        if (narratorNodes != null)
                        {
                            var narrators = narratorNodes
                                .Select(n => NormalizeNameString(n.InnerText))
                                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Contains("Narrated by", StringComparison.OrdinalIgnoreCase))
                                .Distinct()
                                .Take(10) // Limit to first 10 to avoid aggregate lists
                                .ToList();
                            if (narrators.Any()) metadata.Narrators = narrators!;
                        }
                        
                        // Fallback: Try extracting narrators from description for Amazon pages
                        if ((metadata.Narrators == null || !metadata.Narrators.Any()) && !string.IsNullOrWhiteSpace(metadata.Description))
                        {
                            // Look for patterns like "read by Narrator Name" or "narrated by Narrator Name"
                            var narratorMatch = System.Text.RegularExpressions.Regex.Match(
                                metadata.Description, 
                                @"(?:read by|narrated by|narrator[s]?:)\s*([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+(?:\s+and\s+[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)?)", 
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (narratorMatch.Success)
                            {
                                var narratorText = NormalizeNameString(narratorMatch.Groups[1].Value);
                                if (!string.IsNullOrWhiteSpace(narratorText))
                                {
                                    metadata.Narrators = narratorText.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                                    _logger.LogInformation("Extracted narrators from description: {Narrators}", string.Join(", ", metadata.Narrators));
                                }
                            }
                        }
                    }
                    
                    // Clean up authors list: remove narrators and publisher from authors if they snuck in
                    if (metadata.Authors != null && metadata.Authors.Any())
                    {
                        var originalAuthorCount = metadata.Authors.Count;
                        metadata.Authors = metadata.Authors
                            .Where(a => !a.Contains("Publisher", StringComparison.OrdinalIgnoreCase))
                            .Where(a => metadata.Narrators == null || !metadata.Narrators.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                        
                        if (metadata.Authors.Count < originalAuthorCount)
                        {
                            _logger.LogInformation("Filtered authors from {Original} to {Filtered} (removed narrators/publisher)", originalAuthorCount, metadata.Authors.Count);
                        }
                    }

                    // Publisher - try meta tags or labeled fields (fallback if not from product-details)
                    if (string.IsNullOrWhiteSpace(metadata.Publisher))
                    {
                        var publisher = doc.DocumentNode.SelectSingleNode("//meta[@name='publisher']")?.GetAttributeValue("content", null)
                            ?? doc.DocumentNode.SelectSingleNode("//li[contains(.,'Publisher')]|//th[contains(.,'Publisher')]/following-sibling::td")?.InnerText?.Trim();
                        if (!string.IsNullOrWhiteSpace(publisher)) metadata.Publisher = publisher;
                    }

                    // Genres - best effort: look for breadcrumb or category (fallback if not from product-details)
                    if (metadata.Genres == null || !metadata.Genres.Any())
                    {
                        var genreNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/genres') or contains(@class,'genre')]|//li[contains(@class,'genre')]//a");
                        if (genreNodes != null)
                        {
                            metadata.Genres = genreNodes.Select(n => n.InnerText?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()!;
                        }
                    }

                    // Runtime - try to extract minutes (fallback if not from product-details)
                    if (metadata.Runtime == null)
                    {
                        var runtimeText = doc.DocumentNode.SelectSingleNode("//span[contains(@class,'runtimeLabel')]|//li[contains(text(),'hrs')]|//span[contains(text(),'hr')]")?.InnerText;
                        if (!string.IsNullOrWhiteSpace(runtimeText))
                        {
                            var hrsMatch = System.Text.RegularExpressions.Regex.Match(runtimeText, "(\\d+)\\s*hrs?");
                            var minsMatch = System.Text.RegularExpressions.Regex.Match(runtimeText, "(\\d+)\\s*mins?");
                            int total = 0;
                            if (hrsMatch.Success) total += int.Parse(hrsMatch.Groups[1].Value) * 60;
                            if (minsMatch.Success) total += int.Parse(minsMatch.Groups[1].Value);
                            if (total > 0) metadata.Runtime = total;
                        }
                    }

                    // Ensure at least ASIN or Title present before accepting
                    if (!string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.ImageUrl) || (metadata.Authors != null && metadata.Authors.Any()))
                    {
                        _logger.LogInformation("Metadata extracted for ASIN {Asin} from {Source}: Title={Title}, Authors={AuthorCount}, Language={Language}, Publisher={Publisher}, HasDescription={HasDesc}",
                            asin, metadata.Source ?? "Unknown", metadata.Title, metadata.Authors?.Count ?? 0, metadata.Language, metadata.Publisher, !string.IsNullOrWhiteSpace(metadata.Description));
                        
                        if (_cache != null)
                        {
                            _cache.Set(cacheKey, metadata, TimeSpan.FromHours(12));
                        }
                        return metadata;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Scrape attempt failed for URL {Url}", url);
                }
            }

            // If nothing found, return minimal metadata with ASIN to allow fallback
            var fallbackMeta = new AudibleBookMetadata { Asin = asin };
            if (_cache != null) _cache.Set(cacheKey, fallbackMeta, TimeSpan.FromHours(1));
            return fallbackMeta;
        }

        private async Task<(string? html, int statusCode)> GetHtmlAsync(string url)
        {
            try
            {
                // Always use Playwright for Audible URLs since adbl-product-details requires JS rendering
                if (url.Contains("audible.com"))
                {
                    _logger.LogInformation("Using Playwright for Audible URL to render adbl-product-details component: {Url}", url);
                    return await GetHtmlWithPlaywrightAsync(url);
                }
                
                // Also use Playwright for Amazon URLs to access audibleproductdetails_feature_div
                if (url.Contains("amazon.com") && url.Contains("/dp/"))
                {
                    _logger.LogInformation("Using Playwright for Amazon URL to render audibleproductdetails_feature_div: {Url}", url);
                    return await GetHtmlWithPlaywrightAsync(url);
                }
                
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                var response = await _httpClient.SendAsync(request);
                var statusCode = (int)response.StatusCode;
                
                if (statusCode == 404)
                {
                    _logger.LogDebug("404 Not Found for {Url}", url);
                    return (null, 404);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    // Basic bot detection check
                    if (html.Contains("To discuss automated access to Amazon data please contact") || html.Contains("Robot Check") || html.Contains("automated traffic"))
                    {
                        _logger.LogDebug("Bot-detection detected in HTTP fetch for {Url}", url);
                    }
                    else
                    {
                        return (html, statusCode);
                    }
                }

                // Try Playwright fallback on-demand (await-using for async disposables)
                return await GetHtmlWithPlaywrightAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetHtmlAsync failed for {Url}", url);
                return (null, 0);
            }
        }

        private async Task<(string? html, int statusCode)> GetHtmlWithPlaywrightAsync(string url)
        {
            try
            {
                var pwLogger = _loggerFactory.CreateLogger("PlaywrightFallback");
                pwLogger.LogDebug("Starting Playwright rendering for {Url}", url);
                System.Threading.Interlocked.Increment(ref _playwrightFallbackCount);
                pwLogger.LogInformation("Playwright invocation count: {Count}", Interlocked.Read(ref _playwrightFallbackCount));
                
                using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } });
                await using var context = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true, UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36" });
                var page = await context.NewPageAsync();

                // Navigate with a more resilient wait strategy (DOMContentLoaded instead of NetworkIdle)
                // Increase timeout to 60 seconds for slow connections/CAPTCHA scenarios
                var gotoOptions = new Microsoft.Playwright.PageGotoOptions { WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded, Timeout = 60000 };
                var pwResponse = await page.GotoAsync(url, gotoOptions);
                
                var responseStatus = pwResponse?.Status ?? 0;
                if (responseStatus == 404)
                {
                    pwLogger.LogDebug("Playwright received 404 for {Url}", url);
                    return (null, 404);
                }

                // Wait for Audible-specific component or common title selectors
                var selectors = new[] { "adbl-product-details", "adbl-page[loaded]", "#productTitle", ".productTitle", ".product-title", "meta[property=\"og:title\"]" };
                bool found = false;
                foreach (var sel in selectors)
                {
                    try
                    {
                        var wait = await page.WaitForSelectorAsync(sel, new PageWaitForSelectorOptions { Timeout = 5000 });
                        if (wait != null)
                        {
                            pwLogger.LogInformation("Playwright selector matched {Selector} for {Url}", sel, url);
                            found = true;
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        // ignore timeouts per selector
                    }
                }

                // Give extra time for adbl-product-details to fully render
                if (found)
                {
                    await page.WaitForTimeoutAsync(2000); // Wait 2s for component hydration
                }

                var content = await page.ContentAsync();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    pwLogger.LogInformation("Playwright rendered {Length} chars for {Url} (selectorMatched={Matched})", content.Length, url, found);
                    return (content, responseStatus > 0 ? responseStatus : 200);
                }
                
                return (null, 0);
            }
            catch (Exception ex)
            {
                // Detect common Playwright missing-browser error and provide a clear, actionable log message
                try
                {
                    var msg = ex.Message ?? string.Empty;
                    if (msg.IndexOf("Executable doesn't exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("Looks like Playwright was just installed or updated", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Keep a short warning for operators and a debug log with full exception
                        _logger.LogWarning("Playwright rendering skipped for {Url} because browser executables are missing. To fix run the Playwright install script in the build output or run 'npx playwright install' in the project root. See logs for details.", url);
                        _logger.LogDebug(ex, "Playwright missing-executable detail");
                        return (null, 0);
                    }
                }
                catch
                {
                    // ignore any inspection errors and fall through to standard logging
                }

                _logger.LogWarning(ex, "Playwright rendering failed for {Url}", url);
                return (null, 0);
            }
        }

        public async Task<List<AudibleBookMetadata>> PrefetchAsync(List<string> asins)
        {
            var results = new List<AudibleBookMetadata>();
            foreach (var asin in asins)
            {
                var metadata = await ScrapeAudibleMetadataAsync(asin);
                results.Add(metadata);
            }
            return results;
        }

        private string? ExtractLanguage(HtmlDocument doc)
        {
            var languageNode = doc.DocumentNode.SelectSingleNode(
                "//div[@id='detailBullets_feature_div']//span[text()='Language']//following-sibling::span | " +
                "//div[@id='detailBullets_feature_div']//li[contains(.,'Language')]//span[position()>1] | " +
                "//li[contains(.,'Language')]//span[last()] | " +
                "//span[text()='Language:']/following-sibling::span | " +
                "//th[contains(text(),'Language')]/following-sibling::td | " +
                "//td[contains(text(),'Language:')]/following-sibling::td | " +
                "//tr[@id='detailsLanguage']//td//span");
            var text = languageNode?.InnerText.Trim();
            if (text == "Language" || string.IsNullOrEmpty(text))
            {
                var languageListItem = doc.DocumentNode.SelectSingleNode("//li[contains(.,'Language')]");
                if (languageListItem != null)
                {
                    var listText = languageListItem.InnerText.Trim();
                    var match = System.Text.RegularExpressions.Regex.Match(listText, @"Language[:\s]+([^;\(\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        text = match.Groups[1].Value.Trim();
                    }
                }
            }
            if (!string.IsNullOrEmpty(text) && text != "Language")
            {
                text = text.Replace("Language:", "").Trim();
                return text;
            }
            return null;
        }

        private string? ExtractDescription(HtmlDocument doc)
        {
            var descriptionNode = doc.DocumentNode.SelectSingleNode(
                "//div[@data-expanded and contains(@class,'a-expander-content')] | " +
                "//div[@id='bookDescription_feature_div']//div[contains(@class,'a-expander-content')] | " +
                "//div[@id='bookDescription_feature_div']//noscript | " +
                "//div[@id='bookDescription_feature_div']//span | " +
                "//div[@id='productDescription']//p | " +
                "//div[@id='productDescription'] | " +
                "//div[contains(@class,'bookDescription')]//span | " +
                "//div[contains(@class,'productDescription')] | " +
                "//span[@id='productDescription'] | " +
                "//div[@data-feature-name='bookDescription']//span");
            if (descriptionNode != null)
            {
                var htmlContent = descriptionNode.InnerHtml?.Trim();
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    htmlContent = System.Text.RegularExpressions.Regex.Replace(htmlContent, @"\s+", " ");
                    htmlContent = System.Text.RegularExpressions.Regex.Replace(htmlContent, @">\s+<", "><");
                    return htmlContent.Trim();
                }
            }
            return null;
        }

        private string? ExtractPublishYear(HtmlDocument doc)
        {
            var releaseDateNode = doc.DocumentNode.SelectSingleNode(
                "//div[@id='detailBullets_feature_div']//span[contains(text(),'Audible.com release date') or contains(text(),'Release date')]/following-sibling::span | " +
                "//li[contains(.,'Audible.com release date') or contains(.,'Release date')]//span[last()] | " +
                "//span[contains(text(),'release date')]/following-sibling::span | " +
                "//th[contains(text(),'Release')]/following-sibling::td | " +
                "//td[contains(text(),'Release')]/following-sibling::td");
            var dateText = releaseDateNode?.InnerText.Trim();
            if (string.IsNullOrEmpty(dateText) || dateText.ToLower().Contains("release"))
            {
                var releaseDateListItem = doc.DocumentNode.SelectSingleNode("//li[contains(.,'release date')]");
                if (releaseDateListItem != null)
                {
                    var listText = releaseDateListItem.InnerText.Trim();
                    var match = System.Text.RegularExpressions.Regex.Match(listText, @"release date[:\s]+([^;\(\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        dateText = match.Groups[1].Value.Trim();
                    }
                }
            }
            return ExtractYear(dateText);
        }

        /// <summary>
        /// Normalize a raw name string from scraped HTML: remove labels, split on common separators,
        /// trim and dedupe names, and return a comma-joined string (or null if empty).
        /// </summary>
        private string? NormalizeNameString(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            // Remove common labels
            raw = System.Text.RegularExpressions.Regex.Replace(raw, "Narrated by:|Narrated by|Narrator:|By:", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            raw = raw.Replace("\n", ", ").Replace("\\r", ", ").Replace("\t", " ");
            
            // Remove Amazon-specific suffixes like (Author), (Narrator), etc.
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @",?\s*\((?:Author|Narrator|Reader|Performer|Contributor)\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Split on commas, 'and', '/', '&' and other separators
            var parts = System.Text.RegularExpressions.Regex.Split(raw, @",|\band\b|/|&|\\u0026");
            var names = parts.Select(p => p?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            if (!names.Any()) return null;
            return string.Join(", ", names);
        }

        private string? CleanImageUrl(string? imgUrl)
        {
            if (string.IsNullOrWhiteSpace(imgUrl)) return null;
            
            // Filter out logos, navigation images, and batch processing URLs
            if (imgUrl.Contains("/navigation/") || 
                imgUrl.Contains("logo") || 
                imgUrl.Contains("/batch/") ||
                imgUrl.Contains("fls-na.amazon") ||
                imgUrl.StartsWith("data:"))
            {
                _logger.LogDebug("Filtered out non-product image: {Url}", imgUrl);
                return null;
            }
            
            // Clean up social share overlay URLs - extract the base image ID
            // Example: https://m.media-amazon.com/images/I/61D7uTS7-TL._SL10_UR1600,800_CR200,50,1200,630_CLa|1200,630|61D7uTS7-TL.jpg|...
            // Should become: https://m.media-amazon.com/images/I/61D7uTS7-TL._SL500_.jpg
            var cleanUrl = imgUrl;
            if (imgUrl.Contains("PJAdblSocialShare") || imgUrl.Contains("_CLa") || imgUrl.Contains("_SL10_"))
            {
                // Extract image ID (e.g., 61D7uTS7-TL)
                var match = System.Text.RegularExpressions.Regex.Match(imgUrl, @"/images/I/([A-Za-z0-9_-]+)\.");
                if (match.Success)
                {
                    var imageId = match.Groups[1].Value;
                    cleanUrl = $"https://m.media-amazon.com/images/I/{imageId}._SL500_.jpg";
                    _logger.LogInformation("Cleaned social share URL to: {CleanUrl}", cleanUrl);
                }
            }
            
            return cleanUrl;
        }

        private record JsonLdData(string? Title, string? Description, string? ImageUrl, string? PublishYear, string? Language, string? Publisher, List<string>? Authors, List<string>? Narrators);

        private JsonLdData? ExtractFromJsonLd(HtmlDocument doc)
        {
            try
            {
                var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (scriptNodes == null) return null;
                foreach (var sn in scriptNodes)
                {
                    var text = sn.InnerText?.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    try
                    {
                        using var docJson = JsonDocument.Parse(text);
                        var root = docJson.RootElement;
                        // Handle either an object or an array
                        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                        {
                            root = root[0];
                        }
                        string? title = null, desc = null, img = null, year = null, lang = null, pub = null;
                        List<string>? authors = null;
                        if (root.TryGetProperty("name", out var p)) title = p.GetString();
                        if (root.TryGetProperty("headline", out p)) desc = p.GetString();
                        if (root.TryGetProperty("description", out p) && string.IsNullOrEmpty(desc)) desc = p.GetString();
                        if (root.TryGetProperty("image", out p)) img = p.ValueKind == JsonValueKind.String ? p.GetString() : p[0].GetString();
                        if (root.TryGetProperty("datePublished", out p)) year = p.GetString();
                        if (root.TryGetProperty("inLanguage", out p)) lang = p.GetString();
                        if (root.TryGetProperty("publisher", out p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var np)) pub = np.GetString();
                        if (root.TryGetProperty("author", out p))
                        {
                            authors = new List<string>();
                            if (p.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var a in p.EnumerateArray())
                                {
                                    if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
                                    else if (a.ValueKind == JsonValueKind.String) authors.Add(a.GetString() ?? "");
                                }
                            }
                            else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var an)) authors.Add(an.GetString() ?? "");
                            else if (p.ValueKind == JsonValueKind.String) authors.Add(p.GetString() ?? "");
                        }
                        
                        // Check for narrator/readBy in JSON-LD
                        List<string>? narrators = null;
                        if (root.TryGetProperty("readBy", out p) || root.TryGetProperty("narrator", out p))
                        {
                            narrators = new List<string>();
                            if (p.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var n in p.EnumerateArray())
                                {
                                    if (n.ValueKind == JsonValueKind.Object && n.TryGetProperty("name", out var nn)) narrators.Add(nn.GetString() ?? "");
                                    else if (n.ValueKind == JsonValueKind.String) narrators.Add(n.GetString() ?? "");
                                }
                            }
                            else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("name", out var nn)) narrators.Add(nn.GetString() ?? "");
                            else if (p.ValueKind == JsonValueKind.String) narrators.Add(p.GetString() ?? "");
                        }
                        
                        return new JsonLdData(title, desc, img, year, lang, pub, authors, narrators);
                    }
                    catch (JsonException) { continue; }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error parsing JSON-LD");
            }
            return null;
        }

        private bool IsUnavailablePage(HtmlDocument doc)
        {
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? string.Empty;
            if (!string.IsNullOrEmpty(title) && title.ToLower().Contains("not available")) return true;
            var bodyText = doc.DocumentNode.InnerText ?? string.Empty;
            if (bodyText.IndexOf("this audiobook is not available", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (bodyText.IndexOf("audiobook is not available", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private string? ExtractYear(string? dateText)
        {
            if (string.IsNullOrEmpty(dateText)) return null;
            var yearMatch = System.Text.RegularExpressions.Regex.Match(dateText, @"\b(19|20)\d{2}\b");
            return yearMatch.Success ? yearMatch.Value : null;
        }

        /// <summary>
        /// Extract metadata from Amazon's audibleproductdetails_feature_div section (requires Playwright rendering).
        /// This section contains structured metadata: Author, Narrator, Listening Length, Publisher, Language, Series, etc.
        /// </summary>
        private void ExtractFromAmazonAudibleDetails(HtmlDocument doc, AudibleBookMetadata metadata)
        {
            var audibleDetails = doc.DocumentNode.SelectSingleNode("//div[@id='audibleproductdetails_feature_div']");
            if (audibleDetails == null)
            {
                _logger.LogDebug("audibleproductdetails_feature_div not found for ASIN {Asin}", metadata.Asin);
                return;
            }
            
            _logger.LogInformation("Found audibleproductdetails_feature_div for ASIN {Asin}", metadata.Asin);
            
            // Parse the table rows - they contain label/value pairs
            var rows = audibleDetails.SelectNodes(".//tr");
            if (rows == null || !rows.Any())
            {
                _logger.LogDebug("No rows found in audibleproductdetails_feature_div");
                return;
            }
            
            foreach (var row in rows)
            {
                var labelCell = row.SelectSingleNode(".//td[@class='a-span3']|.//th");
                var valueCell = row.SelectSingleNode(".//td[@class='a-span9']|.//td[not(@class='a-span3')]");
                
                if (labelCell == null || valueCell == null) continue;
                
                var label = labelCell.InnerText?.Trim().Replace(":", "").Trim();
                var value = valueCell.InnerText?.Trim();
                
                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value)) continue;
                
                _logger.LogDebug("Amazon detail - {Label}: {Value}", label, value);
                
                switch (label.ToLower())
                {
                    case "author":
                    case "authors":
                        if (metadata.Authors == null || !metadata.Authors.Any())
                        {
                            var authors = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => NormalizeNameString(a))
                                .Where(a => !string.IsNullOrWhiteSpace(a))
                                .ToList();
                            if (authors.Any())
                            {
                                metadata.Authors = authors!;
                                _logger.LogInformation("Extracted authors from Amazon details: {Authors}", string.Join(", ", authors));
                            }
                        }
                        break;
                        
                    case "narrator":
                    case "narrators":
                    case "narrated by":
                        if (metadata.Narrators == null || !metadata.Narrators.Any())
                        {
                            var narrators = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(n => NormalizeNameString(n))
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .ToList();
                            if (narrators.Any())
                            {
                                metadata.Narrators = narrators!;
                                _logger.LogInformation("Extracted narrators from Amazon details: {Narrators}", string.Join(", ", narrators));
                            }
                        }
                        break;
                        
                    case "listening length":
                    case "length":
                        if (metadata.Runtime == null)
                        {
                            // Parse "21 hours and 22 minutes" format
                            var hrsMatch = System.Text.RegularExpressions.Regex.Match(value, @"(\d+)\s*hour");
                            var minsMatch = System.Text.RegularExpressions.Regex.Match(value, @"(\d+)\s*minute");
                            int total = 0;
                            if (hrsMatch.Success) total += int.Parse(hrsMatch.Groups[1].Value) * 60;
                            if (minsMatch.Success) total += int.Parse(minsMatch.Groups[1].Value);
                            if (total > 0)
                            {
                                metadata.Runtime = total;
                                _logger.LogInformation("Extracted runtime from Amazon details: {Runtime} minutes", total);
                            }
                        }
                        break;
                        
                    case "publisher":
                        if (string.IsNullOrWhiteSpace(metadata.Publisher))
                        {
                            metadata.Publisher = value;
                            _logger.LogInformation("Extracted publisher from Amazon details: {Publisher}", value);
                        }
                        break;
                        
                    case "language":
                        if (string.IsNullOrWhiteSpace(metadata.Language))
                        {
                            metadata.Language = CleanLanguage(value);
                            _logger.LogInformation("Extracted language from Amazon details: {Language}", metadata.Language);
                        }
                        break;
                        
                    case "audible.com release date":
                    case "release date":
                        if (string.IsNullOrWhiteSpace(metadata.PublishYear))
                        {
                            // Parse date like "May 02, 2023"
                            var yearMatch = System.Text.RegularExpressions.Regex.Match(value, @"\d{4}");
                            if (yearMatch.Success)
                            {
                                metadata.PublishYear = yearMatch.Value;
                                _logger.LogInformation("Extracted publish year from Amazon details: {Year}", metadata.PublishYear);
                            }
                        }
                        break;
                        
                    case "version":
                        if (string.IsNullOrWhiteSpace(metadata.Version))
                        {
                            metadata.Version = value;
                            _logger.LogInformation("Extracted version from Amazon details: {Version}", value);
                        }
                        break;
                        
                    case "series":
                        if (string.IsNullOrWhiteSpace(metadata.Series))
                        {
                            // Format: "Book 1 of 3 The Empyrean"
                            var seriesMatch = System.Text.RegularExpressions.Regex.Match(value, @"Book\s+(\d+)\s+of\s+\d+\s+(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (seriesMatch.Success)
                            {
                                metadata.SeriesNumber = seriesMatch.Groups[1].Value;
                                metadata.Series = seriesMatch.Groups[2].Value.Trim();
                                _logger.LogInformation("Extracted series from Amazon details: {Series} #{SeriesNumber}", metadata.Series, metadata.SeriesNumber);
                            }
                            else
                            {
                                metadata.Series = value;
                                _logger.LogInformation("Extracted series from Amazon details: {Series}", value);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Extract metadata from Audible search result list item (product-list-item-{ASIN}).
        /// Search results have clean structured data in list items with classes like: runtimeLabel, seriesLabel, releaseDateLabel, languageLabel.
        /// This is often more reliable than product detail page extraction.
        /// </summary>
        private void ExtractFromSearchResult(HtmlDocument doc, AudibleBookMetadata metadata)
        {
            try
            {
                _logger.LogInformation("Starting ExtractFromSearchResult for ASIN {Asin}", metadata.Asin);
                
                // Find the product list item for this ASIN
                var productItem = doc.DocumentNode.SelectSingleNode($"//li[@id='product-list-item-{metadata.Asin}'] | //li[contains(@class,'productListItem')][contains(@id,'{metadata.Asin}')]");
                if (productItem == null)
                {
                    _logger.LogInformation("No search result list item found for ASIN {Asin} - this is expected for product detail pages", metadata.Asin);
                    
                    // Log if we have any list items with class containing "list-item" or "product"
                    var anyListItems = doc.DocumentNode.SelectNodes("//li[contains(@class,'list-item') or contains(@class,'product')]");
                    if (anyListItems != null && anyListItems.Any())
                    {
                        _logger.LogDebug("Found {Count} list items in page, but none matching ASIN", anyListItems.Count);
                    }
                    
                    return;
                }

                _logger.LogInformation("Found search result list item for ASIN {Asin}", metadata.Asin);

                // Extract runtime from runtimeLabel (e.g., "Length: 21 hrs and 22 mins")
                if (metadata.Runtime == null)
                {
                    var runtimeNode = productItem.SelectSingleNode(".//li[contains(@class,'runtimeLabel')]");
                    if (runtimeNode != null)
                    {
                        var runtimeText = runtimeNode.InnerText?.Trim() ?? string.Empty;
                        var match = System.Text.RegularExpressions.Regex.Match(runtimeText, 
                            @"(\d+)\s*hrs?\s+(?:and\s+)?(\d+)\s*mins?", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            int hours = int.Parse(match.Groups[1].Value);
                            int minutes = int.Parse(match.Groups[2].Value);
                            metadata.Runtime = hours * 60 + minutes;
                            _logger.LogInformation("Extracted runtime from search result: {Runtime} minutes ({Hours}h {Minutes}m)", 
                                metadata.Runtime, hours, minutes);
                        }
                    }
                }

                // Extract series from seriesLabel (e.g., "Series: The Empyrean, Book 1")
                if (string.IsNullOrWhiteSpace(metadata.Series))
                {
                    var seriesNode = productItem.SelectSingleNode(".//li[contains(@class,'seriesLabel')]");
                    if (seriesNode != null)
                    {
                        var seriesText = seriesNode.InnerText?.Trim() ?? string.Empty;
                        // Remove "Series:" prefix
                        seriesText = System.Text.RegularExpressions.Regex.Replace(seriesText, @"^\s*Series:\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        // Try to parse "SeriesName, Book N" format
                        var match = System.Text.RegularExpressions.Regex.Match(seriesText, @"^(.+?),\s*Book\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            metadata.Series = match.Groups[1].Value.Trim();
                            metadata.SeriesNumber = match.Groups[2].Value;
                            _logger.LogInformation("Extracted series from search result: {Series}, Book {Number}", 
                                metadata.Series, metadata.SeriesNumber);
                        }
                        else
                        {
                            // Just take the series name without book number
                            metadata.Series = seriesText;
                            _logger.LogInformation("Extracted series from search result: {Series}", metadata.Series);
                        }
                    }
                }

                // Extract release date from releaseDateLabel (e.g., "Release date: 05-02-23")
                if (string.IsNullOrWhiteSpace(metadata.PublishYear))
                {
                    var releaseDateNode = productItem.SelectSingleNode(".//li[contains(@class,'releaseDateLabel')]");
                    if (releaseDateNode != null)
                    {
                        var dateText = releaseDateNode.InnerText?.Trim() ?? string.Empty;
                        var match = System.Text.RegularExpressions.Regex.Match(dateText, @"(\d{2})-(\d{2})-(\d{2})");
                        if (match.Success)
                        {
                            var year = int.Parse(match.Groups[3].Value);
                            // Convert 2-digit year to 4-digit (assume 2000s)
                            metadata.PublishYear = (2000 + year).ToString();
                            _logger.LogInformation("Extracted publish year from search result: {Year}", metadata.PublishYear);
                        }
                    }
                }

                // Extract language from languageLabel (e.g., "Language: English")
                if (string.IsNullOrWhiteSpace(metadata.Language))
                {
                    var languageNode = productItem.SelectSingleNode(".//li[contains(@class,'languageLabel')]");
                    if (languageNode != null)
                    {
                        var languageText = languageNode.InnerText?.Trim() ?? string.Empty;
                        // Remove "Language:" prefix
                        languageText = System.Text.RegularExpressions.Regex.Replace(languageText, @"^\s*Language:\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        metadata.Language = CleanLanguage(languageText.Trim());
                        _logger.LogInformation("Extracted language from search result: {Language}", metadata.Language);
                    }
                }

                // Extract subtitle from subtitle class (e.g., "Empyrean, Book 1")
                if (string.IsNullOrWhiteSpace(metadata.Subtitle))
                {
                    var subtitleNode = productItem.SelectSingleNode(".//li[contains(@class,'subtitle')]//span");
                    if (subtitleNode != null)
                    {
                        metadata.Subtitle = subtitleNode.InnerText?.Trim();
                        _logger.LogInformation("Extracted subtitle from search result: {Subtitle}", metadata.Subtitle);
                    }
                }

                _logger.LogInformation("Completed ExtractFromSearchResult for ASIN {Asin}", metadata.Asin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractFromSearchResult for ASIN {Asin}", metadata.Asin);
            }
        }

        /// <summary>
        /// Extract metadata from adbl-product-details component (Audible's primary metadata container).
        /// This component contains: title, image, summary, authors, narrators, series, release date, language, format, length, publisher, categories, tags.
        /// </summary>
        private void ExtractFromProductDetails(HtmlDocument doc, AudibleBookMetadata metadata)
        {
            var productDetails = doc.DocumentNode.SelectSingleNode("//adbl-product-details");
            if (productDetails == null)
            {
                _logger.LogInformation("adbl-product-details component not found in HTML for ASIN {Asin}", metadata.Asin);
                return;
            }
            
            // Log component structure to understand content
            var innerHtmlLength = productDetails.InnerHtml?.Length ?? 0;
            _logger.LogInformation("adbl-product-details found for ASIN {Asin}, inner HTML: {Length} chars", metadata.Asin, innerHtmlLength);

            // Title - look for h1 within the component (OVERRIDE any previous extraction to avoid "Audible" site logo)
            var titleNode = productDetails.SelectSingleNode(".//h1[@class='bc-heading']")
                         ?? productDetails.SelectSingleNode(".//h1")
                         ?? doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'bc-heading')]");
            if (titleNode != null)
            {
                var title = titleNode.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(title) && title != "Audible")
                {
                    metadata.Title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ").Trim();
                    _logger.LogInformation("Extracted title from component: {Title}", metadata.Title);
                }
            }

            // Image - look for product image (book cover), not site logo
            // Try meta og:image first (most reliable), then look for product-specific images
            var imgNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")
                       ?? productDetails.SelectSingleNode(".//img[contains(@src,'images/I/')]")
                       ?? doc.DocumentNode.SelectSingleNode("//img[contains(@src,'images/I/') and contains(@src,'_SL')]")
                       ?? productDetails.SelectSingleNode(".//img[contains(@class,'bc-image-inset-border')]")
                       ?? productDetails.SelectSingleNode(".//img");
            if (imgNode != null)
            {
                var imgUrl = imgNode.GetAttributeValue("content", null) ?? imgNode.GetAttributeValue("src", null);
                var cleanUrl = CleanImageUrl(imgUrl);
                if (!string.IsNullOrWhiteSpace(cleanUrl))
                {
                    metadata.ImageUrl = cleanUrl;
                    _logger.LogInformation("Extracted image URL from component: {Url}", cleanUrl);
                }
                else
                {
                    _logger.LogWarning("Found image but filtered as logo/nav: {Url}", imgUrl);
                }
            }
            else
            {
                _logger.LogWarning("No image element found in component or page");
            }

            // Authors - Extract from FULL PAGE after Playwright rendering, targeting product details area
            // Look for links to /author/ pages - the FIRST one is always the book's actual author
            var pageAuthorNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/author/')]");
            if (pageAuthorNodes != null)
            {
                var authors = pageAuthorNodes
                    .Select(n => NormalizeNameString(n.InnerText))
                    .Where(s => !string.IsNullOrWhiteSpace(s) && !IsAuthorNoise(s))
                    .Distinct()
                    .Take(1) // Take only the first author - it's always the book's author, rest are recommendations
                    .ToList();
                if (authors.Any())
                {
                    metadata.Authors = authors!;
                    _logger.LogInformation("Extracted {Count} author(s) from Playwright-rendered page: {Authors}", authors.Count, string.Join(", ", authors));
                }
                else
                {
                    _logger.LogWarning("Found {Count} author links but all filtered out as noise", pageAuthorNodes.Count);
                }
            }
            else
            {
                _logger.LogWarning("No author links (href='/author/') found in Playwright-rendered page");
            }

            // Summary (description) - Extract BEFORE narrators so we can parse narrator from description if needed
            // The rendered Audible page uses custom web components
            var summaryNode = productDetails.SelectSingleNode(".//adbl-text-block[@slot='summary']") 
                           ?? productDetails.SelectSingleNode(".//div[@slot='summary']")
                           ?? productDetails.SelectSingleNode(".//span[@class='bc-text bc-publisher-summary-text']") 
                           ?? doc.DocumentNode.SelectSingleNode("//adbl-text-block[@slot='summary']");
            
            _logger.LogInformation("Summary node search result: {Found}", summaryNode != null ? "Found" : "Not found");
            
            if (summaryNode != null && string.IsNullOrWhiteSpace(metadata.Description))
            {
                var summary = summaryNode.InnerText?.Trim();
                _logger.LogInformation("Summary text length: {Length}, starts with: {Start}", 
                    summary?.Length ?? 0, summary?.Substring(0, Math.Min(100, summary?.Length ?? 0)));
                    
                // Filter out UI elements like "Close", "Show more", etc.
                if (!string.IsNullOrWhiteSpace(summary) && summary.Length > 50 && 
                    !summary.Equals("Close", StringComparison.OrdinalIgnoreCase) &&
                    !summary.Equals("Show more", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.Description = System.Text.RegularExpressions.Regex.Replace(summary, @"\s+", " ").Trim();
                    _logger.LogInformation("Extracted description from adbl-product-details: {Length} chars", metadata.Description.Length);
                    
                    // Extract publisher from description - look for (P)YYYY Publisher pattern
                    if (string.IsNullOrWhiteSpace(metadata.Publisher))
                    {
                        var publisherMatch = System.Text.RegularExpressions.Regex.Match(metadata.Description, @"\(P\)\d{4}\s+([^\u00a9\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (publisherMatch.Success)
                        {
                            metadata.Publisher = publisherMatch.Groups[1].Value.Trim();
                            _logger.LogInformation("Extracted publisher from description: {Publisher}", metadata.Publisher);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Summary text too short or looks like UI element: {Text}", summary?.Substring(0, Math.Min(50, summary?.Length ?? 0)));
                }
            }

            // Narrators - Extract from FULL PAGE after Playwright rendering (AFTER description extraction)
            // AGGRESSIVE HTML LOGGING: Search for narrator-related content
            _logger.LogInformation("=== NARRATOR HTML STRUCTURE ANALYSIS START ===");
            
            // Log all text containing "narrat" (case-insensitive)
            var narratorTextNodes = doc.DocumentNode.SelectNodes("//*[contains(translate(text(), 'NARRATED', 'narrated'), 'narrat')]");
            if (narratorTextNodes != null && narratorTextNodes.Count > 0)
            {
                _logger.LogInformation("Found {Count} nodes containing 'narrat' text:", narratorTextNodes.Count);
                foreach (var node in narratorTextNodes.Take(10))
                {
                    var text = node.InnerText?.Trim();
                    var html = node.OuterHtml?.Substring(0, Math.Min(300, node.OuterHtml.Length));
                    _logger.LogInformation("  Node: {Name}, Text: {Text}, HTML: {Html}", node.Name, text?.Substring(0, Math.Min(100, text?.Length ?? 100)), html);
                }
            }
            
            // Log all <li> elements (narrator info often in list items)
            var allListItems = productDetails.SelectNodes(".//li") ?? doc.DocumentNode.SelectNodes("//li[not(ancestor::nav)]");
            if (allListItems != null && allListItems.Count > 0)
            {
                _logger.LogInformation("Found {Count} <li> elements in product area:", allListItems.Count);
                foreach (var li in allListItems.Take(20))
                {
                    var text = li.InnerText?.Trim();
                    var className = li.GetAttributeValue("class", "");
                    if (!string.IsNullOrWhiteSpace(text) && text.Length < 200)
                    {
                        _logger.LogInformation("  <li class='{Class}'>: {Text}", className, text);
                    }
                }
            }
            
            // Log all <span> elements with class containing common narrator patterns
            var narratorSpans = doc.DocumentNode.SelectNodes("//span[contains(@class,'contributor') or contains(@class,'author') or contains(@class,'narrator') or contains(@class,'by-line')]");
            if (narratorSpans != null && narratorSpans.Count > 0)
            {
                _logger.LogInformation("Found {Count} contributor/author/narrator span elements:", narratorSpans.Count);
                foreach (var span in narratorSpans.Take(15))
                {
                    var text = span.InnerText?.Trim();
                    var className = span.GetAttributeValue("class", "");
                    _logger.LogInformation("  <span class='{Class}'>: {Text}", className, text?.Substring(0, Math.Min(150, text?.Length ?? 0)));
                }
            }
            
            // Log all links (might contain narrator links we're missing)
            var allLinks = productDetails.SelectNodes(".//a") ?? doc.DocumentNode.SelectNodes("//a[not(ancestor::nav) and not(ancestor::footer)]");
            if (allLinks != null && allLinks.Count > 0)
            {
                _logger.LogInformation("Found {Count} <a> links in product area (showing first 30):", allLinks.Count);
                foreach (var link in allLinks.Take(30))
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = link.InnerText?.Trim();
                    var className = link.GetAttributeValue("class", "");
                    if (!string.IsNullOrWhiteSpace(text) && text.Length < 100)
                    {
                        _logger.LogInformation("  <a href='{Href}' class='{Class}'>: {Text}", href, className, text);
                    }
                }
            }
            
            _logger.LogInformation("=== NARRATOR HTML STRUCTURE ANALYSIS END ===");
            
            // First try to find narrator information using the bc-action-sheet-item structure
            // These are <li> elements that appear in action sheets/modals
            var allActionSheetItems = doc.DocumentNode.SelectNodes("//li[contains(@class,'bc-action-sheet-item') and contains(@class,'mosaic-action-sheet-item')]");
            
            if (allActionSheetItems != null && allActionSheetItems.Count > 0)
            {
                _logger.LogInformation("Found {Count} bc-action-sheet-item elements total", allActionSheetItems.Count);
                
                // The structure is: label <li> followed by data <li> elements
                // Find "Narrated by:" label
                var allLabels = doc.DocumentNode.SelectNodes("//li[contains(@class,'mosaic-action-sheet-label')]");
                if (allLabels != null)
                {
                    _logger.LogInformation("Found {Count} action sheet labels, checking for 'Narrated by:'", allLabels.Count);
                    foreach (var label in allLabels)
                    {
                        var labelText = label.InnerText?.Trim();
                        _logger.LogInformation("  Label text: '{Text}'", labelText);
                    }
                }
                
                // Try different approaches to find the narrator items
                var narratorItems = new List<string>();
                
                // Approach 1: All action sheet items after checking if text looks like a name
                foreach (var item in allActionSheetItems)
                {
                    var text = item.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && 
                        text.Length > 2 && 
                        text.Length < 100 &&  // Names shouldn't be super long
                        !text.Contains("By:", StringComparison.OrdinalIgnoreCase) &&
                        !text.Contains("Narrated", StringComparison.OrdinalIgnoreCase) &&
                        Char.IsUpper(text[0]))  // Names typically start with capital letters
                    {
                        _logger.LogInformation("  Potential narrator from action sheet: {Text}", text);
                        narratorItems.Add(text);
                    }
                }
                
                if (narratorItems.Count == 2 || narratorItems.Count == 3)
                {
                    // Filter out the author if present
                    if (metadata.Authors != null && metadata.Authors.Any())
                    {
                        narratorItems = narratorItems
                            .Where(n => !metadata.Authors.Contains(n, StringComparer.OrdinalIgnoreCase))
                            .ToList();
                        _logger.LogInformation("After filtering out authors, have {Count} narrators", narratorItems.Count);
                    }
                    
                    if (narratorItems.Any())
                    {
                        metadata.Narrators = narratorItems;
                        _logger.LogInformation("Extracted {Count} narrator(s) from action sheet items: {Narrators}", 
                            narratorItems.Count, string.Join(", ", narratorItems));
                    }
                }
                else if (narratorItems.Count > 0)
                {
                    _logger.LogInformation("Found {Count} potential narrators but count seems off, listing them anyway", narratorItems.Count);
                    metadata.Narrators = narratorItems;
                }
            }
            
            // If not found in component, try full page
            if (metadata.Narrators == null || !metadata.Narrators.Any())
            {
                // First try the bc-action-sheet-item structure (most reliable based on logs)
                var fullPageNarratorList = doc.DocumentNode.SelectNodes("//li[contains(@class,'mosaic-action-sheet-label') and contains(text(),'Narrated by:')]/following-sibling::li[contains(@class,'bc-action-sheet-item')]");
                
                if (fullPageNarratorList != null && fullPageNarratorList.Count > 0)
                {
                    var narratorsFromFullPage = fullPageNarratorList
                        .Select(n => n.InnerText?.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s) && 
                                   s.Length > 2 && 
                                   !s.Contains("By:", StringComparison.OrdinalIgnoreCase) &&
                                   !s.Contains("Narrated by:", StringComparison.OrdinalIgnoreCase))
                        .Distinct()
                        .ToList();
                        
                    if (narratorsFromFullPage.Any())
                    {
                        metadata.Narrators = narratorsFromFullPage.Where(n => n != null).Select(n => n!).ToList();
                        _logger.LogInformation("Extracted {Count} narrator(s) from full page action sheet: {Narrators}", 
                            narratorsFromFullPage.Count, string.Join(", ", narratorsFromFullPage));
                    }
                }
                
                // If still not found, try link-based patterns
                if (metadata.Narrators == null || !metadata.Narrators.Any())
                {
                    var pageNarratorNodes = doc.DocumentNode.SelectNodes(
                        "//a[contains(@href,'/narrator/')] | " +
                        "//span[contains(@class,'narratorLabel')]/following-sibling::span//a | " +
                        "//li[contains(text(),'Narrated by')]//a | " +
                        "//span[contains(text(),'Narrated by')]/following-sibling::a | " +
                        "//span[contains(@class,'narrator')]//a | " +
                        "//a[contains(@class,'narrator')] | " +
                        "//span[contains(text(),'Narrator')]/following-sibling::*//a"
                    );
                    
                    _logger.LogInformation("Narrator node search found {Count} potential nodes in full page", pageNarratorNodes?.Count ?? 0);
                    
                    if (pageNarratorNodes != null && pageNarratorNodes.Count > 0)
                    {
                        var narrators = pageNarratorNodes
                            .Select(n => n.InnerText?.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s) && 
                                       !s.Contains("Narrated by", StringComparison.OrdinalIgnoreCase) &&
                                       !s.Contains("View", StringComparison.OrdinalIgnoreCase) &&
                                       s.Length > 2)
                            .Distinct()
                            .Take(5) // Limit to first 5 narrators from product details area
                            .ToList();
                            
                        _logger.LogInformation("After filtering, extracted {Count} narrators: {Narrators}", 
                            narrators.Count, string.Join(", ", narrators));
                            
                        if (narrators.Any())
                        {
                            metadata.Narrators = narrators!;
                            _logger.LogInformation("Successfully extracted {Count} narrator(s) from page links", narrators.Count);
                        }
                        else
                        {
                            _logger.LogWarning("Found {Count} narrator links but all filtered out", pageNarratorNodes.Count);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No narrator link elements found in rendered HTML");
                    }
                }
            }
            
            // If no narrator links found, try parsing from description text as last resort
            if (metadata.Narrators == null || !metadata.Narrators.Any())
            {
                var descText = metadata.Description ?? "";
                _logger.LogInformation("Attempting to parse narrators from description text (length: {Length})", descText.Length);
                
                // Try multiple patterns:
                // 1. "read by Name1 and Name2"
                // 2. "Narrated by Name1, Name2"
                // 3. Just "read by Name"
                var narratorPatterns = new[]
                {
                    @"(?:read by|narrated by)\s+([^.!?]+?)(?:\s+and\s+([^.!?]+?))?(?:\.|Re-download|$)",
                    @"Narrated by:?\s*([^.!?\n]+)",
                };
                
                foreach (var pattern in narratorPatterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(descText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var narratorList = new List<string>();
                        
                        // Extract first narrator
                        if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                        {
                            var name1 = match.Groups[1].Value.Trim();
                            // Handle "Name1 and Name2" or "Name1, Name2"
                            if (name1.Contains(" and ") || name1.Contains(", "))
                            {
                                var names = name1.Split(new[] { " and ", ", " }, StringSplitOptions.RemoveEmptyEntries);
                                narratorList.AddRange(names.Select(n => n.Trim()));
                            }
                            else
                            {
                                narratorList.Add(name1);
                            }
                        }
                        
                        // Extract second narrator if captured separately
                        if (match.Groups.Count > 2 && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            narratorList.Add(match.Groups[2].Value.Trim());
                        }
                        
                        if (narratorList.Any())
                        {
                            metadata.Narrators = narratorList.Distinct().ToList();
                            _logger.LogInformation("Extracted {Count} narrator(s) from description text: {Narrators}", 
                                narratorList.Count, string.Join(", ", narratorList));
                            break;
                        }
                    }
                }
                
                if (metadata.Narrators == null || !metadata.Narrators.Any())
                {
                    _logger.LogWarning("No narrator links found and could not parse from description text");
                }
            }

            // Series information
            var seriesNode = productDetails.SelectSingleNode(".//li[contains(@class,'seriesLabel')] | .//span[contains(text(),'Series')]");
            if (seriesNode != null)
            {
                var seriesText = seriesNode.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(seriesText))
                {
                    // Extract series name and number (e.g., "Empyrean, Book 1")
                    var match = System.Text.RegularExpressions.Regex.Match(seriesText, @"(?:Series:?\s*)?(.*?)(?:,\s*Book\s+(\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var seriesName = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(seriesName) && seriesName != "Series")
                        {
                            metadata.Series = seriesName;
                        }
                        if (match.Groups.Count > 2 && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            metadata.SeriesNumber = match.Groups[2].Value;
                        }
                    }
                }
            }

            // Extract all detail rows (Release date, Language, Format, Length, Publisher, etc.)
            var detailRows = productDetails.SelectNodes(".//li[contains(@class,'bc-list-item')] | .//div[contains(@class,'bc-row')] | .//span[contains(@class,'bc-text')] | .//div[@class='bc-box']");
            if (detailRows != null && detailRows.Any())
            {
                _logger.LogInformation("Found {Count} detail rows in adbl-product-details", detailRows.Count);
                foreach (var row in detailRows)
                {
                    var rowText = row.InnerText?.Trim();
                    if (string.IsNullOrWhiteSpace(rowText)) continue;

                    _logger.LogDebug("Detail row text: {Text}", rowText.Length > 100 ? rowText.Substring(0, 100) + "..." : rowText);

                    // Release date
                    if (rowText.Contains("Release date:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(metadata.PublishYear))
                    {
                        var dateMatch = System.Text.RegularExpressions.Regex.Match(rowText, @"Release date:\s*([^\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (dateMatch.Success)
                        {
                            metadata.PublishYear = ExtractYear(dateMatch.Groups[1].Value);
                            _logger.LogInformation("Extracted publish year from detail row: {Year}", metadata.PublishYear);
                        }
                    }

                    // Language
                    if (rowText.Contains("Language:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(metadata.Language))
                    {
                        var langMatch = System.Text.RegularExpressions.Regex.Match(rowText, @"Language:\s*([^\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (langMatch.Success)
                        {
                            metadata.Language = CleanLanguage(langMatch.Groups[1].Value.Trim());
                            _logger.LogInformation("Extracted language from detail row: {Language}", metadata.Language);
                        }
                    }

                    // Format/Version
                    if ((rowText.Contains("Version:", StringComparison.OrdinalIgnoreCase) || rowText.Contains("Format:", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(metadata.Version))
                    {
                        if (rowText.Contains("Unabridged", StringComparison.OrdinalIgnoreCase))
                        {
                            metadata.Version = "Unabridged";
                            metadata.Abridged = false;
                            _logger.LogInformation("Extracted version from detail row: Unabridged");
                        }
                        else if (rowText.Contains("Abridged", StringComparison.OrdinalIgnoreCase))
                        {
                            metadata.Version = "Abridged";
                            metadata.Abridged = true;
                            _logger.LogInformation("Extracted version from detail row: Abridged");
                        }
                    }

                    // Length (runtime)
                    if (rowText.Contains("Length:", StringComparison.OrdinalIgnoreCase) && metadata.Runtime == null)
                    {
                        var hrsMatch = System.Text.RegularExpressions.Regex.Match(rowText, @"(\d+)\s*hrs?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        var minsMatch = System.Text.RegularExpressions.Regex.Match(rowText, @"(\d+)\s*mins?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        int total = 0;
                        if (hrsMatch.Success) total += int.Parse(hrsMatch.Groups[1].Value) * 60;
                        if (minsMatch.Success) total += int.Parse(minsMatch.Groups[1].Value);
                        if (total > 0)
                        {
                            metadata.Runtime = total;
                            _logger.LogInformation("Extracted runtime from detail row: {Runtime} minutes", total);
                        }
                    }

                    // Publisher
                    if (rowText.Contains("Publisher:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(metadata.Publisher))
                    {
                        var pubMatch = System.Text.RegularExpressions.Regex.Match(rowText, @"Publisher:\s*([^\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (pubMatch.Success)
                        {
                            metadata.Publisher = pubMatch.Groups[1].Value.Trim();
                            _logger.LogInformation("Extracted publisher from detail row: {Publisher}", metadata.Publisher);
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("No detail rows found in adbl-product-details component");
                
                // Fallback: Try to extract from full page text
                var fullPageText = doc.DocumentNode.InnerText;
                
                // Try to find runtime in page text
                if (metadata.Runtime == null)
                {
                    var runtimeMatch = System.Text.RegularExpressions.Regex.Match(fullPageText, @"Length:\s*(\d+)\s*hours?\s+and\s+(\d+)\s*minutes?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (runtimeMatch.Success)
                    {
                        int hrs = int.Parse(runtimeMatch.Groups[1].Value);
                        int mins = int.Parse(runtimeMatch.Groups[2].Value);
                        metadata.Runtime = (hrs * 60) + mins;
                        _logger.LogInformation("Extracted runtime from full page text: {Runtime} minutes", metadata.Runtime);
                    }
                }
                
                // Try to find version in page text
                if (string.IsNullOrWhiteSpace(metadata.Version))
                {
                    if (fullPageText.Contains("Unabridged", StringComparison.OrdinalIgnoreCase))
                    {
                        metadata.Version = "Unabridged";
                        metadata.Abridged = false;
                        _logger.LogInformation("Extracted version from full page text: Unabridged");
                    }
                    else if (fullPageText.Contains("Abridged", StringComparison.OrdinalIgnoreCase))
                    {
                        metadata.Version = "Abridged";
                        metadata.Abridged = true;
                        _logger.LogInformation("Extracted version from full page text: Abridged");
                    }
                }
            }

            // Categories (genres) and Tags from chips
            var categoryNodes = productDetails.SelectNodes(".//span[contains(@class,'bc-chip-text')] | .//a[contains(@class,'bc-chip')]");
            if (categoryNodes != null)
            {
                var categories = categoryNodes
                    .Select(n => n.InnerText?.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();
                if (categories.Any())
                {
                    if (metadata.Genres == null || !metadata.Genres.Any())
                    {
                        metadata.Genres = categories!;
                    }
                    if (metadata.Tags == null || !metadata.Tags.Any())
                    {
                        metadata.Tags = categories!;
                    }
                }
            }

            // Extract from adbl-product-metadata component for Series, Runtime, Version, etc.
            ExtractFromProductMetadata(doc, metadata);
        }

        /// <summary>
        /// Extract metadata from adbl-product-metadata component (Series, Runtime, Version, etc.)
        /// This component contains structured fields like: Series, Release date, Language, Format, Length, Publisher, Categories
        /// </summary>
        private void ExtractFromProductMetadata(HtmlDocument doc, AudibleBookMetadata metadata)
        {
            try
            {
                _logger.LogInformation("Starting ExtractFromProductMetadata for adbl-product-metadata component");
                
                // Log all custom elements starting with "adbl-"
                var allAdblElements = doc.DocumentNode.SelectNodes("//*[starts-with(local-name(), 'adbl-')]");
                if (allAdblElements != null)
                {
                    _logger.LogInformation("Found {Count} adbl-* custom elements:", allAdblElements.Count);
                    foreach (var elem in allAdblElements.Take(20))
                    {
                        _logger.LogInformation("  Element: {Name}, Text length: {Length}", 
                            elem.Name, elem.InnerText?.Length ?? 0);
                    }
                }
                else
                {
                    _logger.LogWarning("No adbl-* custom elements found in document");
                }
                
                var productMetadata = doc.DocumentNode.SelectSingleNode("//adbl-product-metadata");
                if (productMetadata == null)
                {
                    _logger.LogWarning("No adbl-product-metadata component found");
                    
                    // Try alternate selectors
                    productMetadata = doc.DocumentNode.SelectSingleNode("//*[local-name()='adbl-product-metadata']");
                    if (productMetadata != null)
                    {
                        _logger.LogInformation("Found adbl-product-metadata using local-name() selector");
                    }
                    else
                    {
                        // Log a snippet of the HTML to see what's there
                        var bodyHtml = doc.DocumentNode.SelectSingleNode("//body")?.InnerHtml;
                        if (bodyHtml != null && bodyHtml.Length > 1000)
                        {
                            _logger.LogInformation("Body HTML snippet (first 2000 chars): {Html}", 
                                bodyHtml.Substring(0, Math.Min(2000, bodyHtml.Length)));
                        }
                        return;
                    }
                }

                _logger.LogInformation("Found adbl-product-metadata component");

                // Try to extract from structured list items (same format as search results but within the component)
                // Look for list items with specific classes: runtimeLabel, seriesLabel, releaseDateLabel, languageLabel
                
                // Extract runtime from runtimeLabel
                if (metadata.Runtime == null)
                {
                    var runtimeNode = productMetadata.SelectSingleNode(".//li[contains(@class,'runtimeLabel')] | .//span[contains(text(),'Length:')] | .//div[contains(text(),'Length:')]");
                    if (runtimeNode != null)
                    {
                        var runtimeText = runtimeNode.InnerText?.Trim() ?? string.Empty;
                        _logger.LogInformation("Found runtime text: {Text}", runtimeText);
                        var match = System.Text.RegularExpressions.Regex.Match(runtimeText, 
                            @"(\d+)\s*hrs?\s+(?:and\s+)?(\d+)\s*mins?", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            int hours = int.Parse(match.Groups[1].Value);
                            int minutes = int.Parse(match.Groups[2].Value);
                            metadata.Runtime = hours * 60 + minutes;
                            _logger.LogInformation("Extracted runtime from product metadata: {Runtime} minutes", metadata.Runtime);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No runtime node found in product metadata");
                    }
                }

                // Extract series from seriesLabel
                if (string.IsNullOrWhiteSpace(metadata.Series))
                {
                    var seriesNode = productMetadata.SelectSingleNode(".//li[contains(@class,'seriesLabel')] | .//span[contains(text(),'Series:')] | .//div[contains(text(),'Series:')]");
                    if (seriesNode != null)
                    {
                        var seriesText = seriesNode.InnerText?.Trim() ?? string.Empty;
                        _logger.LogInformation("Found series text: {Text}", seriesText);
                        seriesText = System.Text.RegularExpressions.Regex.Replace(seriesText, @"^\s*Series:\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        var match = System.Text.RegularExpressions.Regex.Match(seriesText, @"^(.+?),\s*Book\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            metadata.Series = match.Groups[1].Value.Trim();
                            metadata.SeriesNumber = match.Groups[2].Value;
                            _logger.LogInformation("Extracted series from product metadata: {Series}, Book {Number}", 
                                metadata.Series, metadata.SeriesNumber);
                        }
                    }
                }

                // Extract release date from releaseDateLabel
                if (string.IsNullOrWhiteSpace(metadata.PublishYear))
                {
                    var releaseDateNode = productMetadata.SelectSingleNode(".//li[contains(@class,'releaseDateLabel')] | .//span[contains(text(),'Release date:')] | .//div[contains(text(),'Release date:')]");
                    if (releaseDateNode != null)
                    {
                        var dateText = releaseDateNode.InnerText?.Trim() ?? string.Empty;
                        _logger.LogInformation("Found release date text: {Text}", dateText);
                        var match = System.Text.RegularExpressions.Regex.Match(dateText, @"(\d{2})-(\d{2})-(\d{2})");
                        if (match.Success)
                        {
                            var year = int.Parse(match.Groups[3].Value);
                            metadata.PublishYear = (2000 + year).ToString();
                            _logger.LogInformation("Extracted publish year from product metadata: {Year}", metadata.PublishYear);
                        }
                    }
                }

                // Now try the JSON approach as fallback
                // The component uses Shadow DOM with JSON data in a script tag
                // Extract the JSON data from <script type="application/json">
                var scriptNode = productMetadata.SelectSingleNode(".//script[@type='application/json']");
                if (scriptNode == null)
                {
                    _logger.LogWarning("No JSON script found in adbl-product-metadata component");
                    return;
                }

                var jsonText = scriptNode.InnerText?.Trim() ?? string.Empty;
                _logger.LogInformation("Found JSON data in adbl-product-metadata: {Length} chars", jsonText.Length);
                
                if (string.IsNullOrEmpty(jsonText))
                {
                    _logger.LogWarning("JSON script is empty");
                    return;
                }

                // Parse the JSON
                System.Text.Json.JsonDocument? jsonDoc = null;
                try
                {
                    jsonDoc = System.Text.Json.JsonDocument.Parse(jsonText);
                    _logger.LogInformation("Successfully parsed JSON from adbl-product-metadata");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing JSON from adbl-product-metadata");
                    return;
                }

                using (jsonDoc)
                {
                    var root = jsonDoc.RootElement;
                    
                    // Log all property names to understand JSON structure
                    _logger.LogInformation("JSON properties in adbl-product-metadata: {Properties}", 
                        string.Join(", ", root.EnumerateObject().Select(p => p.Name)));

                    // The JSON only contains authors/narrators/rating, not the metadata fields
                    // The visible metadata is rendered by the component but not in the JSON
                    // We need to extract from the RENDERED text instead
                }

                // Extract from rendered/visible text on the page (Playwright renders the full page)
                var pageText = doc.DocumentNode.InnerText;
                
                // Log a sample of the rendered text to understand its structure
                var sampleLength = Math.Min(5000, pageText.Length);
                var sampleText = pageText.Substring(0, sampleLength);
                // Look for the product metadata section
                var metadataIndex = sampleText.IndexOf("Release date", StringComparison.OrdinalIgnoreCase);
                if (metadataIndex > 0)
                {
                    var snippet = sampleText.Substring(Math.Max(0, metadataIndex - 100), Math.Min(1000, sampleText.Length - metadataIndex + 100));
                    _logger.LogInformation("Page text around metadata section (1000 chars): {Snippet}", snippet);
                }
                else
                {
                    _logger.LogWarning("Could not find 'Release date' in page text. Sample (first 1000 chars): {Sample}", 
                        sampleText.Substring(0, Math.Min(1000, sampleText.Length)));
                }
                
                // Extract Series (e.g., "Series Book 1,  The Empyrean" or "Series\nBook 1, \nThe Empyrean")
                var seriesMatch = System.Text.RegularExpressions.Regex.Match(pageText,
                    @"Series[\s\n]+Book\s+(\d+)[,\s\n]+([^\n\r]+?)(?=[\s\n]*(?:Release date|Language|Format|Length|Publisher|Categories|$))",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                if (seriesMatch.Success)
                {
                    metadata.SeriesNumber = seriesMatch.Groups[1].Value.Trim();
                    metadata.Series = seriesMatch.Groups[2].Value.Trim();
                    _logger.LogInformation("Extracted series from rendered text: {Series}, Book {Number}",
                        metadata.Series, metadata.SeriesNumber);
                }

                // Extract Release date (e.g., "Release date\n05-02-23" or "Release date 05-02-23")
                var releaseDateMatch = System.Text.RegularExpressions.Regex.Match(pageText,
                    @"Release\s+date[\s\n]+(\d{2})-(\d{2})-(\d{2})",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (releaseDateMatch.Success)
                {
                    var year = int.Parse(releaseDateMatch.Groups[3].Value);
                    metadata.PublishYear = (2000 + year).ToString();
                    _logger.LogInformation("Extracted publish year from rendered text: {Year}", metadata.PublishYear);
                }

                // Extract Language (e.g., "Language\nEnglish" or "Language English")
                if (string.IsNullOrWhiteSpace(metadata.Language))
                {
                    var languageMatch = System.Text.RegularExpressions.Regex.Match(pageText,
                        @"Language[\s\n]+([A-Za-z]+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (languageMatch.Success)
                    {
                        metadata.Language = CleanLanguage(languageMatch.Groups[1].Value.Trim());
                        _logger.LogInformation("Extracted language from rendered text: {Language}", metadata.Language);
                    }
                }

                // Extract Format (e.g., "Format\nUnabridged Audiobook" or "Format Unabridged Audiobook")
                if (string.IsNullOrWhiteSpace(metadata.Version))
                {
                    var formatMatch = System.Text.RegularExpressions.Regex.Match(pageText,
                        @"Format[\s\n]+(Unabridged|Abridged)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (formatMatch.Success)
                    {
                        var format = formatMatch.Groups[1].Value;
                        if (format.Contains("Unabridged", StringComparison.OrdinalIgnoreCase))
                        {
                            metadata.Version = "Unabridged";
                            metadata.Abridged = false;
                            _logger.LogInformation("Extracted version from rendered text: Unabridged");
                        }
                        else if (format.Contains("Abridged", StringComparison.OrdinalIgnoreCase))
                        {
                            metadata.Version = "Abridged";
                            metadata.Abridged = true;
                            _logger.LogInformation("Extracted version from rendered text: Abridged");
                        }
                    }
                }

                // Extract Length (e.g., "Length\n21 hrs and 22 mins" or "Length 21 hrs and 22 mins")
                if (metadata.Runtime == null)
                {
                    var lengthMatch = System.Text.RegularExpressions.Regex.Match(pageText,
                        @"Length[\s\n]+(\d+)\s*hrs?\s+(?:and\s+)?(\d+)\s*mins?",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (lengthMatch.Success)
                    {
                        int hours = int.Parse(lengthMatch.Groups[1].Value);
                        int minutes = int.Parse(lengthMatch.Groups[2].Value);
                        metadata.Runtime = hours * 60 + minutes;
                        _logger.LogInformation("Extracted runtime from rendered text: {Runtime} minutes ({Hours}h {Minutes}m)",
                            metadata.Runtime, hours, minutes);
                    }
                }

                // Extract Publisher (e.g., "Publisher\nRecorded Books" or "Publisher Recorded Books")
                if (string.IsNullOrWhiteSpace(metadata.Publisher))
                {
                    var publisherMatch = System.Text.RegularExpressions.Regex.Match(pageText,
                        @"Publisher[\s\n]+([^\n\r]+?)(?=[\s\n]*(?:Categories|ASIN|$))",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (publisherMatch.Success)
                    {
                        metadata.Publisher = publisherMatch.Groups[1].Value.Trim();
                        _logger.LogInformation("Extracted publisher from rendered text: {Publisher}", metadata.Publisher);
                    }
                }

                _logger.LogInformation("Completed ExtractFromProductMetadata");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractFromProductMetadata");
            }
        }

        /// <summary>
        /// Clean language field by removing currency codes and normalizing.
        /// </summary>
        private string CleanLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return language;
            
            // Remove currency codes (e.g., "English - USD" -> "English")
            var cleaned = System.Text.RegularExpressions.Regex.Replace(language, @"\s*-\s*(USD|EUR|GBP|CAD|AUD|INR)\s*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove other common noise
            cleaned = cleaned.Replace("Language:", "").Trim();
            
            return cleaned;
        }

        /// <summary>
        /// Check if author string is navigation noise or invalid.
        /// </summary>
        private bool IsAuthorNoise(string? author)
        {
            if (string.IsNullOrWhiteSpace(author)) return true;
            var a = author.Trim();
            if (a.Length < 2) return true;
            
            // Filter out common Amazon/Audible navigation elements
            var noisePhrases = new[] { 
                "Shop By", "Shop by", "Authors", "By:", "Sort by", 
                "Written by", "See all", "Browse", "More by",
                "Visit Amazon", "Visit", "Learn more"
            };
            
            foreach (var noise in noisePhrases)
            {
                if (a.Contains(noise, StringComparison.OrdinalIgnoreCase)) return true;
            }
            
            return false;
        }

        /// <summary>
        /// Parse series information from title format like "Fourth Wing: Empyrean, Book 1"
        /// Updates metadata Title, Series, and SeriesNumber properties.
        /// </summary>
        private void ParseSeriesFromTitle(AudibleBookMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata.Title)) return;

            var title = metadata.Title;
            
            // Pattern: "Title: Series, Book #" or "Title: Series Book #"
            // Examples: "Fourth Wing: Empyrean, Book 1", "The Hobbit: Middle-earth, Book 1"
            var match = System.Text.RegularExpressions.Regex.Match(title,
                @"^(.+?):\s*([^,]+),?\s+Book\s+(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                metadata.Title = match.Groups[1].Value.Trim();
                metadata.Series = match.Groups[2].Value.Trim();
                metadata.SeriesNumber = match.Groups[3].Value.Trim();
                _logger.LogInformation("Parsed series from title: Title='{Title}', Series='{Series}', Number='{Number}'",
                    metadata.Title, metadata.Series, metadata.SeriesNumber);
                return;
            }

            // Pattern: "Title (Series Book #)" or "Title (Series, Book #)"
            // Examples: "Fourth Wing (Empyrean Book 1)", "The Name of the Wind (Kingkiller Chronicle, Book 1)"
            match = System.Text.RegularExpressions.Regex.Match(title,
                @"^(.+?)\s*\(([^,\)]+),?\s+Book\s+(\d+)\)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                metadata.Title = match.Groups[1].Value.Trim();
                metadata.Series = match.Groups[2].Value.Trim();
                metadata.SeriesNumber = match.Groups[3].Value.Trim();
                _logger.LogInformation("Parsed series from title: Title='{Title}', Series='{Series}', Number='{Number}'",
                    metadata.Title, metadata.Series, metadata.SeriesNumber);
                return;
            }

            // Pattern: "Title, Book # of Series" or "Title: Book # of Series"
            // Examples: "Fourth Wing, Book 1 of Empyrean", "Fourth Wing: Book 1 of The Empyrean"
            match = System.Text.RegularExpressions.Regex.Match(title,
                @"^(.+?)[,:]\s+Book\s+(\d+)\s+of\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                metadata.Title = match.Groups[1].Value.Trim();
                metadata.SeriesNumber = match.Groups[2].Value.Trim();
                metadata.Series = match.Groups[3].Value.Trim();
                _logger.LogInformation("Parsed series from title: Title='{Title}', Series='{Series}', Number='{Number}'",
                    metadata.Title, metadata.Series, metadata.SeriesNumber);
                return;
            }
        }
    }

    public interface IAudibleMetadataService
    {
        Task<AudibleBookMetadata> ScrapeAudibleMetadataAsync(string asin);
        Task<List<AudibleBookMetadata>> PrefetchAsync(List<string> asins);
    }
}