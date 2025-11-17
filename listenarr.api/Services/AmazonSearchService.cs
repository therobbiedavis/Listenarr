using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;

namespace Listenarr.Api.Services
{
    public class AmazonSearchResult
    {
        public string? Asin { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? ImageUrl { get; set; }
        public string? Price { get; set; }
        public bool IsAudiobook { get; set; }
    }

    public interface IAmazonSearchService
    {
        Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string title, string? author = null);
    }

    public class AmazonSearchService : IAmazonSearchService
    {
    private readonly HttpClient _httpClient;
    private readonly ILogger<AmazonSearchService> _logger;
    private readonly IPlaywrightPageFetcher _playwrightFetcher;
    private readonly IAmazonAsinService _amazonAsinService;
        private static readonly Regex AsinRegex = new(@"/dp/([A-Z0-9]{10})", RegexOptions.Compiled);
        // New: placeholder / non-product title patterns
        private static readonly Regex PlaceholderTitleRegex = new(
            @"^(?:\d+[-–]\d+ of \d+ results for|results for|RESULTS FOR|Amazon\.com:|audible session|Audible membership|Best Sellers|Discover more)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const int EarlyAsinCap = 50; // Cap unique ASINs early to avoid over-fetching (relaxed for debugging)
        
        private bool IsPlaceholderTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;
            var t = title.Trim();
            if (t.Length < 3) return true; // too short
            if (PlaceholderTitleRegex.IsMatch(t)) return true;
            // Filter summary-like titles containing 'results for'
            if (t.Contains("results for", StringComparison.OrdinalIgnoreCase) && t.IndexOf('"') == -1)
                return true;
            return false;
        }
        
        private AmazonSearchResult? ExtractFromLink(HtmlNode linkNode)
        {
            try
            {
                var href = linkNode.GetAttributeValue("href", "");
                var asinMatch = AsinRegex.Match(href);
                if (!asinMatch.Success) return null;
                
                var asin = asinMatch.Groups[1].Value;
                var title = linkNode.InnerText?.Trim() ?? "Unknown Title";
                
                // Try to find parent container for more info
                var container = linkNode.ParentNode;
                for (int i = 0; i < 5 && container != null; i++)
                {
                    var titleNode = container.SelectSingleNode(".//span[contains(@class,'a-text-normal')] | .//h2//span | .//h3//span");
                    if (titleNode != null && !string.IsNullOrEmpty(titleNode.InnerText))
                    {
                        title = titleNode.InnerText.Trim();
                        break;
                    }
                    container = container.ParentNode;
                }
                
                if (IsPlaceholderTitle(title))
                {
                    // Attempt product page fetch for a better title (limited via caller)
                    title = null;
                }
                
                return new AmazonSearchResult
                {
                    Asin = asin,
                    Title = title,
                    IsAudiobook = true // Assume true since we're searching in Audible category
                };
            }
            catch
            {
                return null;
            }
        }

        public AmazonSearchService(HttpClient httpClient, IPlaywrightPageFetcher playwrightFetcher, ILogger<AmazonSearchService> logger, IAmazonAsinService amazonAsinService)
        {
            _httpClient = httpClient;
            _playwrightFetcher = playwrightFetcher;
            _logger = logger;
            _amazonAsinService = amazonAsinService;
        }

        public async Task<List<AmazonSearchResult>> SearchAudiobooksAsync(string title, string? author = null)
        {
            var results = new List<AmazonSearchResult>();

            try
            {
                // Build search query
                var searchQuery = title;
                if (!string.IsNullOrEmpty(author))
                {
                    searchQuery += $" {author}";
                }

                // Try multiple search variations. If the incoming title looks like an ISBN
                // prefer the ISBN/Books (stripbooks) path and DO NOT try Audible index
                // variations which frequently return unrelated Audible results for ISBNs.
                var digitsOnlyCheck = Regex.Replace(searchQuery ?? string.Empty, "\\D", "");
                List<string> searchVariations;
                if (!string.IsNullOrEmpty(digitsOnlyCheck) && (digitsOnlyCheck.Length == 10 || digitsOnlyCheck.Length == 13))
                {
                    // For ISBN searches, only use the cleaned digits (no extra suffixes)
                    _logger.LogInformation("ISBN-only search detected for {Query}; restricting variations to ISBN mode", searchQuery);
                    searchVariations = new List<string> { digitsOnlyCheck };
                }
                else
                {
                    // General title searches: try audiobook/audible variants
                    searchVariations = new List<string>
                    {
                        $"{(searchQuery ?? string.Empty)} audiobook",
                        $"{(searchQuery ?? string.Empty)} audible",
                        (searchQuery ?? string.Empty)
                    };
                }

                foreach (var query in searchVariations)
                {
                    var searchResults = await SearchAmazonAsync(query);
                    results.AddRange(searchResults);
                    
                    // Stop if we found good results
                    if (results.Count >= 10) break;
                }

                // Remove duplicates based on ASIN & discard placeholder titles if any slipped
                results = results
                    .Where(r => !string.IsNullOrEmpty(r.Asin))
                    .GroupBy(r => r.Asin)
                    .Select(g => g.First())
                    .Where(r => !IsPlaceholderTitle(r.Title))
                    .Take(EarlyAsinCap) // enforce cap again at aggregation
                    .ToList();

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Amazon for audiobooks: {Title} by {Author}", title, author);
                return results;
            }
        }

        private async Task<List<AmazonSearchResult>> SearchAmazonAsync(string query)
        {
            var results = new List<AmazonSearchResult>();
            var seenAsins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int productPageFetches = 0;
            // Track titles / ASINs filtered out due to placeholder detection for diagnostics
            var filteredOut = new List<(string Asin, string? Title, string Reason)>();

            try
            {
                // If the query looks like an ISBN (digits only, length 10 or 13), search the books index
                // using the ISBN filter (p_66) to get exact ISBN matches. Otherwise search Audible index.
                var digitsOnly = Regex.Replace(query ?? string.Empty, "\\D", "");
                string searchUrl;
                if (!string.IsNullOrEmpty(digitsOnly) && (digitsOnly.Length == 10 || digitsOnly.Length == 13))
                {
                    // Exact-ISBN search in Books (stripbooks) using p_66 filter
                    searchUrl = $"https://www.amazon.com/s?i=stripbooks&rh=p_66%3A{Uri.EscapeDataString(digitsOnly)}";
                }
                else
                {
                    // Search specifically in Audible store for general queries
                    searchUrl = $"https://www.amazon.com/s?k={Uri.EscapeDataString(query ?? string.Empty)}&i=audible&ref=sr_nr_n_1";
                }
                
                _logger.LogInformation("Searching Amazon: {SearchUrl}", searchUrl);

                var html = await GetHtmlAsync(searchUrl);
                if (string.IsNullOrEmpty(html))
                {
                    return results;
                }

                // Parse search results
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Debug: Log HTML length and check for bot detection
                _logger.LogInformation("Amazon HTML response length: {Length} characters", html.Length);
                
                if (html.Contains("To discuss automated access to Amazon data please contact"))
                {
                    _logger.LogWarning("Amazon bot detection triggered");
                    return results;
                }

                // Find search result items using modern Amazon selectors
                var resultNodes = doc.DocumentNode.SelectNodes(
                    "//div[@data-component-type='s-search-result'] | " +
                    "//div[contains(@class,'s-result-item')] | " +
                    "//div[contains(@class,'s-card-container')] | " +
                    "//div[contains(@class,'sg-col-inner')] | " +
                    "//div[contains(@class,'s-widget-container')] | " +
                    "//span[contains(@class,'rush-component')] | " +
                    "//div[contains(@data-index,'')][contains(@class,'s-result')] | " +
                    "//div[contains(@class,'AdHolder')] | " +
                    "//div[contains(@class,'s-expand-height')] | " +
                    "//article | " +
                    "//div[.//a[contains(@href,'/dp/')]] | " +
                    "//div[.//a[contains(@href,'/gp/product/')]] | " +
                    "//li[contains(@class,'s-result-item')]"
                );

                _logger.LogInformation("Found {Count} result nodes using XPath selector", resultNodes?.Count ?? 0);

                if (resultNodes != null)
                {
                    foreach (var node in resultNodes.Take(50)) // Limit to first 50 results for broader capture (relaxed)
                    {
                        if (results.Count >= EarlyAsinCap) break; // early cap
                        var result = ExtractSearchResult(node);
                                if (result != null && !string.IsNullOrEmpty(result.Asin) && seenAsins.Add(result.Asin))
                                {
                                    // If this query is an ISBN, require product-page verification that the ISBN is present
                                    if (!string.IsNullOrEmpty(digitsOnly) && (digitsOnly.Length == 10 || digitsOnly.Length == 13))
                                    {
                                        try
                                        {
                                            var verify = await _amazonAsinService.VerifyAsinContainsIsbnAsync(result.Asin!, digitsOnly);
                                            if (verify.Success && verify.MatchesIsbn)
                                            {
                                                // Optionally fetch a nicer title if we only had a placeholder
                                                if (IsPlaceholderTitle(result.Title) && productPageFetches < 20)
                                                {
                                                    try
                                                    {
                                                        var fetched = await FetchProductTitleAsync(result.Asin);
                                                        if (!IsPlaceholderTitle(fetched))
                                                        {
                                                            result.Title = fetched;
                                                            productPageFetches++;
                                                        }
                                                    }
                                                    catch (Exception pex)
                                                    {
                                                        _logger.LogDebug(pex, "Product page title fetch failed for {Asin}", result.Asin);
                                                    }
                                                }
                                                results.Add(result);
                                            }
                                            else
                                            {
                                                // Record why the candidate was rejected for diagnostics
                                                var reason = verify.Success ? "product-page-missing-isbn" : $"verify-failed:{verify.Error}";
                                                filteredOut.Add((result.Asin ?? "<unknown>", result.Title, reason));
                                                _logger.LogDebug("Rejecting ASIN {Asin} for ISBN query {Isbn}: {Reason}", result.Asin, digitsOnly, reason);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            filteredOut.Add((result.Asin ?? "<unknown>", result.Title, "verify-exception"));
                                            _logger.LogDebug(ex, "Exception verifying ASIN {Asin} for ISBN {Isbn}", result.Asin, digitsOnly);
                                        }
                                    }
                                    else
                                    {
                                        // Non-ISBN flow unchanged
                                        if (IsPlaceholderTitle(result.Title))
                                        {
                                            // attempt fetch product page title if budget remains (increased budget)
                                            if (productPageFetches < 20)
                                            {
                                                try
                                                {
                                                    var fetched = await FetchProductTitleAsync(result.Asin);
                                                    if (!IsPlaceholderTitle(fetched))
                                                    {
                                                        result.Title = fetched;
                                                        productPageFetches++;
                                                    }
                                                }
                                                catch (Exception pex)
                                                {
                                                    _logger.LogDebug(pex, "Product page title fetch failed for {Asin}", result.Asin);
                                                }
                                            }
                                        }
                                        if (!IsPlaceholderTitle(result.Title))
                                        {
                                            results.Add(result);
                                        }
                                        else
                                        {
                                            // Still a placeholder after any fetch attempts - record for diagnostics
                                            filteredOut.Add((result.Asin ?? "<unknown>", result.Title, "placeholder-after-fetch"));
                                            _logger.LogDebug("Filtering out ASIN {Asin} due to placeholder title (after fetch attempts): {Title}", result.Asin, result.Title);
                                        }
                                    }
                                }
                    }
                }
                
                // Fallback: If no results, try finding any links with ASINs
                if (!results.Any())
                {
                    _logger.LogInformation("No results with primary selectors, trying ASIN link fallback");
                    var asinLinks = doc.DocumentNode.SelectNodes("//a[contains(@href,'/dp/')]");
                    if (asinLinks != null)
                    {
                        foreach (var link in asinLinks.Take(50))
                        {
                            if (results.Count >= EarlyAsinCap) break;
                            var result = ExtractFromLink(link);
                            if (result != null && !string.IsNullOrEmpty(result.Asin) && seenAsins.Add(result.Asin))
                            {
                                    if (IsPlaceholderTitle(result.Title) && productPageFetches < 20)
                                {
                                    try
                                    {
                                        var fetched = await FetchProductTitleAsync(result.Asin);
                                        if (!IsPlaceholderTitle(fetched))
                                        {
                                            result.Title = fetched;
                                            productPageFetches++;
                                        }
                                    }
                                    catch (Exception dpex)
                                    {
                                        _logger.LogDebug(dpex, "Failed to fetch product page for ASIN {Asin}", result.Asin);
                                    }
                                }
                                    if (!IsPlaceholderTitle(result.Title))
                                        results.Add(result);
                                    else
                                    {
                                        filteredOut.Add((result.Asin ?? "<unknown>", result.Title, "link-fallback-placeholder"));
                                        _logger.LogDebug("Filtering out ASIN {Asin} from link fallback due to placeholder title: {Title}", result.Asin, result.Title);
                                    }
                            }
                        }
                    }
                }

                // Second-level fallback: regex scan entire HTML for /dp/ASIN patterns if still empty
                if (!results.Any())
                {
                    _logger.LogInformation("No results after DOM link fallback. Running regex ASIN scan.");
                    try
                    {
                        var asinMatches = AsinRegex.Matches(html);
                        foreach (Match m in asinMatches)
                        {
                            if (results.Count >= EarlyAsinCap) break;
                            if (!m.Success) continue;
                            var asin = m.Groups[1].Value;
                            if (!seenAsins.Add(asin)) continue;

                            var anchor = doc.DocumentNode.SelectSingleNode($"//a[contains(@href,'/dp/{asin}')]");
                            var titleGuess = anchor?.InnerText?.Trim() ?? "Unknown Title";
                            if (IsPlaceholderTitle(titleGuess) && productPageFetches < 5)
                            {
                                try
                                {
                                    var fetched = await FetchProductTitleAsync(asin);
                                    if (!IsPlaceholderTitle(fetched))
                                    {
                                        titleGuess = fetched!;
                                        productPageFetches++;
                                    }
                                }
                                catch (Exception dpex)
                                {
                                    _logger.LogDebug(dpex, "Failed to fetch product page for ASIN {Asin}", asin);
                                }
                            }
                            if (IsPlaceholderTitle(titleGuess))
                            {
                                filteredOut.Add((asin, titleGuess, "regex-placeholder"));
                                _logger.LogDebug("Filtering out ASIN {Asin} from regex fallback due to placeholder title: {Title}", asin, titleGuess);
                                continue;
                            }
                            results.Add(new AmazonSearchResult
                            {
                                Asin = asin,
                                Title = titleGuess,
                                IsAudiobook = true
                            });
                        }
                        _logger.LogInformation("Regex fallback added {Count} results", results.Count);
                    }
                    catch (Exception rex)
                    {
                        _logger.LogWarning(rex, "Regex ASIN fallback failed");
                    }
                }

                _logger.LogInformation("Found {Count} Amazon search results for query: {Query}", results.Count, query);

                // Diagnostic: log how many candidates were filtered out due to placeholder titles
                if (filteredOut.Any())
                {
                    var sample = string.Join(", ", filteredOut.Take(10).Select(f => $"{f.Asin}:{(string.IsNullOrWhiteSpace(f.Title)?"<empty>":f.Title)}[{f.Reason}]"));
                    _logger.LogInformation("Filtered out {Count} ASIN candidates due to placeholder titles (showing up to 10): {Sample}", filteredOut.Count, sample);
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Amazon search results for query: {Query}", query);
                return results;
            }
        }

        private AmazonSearchResult? ExtractSearchResult(HtmlNode node)
        {
            try
            {
                // Extract ASIN
                var asinLink = node.SelectSingleNode(".//a[contains(@href,'/dp/')]")?.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(asinLink)) return null;

                var asinMatch = AsinRegex.Match(asinLink);
                if (!asinMatch.Success) return null;

                var asin = asinMatch.Groups[1].Value;

                // Extract title
                var titleNode = node.SelectSingleNode(
                    ".//span[contains(@class,'a-size-base-plus')] | " +
                    ".//span[contains(@class,'a-size-medium')] | " +
                    ".//h2//span | " +
                    ".//a//span[contains(@class,'a-text-normal')]"
                );
                var title = titleNode?.InnerText.Trim();

                // Extract author/narrator info
                var authorNode = node.SelectSingleNode(
                    ".//span[contains(@class,'a-size-base')] | " +
                    ".//span[contains(text(),'by')] | " +
                    ".//a[contains(@href,'author')]"
                );
                var author = authorNode?.InnerText.Trim();

                // Clean up author text
                if (!string.IsNullOrEmpty(author))
                {
                    author = author.Replace("by ", "").Replace("By ", "").Trim();
                }

                // Extract image
                var imageNode = node.SelectSingleNode(".//img");
                var imageUrl = imageNode?.GetAttributeValue("src", "") ?? imageNode?.GetAttributeValue("data-src", "");

                // Extract price
                var priceNode = node.SelectSingleNode(
                    ".//span[contains(@class,'a-price-whole')] | " +
                    ".//span[contains(@class,'a-price')] | " +
                    ".//span[contains(text(),'$')]"
                );
                var price = priceNode?.InnerText.Trim();

                // Check if it's likely an audiobook
                var nodeText = node.InnerText.ToLower();
                var isAudiobook = nodeText.Contains("audible") || 
                                  nodeText.Contains("audiobook") || 
                                  nodeText.Contains("narrator") ||
                                  nodeText.Contains("narrated by");

                return new AmazonSearchResult
                {
                    Asin = asin,
                    Title = title,
                    Author = author,
                    ImageUrl = imageUrl,
                    Price = price,
                    IsAudiobook = isAudiobook
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting search result from Amazon node");
                return null;
            }
        }

        private async Task<string?> GetHtmlAsync(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add realistic headers
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("Connection", "keep-alive");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                // If the response is not successful or looks like a bot/challenge page, try Playwright fallback
                if (!response.IsSuccessStatusCode ||
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == (HttpStatusCode)429 ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    (body != null && (body.Contains("captcha", StringComparison.OrdinalIgnoreCase) || body.Contains("To discuss automated access to Amazon data please contact", StringComparison.OrdinalIgnoreCase))))
                {
                    _logger.LogInformation("HttpClient fetch returned status {Status} or challenge; falling back to Playwright for {Url}", (int)response.StatusCode, url);
                    try
                    {
                        var pw = await _playwrightFetcher.FetchAsync(url);
                        if (!string.IsNullOrWhiteSpace(pw)) return pw;
                    }
                    catch (Exception pex)
                    {
                        _logger.LogDebug(pex, "Playwright fallback failed for {Url}", url);
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    response.EnsureSuccessStatusCode();
                }

                return body;
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException<System.Net.Http.HttpResponseMessage> ex)
            {
                // Circuit breaker is open - try Playwright fallback
                _logger.LogWarning(ex, "Amazon search circuit open; attempting Playwright fallback for {Url}", url);
                try
                {
                    var pw = await _playwrightFetcher.FetchAsync(url);
                    return pw;
                }
                catch (Exception pex)
                {
                    _logger.LogWarning(pex, "Playwright fallback also failed for {Url}", url);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching HTML from URL: {Url}", url);
                // Try Playwright as a last resort
                try
                {
                    var pw = await _playwrightFetcher.FetchAsync(url);
                    return pw;
                }
                catch (Exception pex)
                {
                    _logger.LogDebug(pex, "Playwright fallback failed for {Url}", url);
                    return null;
                }
            }
        }

        private async Task<string?> FetchProductTitleAsync(string asin)
        {
            try
            {
                var url = $"https://www.amazon.com/dp/{asin}";
                var html = await GetHtmlAsync(url);
                if (string.IsNullOrEmpty(html)) return null;
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var titleNode = doc.DocumentNode.SelectSingleNode("//span[@id='productTitle'] | //h1//span | //h1");
                var title = titleNode?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    // Try meta og:title
                    var ogNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
                    title = ogNode?.GetAttributeValue("content", null)?.Trim() ?? title;
                }
                if (string.IsNullOrWhiteSpace(title))
                {
                    // Try document <title>
                    var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
                    if (!string.IsNullOrWhiteSpace(pageTitle))
                    {
                        // Amazon often appends: : A Novel, Audible Audiobook – Unabridged
                        // Remove trailing marketing phrases after a delimiter
                        var cleaned = pageTitle.Split('|', '-', '–').FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(cleaned)) title = cleaned.Trim();
                    }
                }
                // Filter placeholders / generic phrases
                if (!string.IsNullOrWhiteSpace(title) && (title.StartsWith("Amazon.com:") || title.Contains("Audible Audiobook")))
                {
                    // Strip leading Amazon.com: and descriptor
                    title = title.Replace("Amazon.com:", "").Trim();
                }
                if (!string.IsNullOrWhiteSpace(title))
                {
                    _logger.LogInformation("Fetched product page title for {Asin}: {Title}", asin, title);
                }
                return title;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error fetching product title for {Asin}", asin);
                return null;
            }
        }
    }
}