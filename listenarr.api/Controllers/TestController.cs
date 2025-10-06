using Microsoft.AspNetCore.Mvc;
using Listenarr.Api.Services;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IAmazonSearchService _amazonSearchService;

        public TestController(IAmazonSearchService amazonSearchService)
        {
            _amazonSearchService = amazonSearchService;
        }

        [HttpGet("amazon-html/{query}")]
        public async Task<IActionResult> GetAmazonHtml(string query)
        {
            // This is a test endpoint to inspect the raw HTML being returned by Amazon
            try
            {
                var url = $"https://www.amazon.com/s?k={Uri.EscapeDataString(query)}&i=audible&ref=sr_nr_n_1";
                
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                // Add realistic headers
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("Connection", "keep-alive");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");

                var response = await httpClient.SendAsync(request);
                var html = await response.Content.ReadAsStringAsync();
                
                // Return truncated HTML for inspection
                var truncated = html.Length > 5000 ? html.Substring(0, 5000) + "... [TRUNCATED]" : html;
                
                return Ok(new { 
                    url = url,
                    statusCode = (int)response.StatusCode,
                    contentLength = html.Length,
                    contentSample = truncated,
                    containsBotWarning = html.Contains("To discuss automated access to Amazon data please contact"),
                    containsSearchResults = html.Contains("data-component-type=\"s-search-result\"") || html.Contains("s-result-item")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("amazon-search/{query}")]
        public async Task<IActionResult> AmazonSearchRaw(string query)
        {
            try
            {
                var results = await _amazonSearchService.SearchAudiobooksAsync(query);
                return Ok(new {
                    query,
                    count = results.Count,
                    asins = results.Select(r => new { r.Asin, r.Title }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}