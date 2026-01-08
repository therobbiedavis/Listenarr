using System.Text.RegularExpressions;
using System.Net;
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
        public string? Publisher { get; set; }
    }

    public class AudibleSearchService : IAudibleSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AudibleSearchService> _logger;
        private readonly IHttpClientFactory? _httpClientFactory;
        private static readonly Regex AsinRegex = new(@"/pd/[^/]+/([A-Z0-9]{10})", RegexOptions.Compiled);
        private static readonly Regex AsinFromUrlRegex = new(@"B0[A-Z0-9]{8}", RegexOptions.Compiled);
        // New: detect navigation/header noise and generic site labels like 'Audible'
        private static readonly string[] HeaderNoisePhrases = new[] {
        "English - USD", "Language", "Currency", "Sign in", "Account & Lists", "Audible", "Audible.com",
        "No results", "Suggested Searches", "No results found", "Try again", "Browse categories",
        "Customer Service", "Help", "Search", "Menu"
    };

        // Detect common locale/geo redirect messages (examples include German audible.de redirect text)
        private static readonly string[] RedirectNoisePhrases = new[]
        {
        // English
        "have redirected you",
        "we have redirected",
        // German
        "aufgrund deines standorts haben wir dich zu",
        "haben wir dich zu audible.de weitergeleitet",
        // French
        "nous vous avons redirigé",
        // Spanish
        "te hemos redirigido",
        // Italian
        "ti abbiamo reindirizzato",
        // generic
        "redirected to",
        "you to audible",
    };

        private readonly IConfigurationService _configurationService;

        public AudibleSearchService(HttpClient httpClient, ILogger<AudibleSearchService> logger, IConfigurationService configurationService, IHttpClientFactory? httpClientFactory = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configurationService = configurationService;
        }

        public async Task<List<AudibleSearchResult>> SearchAudiobooksAsync(string query, CancellationToken ct = default)
        {
            var results = new List<AudibleSearchResult>();

            try
            {
                // Format the search query for Audible
                var searchQuery = query.Replace(" ", "+").ToLower();
                var searchUrl = $"https://www.audible.com/search?keywords={Uri.EscapeDataString(searchQuery)}";

                _logger.LogInformation("Searching Audible: {SearchUrl}", searchUrl);

                var html = await GetHtmlAsync(searchUrl, ct);
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
                    // Prefer nodes that actually contain product links (more likely to be real results)
                    var nodesWithLinks = resultNodes.Where(n => n.SelectSingleNode(".//a[contains(@href,'/pd/')]") != null).ToList();
                    _logger.LogInformation("Filtered {WithLinks} nodes containing product links from {Total} potential nodes", nodesWithLinks.Count, resultNodes.Count);

                    var processed = 0;
                    var foundAsin = 0;
                    var duplicateAsinCount = 0;

                    // Per-provider cap for unique ASINs (align with SearchService default)
                    var providerUniqueCap = 50;
                    var seenAsins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var seenImageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Increase processing limit to capture more nodes but stop when we've collected enough unique ASINs
                    foreach (var node in nodesWithLinks.Take(200))
                    {
                        ct.ThrowIfCancellationRequested();
                        processed++;
                        var result = ExtractAudibleSearchResult(node, seenImageIds);
                        if (result != null && !string.IsNullOrEmpty(result.Asin))
                        {
                            // Deduplicate early by ASIN to avoid emitting the same ASIN multiple times
                            if (seenAsins.Contains(result.Asin))
                            {
                                duplicateAsinCount++;
                                _logger.LogDebug("Skipping duplicate ASIN {Asin} from node", result.Asin);
                                // Stop early if we've already collected enough unique ASINs
                                if (seenAsins.Count >= providerUniqueCap) break;
                                continue;
                            }

                            // Mark seen and count as a found unique ASIN
                            seenAsins.Add(result.Asin);
                            foundAsin++;

                            // Clean noisy titles
                            if (IsHeaderNoise(result.Title))
                            {
                                ct.ThrowIfCancellationRequested();
                                var fetched = await TryFetchProductTitle(result.ProductUrl, result.Asin!, ct);
                                if (!string.IsNullOrWhiteSpace(fetched)) result.Title = fetched;
                            }
                            if (!IsHeaderNoise(result.Title))
                            {
                                results.Add(result);
                                _logger.LogInformation("Extracted Audible result: {Title} (ASIN: {Asin})", result.Title, result.Asin);
                            }
                            else
                            {
                                _logger.LogDebug("Skipped noisy title for ASIN {Asin}", result.Asin);
                            }

                            // Stop early if we've reached the provider unique cap
                            if (seenAsins.Count >= providerUniqueCap)
                            {
                                _logger.LogInformation("Reached provider unique ASIN cap: {Cap}", providerUniqueCap);
                                break;
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Node did not yield an ASIN or was unparsable (processed {Processed} of nodesWithLinks)", processed);
                        }
                    }

                    _logger.LogInformation("Processed {Processed} product-linked nodes, found {FoundAsin} unique ASINs, skipped {DuplicateCount} duplicates", processed, foundAsin, duplicateAsinCount);
                }

                // If no results with first selector, try broader search
                if (!results.Any())
                {
                    _logger.LogInformation("No results with primary selectors, trying broader search");
                    var allLinks = doc.DocumentNode.SelectNodes("//a[contains(@href,'/pd/')]");

                    if (allLinks != null)
                    {
                        _logger.LogInformation("Found {Count} direct product links on page, trying a broader extraction", allLinks.Count);

                        var seenFallback = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var fallbackDuplicates = 0;
                        var fallbackFound = 0;
                        var fallbackCap = 50;

                        foreach (var link in allLinks.Take(50))
                        {
                            var result = ExtractFromProductLink(link);
                            if (result != null && !string.IsNullOrEmpty(result.Asin))
                            {
                                if (seenFallback.Contains(result.Asin))
                                {
                                    fallbackDuplicates++;
                                    _logger.LogDebug("Skipping duplicate ASIN in fallback extraction: {Asin}", result.Asin);
                                    if (seenFallback.Count >= fallbackCap) break;
                                    continue;
                                }

                                seenFallback.Add(result.Asin);
                                fallbackFound++;

                                if (IsHeaderNoise(result.Title))
                                {
                                    ct.ThrowIfCancellationRequested();
                                    var fetched = await TryFetchProductTitle(result.ProductUrl, result.Asin!, ct);
                                    if (!string.IsNullOrWhiteSpace(fetched)) result.Title = fetched;
                                }
                                if (!IsHeaderNoise(result.Title))
                                {
                                    results.Add(result);
                                    _logger.LogInformation("Extracted from link: {Title} (ASIN: {Asin})", result.Title, result.Asin);
                                }
                                else
                                {
                                    _logger.LogDebug("Skipped noisy title from direct link for ASIN {Asin}", result.Asin);
                                }

                                if (seenFallback.Count >= fallbackCap)
                                {
                                    _logger.LogInformation("Reached fallback unique ASIN cap: {Cap}", fallbackCap);
                                    break;
                                }
                            }
                        }

                        _logger.LogInformation("Fallback extraction processed {Links} links, found {Found} unique ASINs, skipped {Dup}", allLinks.Count, fallbackFound, fallbackDuplicates);
                    }
                }

                _logger.LogInformation("Found {Count} Audible search results for query: {Query}", results.Count, LogRedaction.SanitizeText(query));
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Audible for query: {Query}", LogRedaction.SanitizeText(query));
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

            // If the title contains any header noise phrase, treat as noise (regardless of length)
            if (HeaderNoisePhrases.Any(p => t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)) return true;

            // If the title contains any known locale/geo redirect phrasing, treat as noise
            if (RedirectNoisePhrases.Any(p => t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)) return true;

            // Repeated blocks like header duplicated
            if (t.Count(c => c == '\n') > 3 && t.Length > 120) return true;

            // Check for titles that are just whitespace and newlines
            if (t.All(c => char.IsWhiteSpace(c) || c == '\n' || c == '\r')) return true;

            return false;
        }

        internal async Task<string?> TryFetchProductTitle(string? productUrl, string asin, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(productUrl)) return null;
            try
            {
            ct.ThrowIfCancellationRequested();
            var html = await GetHtmlAsync(productUrl, ct);
                if (string.IsNullOrEmpty(html)) return null;
                // If the fetched HTML contains known redirect/locale messages, treat as noise and skip
                var lowerHtml = html.ToLowerInvariant();
                if (RedirectNoisePhrases.Any(p => lowerHtml.Contains(p)))
                {
                    var snippet = html.Length > 300 ? html.Substring(0, 300) : html;
                    _logger.LogWarning("Detected locale/redirect noise in product page for {Asin}, skipping title. Snippet: {Snippet}", asin, snippet);
                    return null;
                }
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // Prefer og:title or <title> before falling back to H1 — more reliable across locales and templates
                var metaOg = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null);
                if (!string.IsNullOrWhiteSpace(metaOg))
                {
                    _logger.LogDebug("Found og:title for {Asin}: {OgTitle}", asin, metaOg);
                    return metaOg.Trim();
                }

                var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(pageTitle))
                {
                    _logger.LogDebug("Found <title> for {Asin}: {PageTitle}", asin, pageTitle);
                    // Strip site suffixes like "| Audible" if present
                    var clean = Regex.Replace(pageTitle, "\\|.*$", "", RegexOptions.None).Trim();
                    if (!string.IsNullOrWhiteSpace(clean)) return clean;
                }

                var titleNode = doc.DocumentNode.SelectSingleNode("//h1 | //h1//span | //div[contains(@class,'title')]//h1 | //h1[contains(@class,'bc-heading')]");
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

        private AudibleSearchResult? ExtractAudibleSearchResult(HtmlNode node, HashSet<string> seenImageIds)
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
                var rawImageUrl = imageNode?.GetAttributeValue("src", "") ?? imageNode?.GetAttributeValue("data-lazy", "");
                _logger.LogDebug("Raw image URL for ASIN {Asin}: {RawUrl}", extractedAsin, rawImageUrl);
                // Clean and upgrade to high-resolution image URL
                var imageUrl = CleanImageUrl(rawImageUrl);
                _logger.LogDebug("Cleaned image URL for ASIN {Asin}: {CleanUrl}", extractedAsin, imageUrl);

                // Extract image id and ensure we only upgrade/log each image once per page
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    var idMatch = System.Text.RegularExpressions.Regex.Match(imageUrl, @"/images/I/([A-Za-z0-9_-]+)\.");
                    if (idMatch.Success)
                    {
                        var imageId = idMatch.Groups[1].Value;
                        if (seenImageIds.Contains(imageId))
                        {
                            _logger.LogDebug("Skipping duplicate image upgrade for image id {ImageId}", imageId);
                            imageUrl = null;
                        }
                        else
                        {
                            seenImageIds.Add(imageId);
                            _logger.LogInformation("Upgraded image URL to high-res: {ImageId} -> {CleanUrl}", imageId, imageUrl);
                        }
                    }
                }

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

                // Extract publisher from publisherLabel or publisherSummary with comprehensive selectors
                var publisherNode = node.SelectSingleNode(
                    ".//li[contains(@class,'publisherLabel')] | " +
                    ".//span[contains(@class,'publisherLabel')] | " +
                    ".//li[contains(@class,'publisherSummary')] | " +
                    ".//span[contains(@class,'publisherSummary')] | " +
                    ".//li[contains(@class,'publisher')] | " +
                    ".//span[contains(@class,'publisher')] | " +
                    ".//span[contains(text(),'By:')]/following-sibling::span | " +
                    ".//span[contains(text(),'Publisher:')]/following-sibling::span | " +
                    ".//li[contains(text(),'Publisher:')]"
                );
                var publisher = publisherNode?.InnerText?
                    .Replace("By:", "")
                    .Replace("Publisher:", "")
                    .Replace("©", "")
                    .Trim();
                
                // Log if publisher extraction fails for debugging
                if (string.IsNullOrWhiteSpace(publisher))
                {
                    _logger.LogDebug("Publisher not found for result with title: {Title}, ASIN: {Asin}", title, extractedAsin);
                }

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
                    Language = language,
                    Publisher = publisher
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

        internal async Task<string?> GetHtmlAsync(string url, CancellationToken ct = default)
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

                var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();

                // If the page looks like a geo-redirect / locale notice, try forcing a .com domain and re-request
                var lowerHtml = html?.ToLowerInvariant() ?? string.Empty;
                if (RedirectNoisePhrases.Any(p => lowerHtml.Contains(p)) || IsNonUsHost(url))
                {
                    try
                    {
                        var usUrl = ForceToUSDomain(url);
                        if (!string.Equals(usUrl, url, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("Retrying Audible URL using US domain: {UsUrl}", usUrl);
                            using var retryReq = new HttpRequestMessage(HttpMethod.Get, usUrl);
                            // copy headers
                            foreach (var h in request.Headers)
                                retryReq.Headers.TryAddWithoutValidation(h.Key, h.Value);
                            HttpClient? usClient = null;
                            try
                            {
                                // Prefer a client configured from runtime application settings so proxy can be changed by user
                                var appSettings = await _configurationService.GetApplicationSettingsAsync();
                                if (appSettings != null && appSettings.UseUsProxy && !string.IsNullOrWhiteSpace(appSettings.UsProxyHost) && appSettings.UsProxyPort > 0)
                                {
                                    var handler = new HttpClientHandler
                                    {
                                        AutomaticDecompression = DecompressionMethods.All
                                    };
                                    var proxy = new WebProxy(appSettings.UsProxyHost, appSettings.UsProxyPort);
                                    if (!string.IsNullOrWhiteSpace(appSettings.UsProxyUsername))
                                        proxy.Credentials = new NetworkCredential(appSettings.UsProxyUsername, appSettings.UsProxyPassword ?? string.Empty);
                                    handler.Proxy = proxy;
                                    handler.UseProxy = true;
                                    usClient = new HttpClient(handler, disposeHandler: true);
                                }
                                else if (_httpClientFactory != null)
                                {
                                    usClient = _httpClientFactory.CreateClient("us");
                                }
                                else
                                {
                                    usClient = _httpClient;
                                }

                                var retryResp = await usClient.SendAsync(retryReq, ct);
                                if (retryResp.IsSuccessStatusCode)
                                    return await retryResp.Content.ReadAsStringAsync();
                            }
                            finally
                            {
                                // Dispose only if we created a dedicated client here
                                if (usClient != null && usClient != _httpClient && (_httpClientFactory == null || usClient != _httpClientFactory.CreateClient("us")))
                                {
                                    try { usClient.Dispose(); } catch { }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed retrying Audible URL as US domain");
                    }
                }

                return html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching HTML from Audible URL: {Url}", url);
                return null;
            }
        }

        internal static bool IsNonUsHost(string url)
        {
            try
            {
                var u = new Uri(url);
                var host = u.Host ?? string.Empty;
                // If host contains audible but not .com, treat as non-US
                if (host.Contains("audible", StringComparison.OrdinalIgnoreCase) && !host.EndsWith(".com", StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string? CleanImageUrl(string? imgUrl)
        {
            if (string.IsNullOrWhiteSpace(imgUrl))
            {
                _logger.LogDebug("CleanImageUrl: Empty or null image URL");
                return null;
            }

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

            // Clean up social share overlay URLs and upgrade to high-resolution
            // Example: https://m.media-amazon.com/images/I/61D7uTS7-TL._SL400_.jpg
            // Should become: https://m.media-amazon.com/images/I/61D7uTS7-TL._SL500_.jpg
            var cleanUrl = imgUrl;
            
            // Extract image ID and create high-res URL
            var match = System.Text.RegularExpressions.Regex.Match(imgUrl, @"/images/I/([A-Za-z0-9_-]+)\.");
                if (match.Success)
            {
                var imageId = match.Groups[1].Value;
                cleanUrl = $"https://m.media-amazon.com/images/I/{imageId}._SL500_.jpg";
                _logger.LogDebug("Upgraded image URL to high-res (deferred log): {ImageId} -> {CleanUrl}", imageId, cleanUrl);
            }
            else
            {
                _logger.LogWarning("Could not extract image ID from URL: {Url}", imgUrl);
            }

            return cleanUrl;
        }

        internal static string ForceToUSDomain(string url)
        {
            try
            {
                var u = new Uri(url);
                var host = u.Host;
                if (host.Contains("audible", StringComparison.OrdinalIgnoreCase))
                {
                    var builder = new UriBuilder(u)
                    {
                        Host = "www.audible.com"
                    };
                    return builder.Uri.ToString();
                }
                return url;
            }
            catch
            {
                return url;
            }
        }
    }
}