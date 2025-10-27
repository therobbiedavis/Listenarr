using System.Net;
using System.Text.RegularExpressions;

namespace Listenarr.Api.Services
{
    public class AmazonAsinService : IAmazonAsinService
    {
    private readonly HttpClient _httpClient;
    private readonly ILogger<AmazonAsinService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IConfigurationService _configurationService;
    private static readonly Regex AsinRegex = new("/dp/([A-Z0-9]{10})", RegexOptions.Compiled);
    private static readonly Regex DataAsinRegex = new("data-asin=\"([A-Z0-9]{10})\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex ProductAsinRegex = new("\\\"asin\\\"\\s*:\\s*\\\"([A-Z0-9]{10})\\\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DetailBulletsRegex = new("<li[^>]*>\\s*<span[^>]*>\\s*ASIN\\s*</span>\\s*<span[^>]*>\\s*([A-Z0-9]{10})\\s*</span>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public AmazonAsinService(HttpClient httpClient, ILogger<AmazonAsinService> logger, IConfigurationService configurationService, IHttpClientFactory? httpClientFactory = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configurationService = configurationService;
        }

        public async Task<(bool Success, string? Asin, string? Error)> GetAsinFromIsbnAsync(string isbn, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(isbn)) return (false, null, "ISBN required");
            var cleaned = isbn.Replace("-", "").Trim();
            if (cleaned.Length is not (10 or 13)) return (false, null, "Invalid ISBN length");

            try
            {
                // Ordered set of search URL attempts (broadening & hinting audible context)
                var searchVariants = new List<string>
                {
                    $"https://www.amazon.com/s?k={WebUtility.UrlEncode(cleaned)}&i=audible",                // audible index direct
                    $"https://www.amazon.com/s?k={WebUtility.UrlEncode(cleaned + " audible")}&i=audible",   // audible index + keyword
                    $"https://www.amazon.com/s?k={WebUtility.UrlEncode(cleaned + " audiobook")}",          // general + audiobook hint
                    $"https://www.amazon.com/s?k={WebUtility.UrlEncode(cleaned)}"                           // general raw
                };

                foreach (var url in searchVariants)
                {
                    var html = await GetHtmlAsync(url, ct);
                    if (html == null) continue;
                    if (IsBotBlocked(html))
                    {
                        _logger.LogWarning("Amazon bot-detection encountered for ISBN {Isbn} at {Url}", isbn, url);
                        // Continue trying other variants; maybe a different path passes.
                        continue;
                    }

                    var asin = ExtractFirstAsin(html);
                    if (!string.IsNullOrEmpty(asin))
                    {
                        var verify = await TryVerifyAsinAsync(asin, cleaned, ct);
                        if (verify.Success)
                            return (true, asin, null);
                    }
                }

                return (false, null, "ASIN not found for ISBN");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve ASIN from ISBN {Isbn}", isbn);
                return (false, null, "Lookup failed");
            }
        }

    internal async Task<string?> GetHtmlAsync(string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            // Amazon expects realistic headers
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            req.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var html = await resp.Content.ReadAsStringAsync(ct);

            // If the response looks like a geo-redirect or we are not on .com, try forcing amazon.com and retry
            var lower = html?.ToLowerInvariant() ?? string.Empty;
            if (IsNonUsHost(url) || lower.Contains("we have redirected") || lower.Contains("have redirected you") || lower.Contains("redirected to"))
            {
                try
                {
                    var usUrl = ForceToUSDomain(url);
                    if (!string.Equals(usUrl, url, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Retrying Amazon URL using US domain: {UsUrl}", usUrl);
                        using var retryReq = new HttpRequestMessage(HttpMethod.Get, usUrl);
                        foreach (var h in req.Headers)
                            retryReq.Headers.TryAddWithoutValidation(h.Key, h.Value);
                        HttpClient? usClient = null;
                        try
                        {
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
                                return await retryResp.Content.ReadAsStringAsync(ct);
                        }
                        finally
                        {
                            if (usClient != null && usClient != _httpClient && (_httpClientFactory == null || usClient != _httpClientFactory.CreateClient("us")))
                            {
                                try { usClient.Dispose(); } catch { }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed retrying Amazon URL as US domain");
                }
            }

            return html;
        }

    internal static bool IsNonUsHost(string url)
        {
            try
            {
                var u = new Uri(url);
                var host = u.Host ?? string.Empty;
                if (host.Contains("amazon", StringComparison.OrdinalIgnoreCase) && !host.EndsWith(".com", StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

    internal static string ForceToUSDomain(string url)
        {
            try
            {
                var u = new Uri(url);
                var host = u.Host;
                if (host.Contains("amazon", StringComparison.OrdinalIgnoreCase))
                {
                    var builder = new UriBuilder(u)
                    {
                        Host = "www.amazon.com"
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

        private string? ExtractFirstAsin(string html)
        {
            // Priority: explicit product links, then data attributes, then JSON, then detail bullets
            var m = AsinRegex.Match(html);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();

            m = DataAsinRegex.Match(html);
            if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                return m.Groups[1].Value.ToUpperInvariant();

            m = ProductAsinRegex.Match(html);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();

            m = DetailBulletsRegex.Match(html);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();

            return null;
        }

        private bool IsBotBlocked(string html)
        {
            // Simple heuristics for Amazon bot / captcha pages
            if (html.Contains("Robot Check", StringComparison.OrdinalIgnoreCase)) return true;
            if (html.Contains("captcha", StringComparison.OrdinalIgnoreCase) && html.Contains("amazon", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private async Task<(bool Success, bool MatchesIsbn)> TryVerifyAsinAsync(string asin, string isbn, CancellationToken ct)
        {
            try
            {
                var productUrl = $"https://www.amazon.com/dp/{asin}";
                var productHtml = await GetHtmlAsync(productUrl, ct);
                if (productHtml == null) return (false, false);

                // Check if ISBN appears on page (some Audible listings include print ISBN)
                if (productHtml.Contains(isbn, StringComparison.OrdinalIgnoreCase))
                    return (true, true);

                // At least we loaded a product page, consider it a partial success
                return (true, false);
            }
            catch
            {
                return (false, false);
            }
        }
    }
}
