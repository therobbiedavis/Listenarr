using System.Text.RegularExpressions;
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
        private static readonly Regex AsinRegex = new(@"/dp/([A-Z0-9]{10})", RegexOptions.Compiled);
        // New: placeholder / non-product title patterns
        private static readonly Regex PlaceholderTitleRegex = new(
            @"^(?:\d+[-–]\d+ of \d+ results for|results for|RESULTS FOR|Amazon\.com:|audible session|Audible membership|Best Sellers|Discover more)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private const int EarlyAsinCap = 20; // Cap unique ASINs early to avoid over-fetching
        
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

        public AmazonSearchService(HttpClient httpClient, ILogger<AmazonSearchService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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

                // Try multiple search variations
                var searchVariations = new List<string>
                {
                    $"{searchQuery} audiobook",
                    $"{searchQuery} audible",
                    searchQuery
                };

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

            try
            {
                // Search specifically in Audible store
                var searchUrl = $"https://www.amazon.com/s?k={Uri.EscapeDataString(query)}&i=audible&ref=sr_nr_n_1";
                
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
                    foreach (var node in resultNodes.Take(25)) // Limit to first 25 results for broader capture
                    {
                        if (results.Count >= EarlyAsinCap) break; // early cap
                        var result = ExtractSearchResult(node);
                        if (result != null && !string.IsNullOrEmpty(result.Asin) && seenAsins.Add(result.Asin))
                        {
                            // Filter placeholders early
                            if (IsPlaceholderTitle(result.Title))
                            {
                                // attempt fetch product page title if budget remains
                                if (productPageFetches < 5)
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
                        foreach (var link in asinLinks.Take(20))
                        {
                            if (results.Count >= EarlyAsinCap) break;
                            var result = ExtractFromLink(link);
                            if (result != null && !string.IsNullOrEmpty(result.Asin) && seenAsins.Add(result.Asin))
                            {
                                if (IsPlaceholderTitle(result.Title) && productPageFetches < 5)
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
                            if (IsPlaceholderTitle(titleGuess)) continue;
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
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching HTML from URL: {Url}", url);
                return null;
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