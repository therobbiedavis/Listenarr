using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Listenarr.Api.Services
{
    public class AudibleSearchResult
    {
        public string? Asin { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Narrator { get; set; }
        public string? ImageUrl { get; set; }
        public string? Duration { get; set; }
        public string? Price { get; set; }
        public string? ProductUrl { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        public string? Subtitle { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Language { get; set; }
    }

    public class AudibleSearchService : IAudibleSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AudibleSearchService> _logger;
        private static readonly Regex AsinRegex = new(@"/pd/[^/]+/([A-Z0-9]{10})", RegexOptions.Compiled);
        private static readonly Regex AsinFromUrlRegex = new(@"B0[A-Z0-9]{8}", RegexOptions.Compiled);
    // New: detect navigation/header noise and generic site labels like 'Audible'
    private static readonly string[] HeaderNoisePhrases = new[] { "English - USD", "Language", "Currency", "Sign in", "Account & Lists", "Audible", "Audible.com" };

        public AudibleSearchService(HttpClient httpClient, ILogger<AudibleSearchService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<AudibleSearchResult>> SearchAudiobooksAsync(string query)
        {
            var results = new List<AudibleSearchResult>();

            try
            {
                // Format the search query for Audible
                var searchQuery = query.Replace(" ", "+").ToLower();
                var searchUrl = $"https://www.audible.com/search?keywords={Uri.EscapeDataString(searchQuery)}";
                
                _logger.LogInformation("Searching Audible: {SearchUrl}", searchUrl);

                var html = await GetHtmlAsync(searchUrl);
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Empty HTML response from Audible search");
                    return results;
                }

                // Parse search results
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Debug: Log HTML length and check for bot detection
                _logger.LogInformation("Audible HTML response length: {Length} characters", html.Length);
                if (html.Contains("Robot Check") || html.Contains("automated traffic"))
                {
                    _logger.LogWarning("Audible bot detection triggered");
                    return results;
                }

                // Find search result items using modern Audible selectors
                var resultNodes = doc.DocumentNode.SelectNodes(
                    "//li[contains(@class,'productListItem')] | " +
                    "//div[contains(@class,'adbl-impression-container')] | " +
                    "//div[contains(@class,'bc-list-item')] | " +
                    "//span[contains(@class,'bc-list-item')] | " +
                    "//li[contains(@class,'bc-list-item')] | " +
                    "//div[contains(@class,'product-list-item')] | " +
                    "//div[contains(@class,'product-card')] | " +
                    "//div[contains(@class,'product-tile')] | " +
                    "//article | " +
                    "//li[contains(@class,'result')] | " +
                    "//div[contains(@class,'search-result')] | " +
                    "//div[.//a[contains(@href,'/pd/')]] | " +
                    "//li[.//a[contains(@href,'/pd/')]] | " +
                    "//div[contains(@data-asin,'')]"
                );

                _logger.LogInformation("Found {Count} potential result nodes using XPath selector", resultNodes?.Count ?? 0);

                if (resultNodes != null)
                {
                    foreach (var node in resultNodes.Take(20)) // Limit to first 20 results
                    {
                        var result = ExtractAudibleSearchResult(node);
                        if (result != null && !string.IsNullOrEmpty(result.Asin))
                        {
                            // Clean noisy titles
                            if (IsHeaderNoise(result.Title))
                            {
                                var fetched = await TryFetchProductTitle(result.ProductUrl, result.Asin!);
                                if (!string.IsNullOrWhiteSpace(fetched)) result.Title = fetched;
                            }
                            if (!IsHeaderNoise(result.Title))
                            {
                                results.Add(result);
                                _logger.LogInformation("Extracted Audible result: {Title} (ASIN: {Asin})", result.Title, result.Asin);
                            }
                        }
                    }
                }

                // If no results with first selector, try broader search
                if (!results.Any())
                {
                    _logger.LogInformation("No results with primary selectors, trying broader search");
                    var allLinks = doc.DocumentNode.SelectNodes("//a[contains(@href,'/pd/')]");
                    
                    if (allLinks != null)
                    {
                        foreach (var link in allLinks.Take(10))
                        {
                            var result = ExtractFromProductLink(link);
                            if (result != null && !string.IsNullOrEmpty(result.Asin))
                            {
                                if (IsHeaderNoise(result.Title))
                                {
                                    var fetched = await TryFetchProductTitle(result.ProductUrl, result.Asin!);
                                    if (!string.IsNullOrWhiteSpace(fetched)) result.Title = fetched;
                                }
                                if (!IsHeaderNoise(result.Title))
                                {
                                    results.Add(result);
                                    _logger.LogInformation("Extracted from link: {Title} (ASIN: {Asin})", result.Title, result.Asin);
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Found {Count} Audible search results for query: {Query}", results.Count, query);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Audible for query: {Query}", query);
                return results;
            }
        }

        private bool IsHeaderNoise(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;
            var t = title.Trim();
            if (t.Length < 3) return true;
            // If the title is exactly one of the header phrases or is very short/one-word generic like 'Audible', treat as noise
            if (HeaderNoisePhrases.Any(p => string.Equals(t, p, StringComparison.OrdinalIgnoreCase))) return true;
            if (HeaderNoisePhrases.Any(p => t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0 && t.Length < 40)) return true;
            // repeated blocks like header duplicated
            if (t.Count(c => c == '\n') > 3 && t.Length > 120) return true;
            return false;
        }

        private async Task<string?> TryFetchProductTitle(string? productUrl, string asin)
        {
            if (string.IsNullOrEmpty(productUrl)) return null;
            try
            {
                var html = await GetHtmlAsync(productUrl);
                if (string.IsNullOrEmpty(html)) return null;
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var titleNode = doc.DocumentNode.SelectSingleNode("//h1 | //h1//span | //div[contains(@class,'title')]//h1 | //h1[contains(@class,'bc-heading')]" );
                var title = titleNode?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    _logger.LogInformation("Fetched Audible product page title for {Asin}: {Title}", asin, title);
                }
                return title;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch Audible product page title for {Asin}", asin);
                return null;
            }
        }

        private AudibleSearchResult? ExtractAudibleSearchResult(HtmlNode node)
        {
            try
            {
                // Extract product link and ASIN
                var productLink = node.SelectSingleNode(".//a[contains(@href,'/pd/')]")?.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(productLink)) return null;

                // Make sure it's a full URL
                if (productLink.StartsWith("/"))
                {
                    productLink = "https://www.audible.com" + productLink;
                }

                var asinMatch = AsinRegex.Match(productLink);
                if (!asinMatch.Success)
                {
                    // Try alternative ASIN extraction
                    var asinAltMatch = AsinFromUrlRegex.Match(productLink);
                    if (!asinAltMatch.Success) return null;
                    
                    var asin = asinAltMatch.Value;
                    return new AudibleSearchResult { Asin = asin, ProductUrl = productLink };
                }

                var extractedAsin = asinMatch.Groups[1].Value;

                // Extract title
                var titleNode = node.SelectSingleNode(
                    ".//h3[contains(@class,'bc-heading')] | " +
                    ".//h3//a | " +
                    ".//span[contains(@class,'bc-text')] | " +
                    ".//a[contains(@href,'/pd/')]"
                );
                var title = titleNode?.InnerText?.Trim();

                // Extract author
                var authorNode = node.SelectSingleNode(
                    ".//li[contains(@class,'authorLabel')] | " +
                    ".//span[contains(@class,'authorLabel')] | " +
                    ".//a[contains(@class,'author')] | " +
                    ".//span[contains(text(),'By:')] | " +
                    ".//span[contains(text(),'Written by')]"
                );
                var author = authorNode?.InnerText?.Replace("By:", "").Replace("Written by", "").Trim();

                // Extract narrator
                var narratorNode = node.SelectSingleNode(
                    ".//li[contains(@class,'narratorLabel')] | " +
                    ".//span[contains(@class,'narratorLabel')] | " +
                    ".//span[contains(text(),'Narrated by')] | " +
                    ".//span[contains(text(),'Narrator')]"
                );
                var narrator = narratorNode?.InnerText?.Replace("Narrated by:", "").Replace("Narrator:", "").Trim();

                // Extract image
                var imageNode = node.SelectSingleNode(".//img");
                var imageUrl = imageNode?.GetAttributeValue("src", "") ?? imageNode?.GetAttributeValue("data-lazy", "");

                // Extract duration/runtime from runtimeLabel
                var durationNode = node.SelectSingleNode(
                    ".//li[contains(@class,'runtimeLabel')] | " +
                    ".//span[contains(@class,'runtimeLabel')] | " +
                    ".//span[contains(text(),'Length')] | " +
                    ".//li[contains(text(),'hrs')] | " +
                    ".//span[contains(text(),'hr')]"
                );
                var duration = durationNode?.InnerText?.Trim();

                // Extract series from seriesLabel (e.g., "Series: The Empyrean, Book 1")
                var seriesNode = node.SelectSingleNode(
                    ".//li[contains(@class,'seriesLabel')] | " +
                    ".//span[contains(@class,'seriesLabel')]"
                );
                string? series = null;
                string? seriesNumber = null;
                if (seriesNode != null)
                {
                    var seriesText = seriesNode.InnerText?.Trim() ?? "";
                    // Remove "Series:" prefix
                    seriesText = Regex.Replace(seriesText, @"^\s*Series:\s*", "", RegexOptions.IgnoreCase);
                    // Parse "SeriesName, Book N" format
                    var seriesMatch = Regex.Match(seriesText, @"^(.+?),\s*Book\s+(\d+)", RegexOptions.IgnoreCase);
                    if (seriesMatch.Success)
                    {
                        series = seriesMatch.Groups[1].Value.Trim();
                        seriesNumber = seriesMatch.Groups[2].Value;
                    }
                    else
                    {
                        series = seriesText;
                    }
                }

                // Extract subtitle (e.g., "Empyrean, Book 1")
                var subtitleNode = node.SelectSingleNode(
                    ".//li[contains(@class,'subtitle')] | " +
                    ".//span[contains(@class,'subtitle')]"
                );
                var subtitle = subtitleNode?.InnerText?.Trim();

                // Extract release date from releaseDateLabel
                var releaseDateNode = node.SelectSingleNode(
                    ".//li[contains(@class,'releaseDateLabel')] | " +
                    ".//span[contains(@class,'releaseDateLabel')]"
                );
                var releaseDate = releaseDateNode?.InnerText?.Trim();

                // Extract language from languageLabel
                var languageNode = node.SelectSingleNode(
                    ".//li[contains(@class,'languageLabel')] | " +
                    ".//span[contains(@class,'languageLabel')]"
                );
                var language = languageNode?.InnerText?.Replace("Language:", "").Trim();

                // Extract price
                var priceNode = node.SelectSingleNode(
                    ".//span[contains(@class,'price')] | " +
                    ".//span[contains(text(),'$')] | " +
                    ".//p[contains(@class,'price')]"
                );
                var price = priceNode?.InnerText?.Trim();

                return new AudibleSearchResult
                {
                    Asin = extractedAsin,
                    Title = title,
                    Author = author,
                    Narrator = narrator,
                    ImageUrl = imageUrl,
                    Duration = duration,
                    Price = price,
                    ProductUrl = productLink,
                    Series = series,
                    SeriesNumber = seriesNumber,
                    Subtitle = subtitle,
                    ReleaseDate = releaseDate,
                    Language = language
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting Audible search result from node");
                return null;
            }
        }

        private AudibleSearchResult? ExtractFromProductLink(HtmlNode linkNode)
        {
            try
            {
                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) return null;

                if (href.StartsWith("/"))
                {
                    href = "https://www.audible.com" + href;
                }

                var asinMatch = AsinRegex.Match(href);
                if (!asinMatch.Success)
                {
                    var asinAltMatch = AsinFromUrlRegex.Match(href);
                    if (!asinAltMatch.Success) return null;
                    
                    return new AudibleSearchResult 
                    { 
                        Asin = asinAltMatch.Value,
                        Title = linkNode.InnerText?.Trim(),
                        ProductUrl = href
                    };
                }

                return new AudibleSearchResult
                {
                    Asin = asinMatch.Groups[1].Value,
                    Title = linkNode.InnerText?.Trim(),
                    ProductUrl = href
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting from product link");
                return null;
            }
        }

        private async Task<string?> GetHtmlAsync(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                // Add realistic headers to avoid bot detection
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("Connection", "keep-alive");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", "none");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching HTML from Audible URL: {Url}", url);
                return null;
            }
        }
    }
}