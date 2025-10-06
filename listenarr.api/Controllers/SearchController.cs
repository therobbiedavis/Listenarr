using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ISearchService searchService, ILogger<SearchController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<SearchResult>>> Search(
            [FromQuery] string query,
            [FromQuery] string? category = null,
            [FromQuery] List<string>? apiIds = null,
            [FromQuery] bool enrichedOnly = false)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var results = await _searchService.SearchAsync(query, category, apiIds);
                if (enrichedOnly)
                {
                    results = results.Where(r => r.IsEnriched).ToList();
                }
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search for query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("api/{apiId}")]
        public async Task<ActionResult<List<SearchResult>>> SearchByApi(
            string apiId,
            [FromQuery] string query,
            [FromQuery] string? category = null)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var results = await _searchService.SearchByApiAsync(apiId, query, category);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching API {ApiId} for query: {Query}", apiId, query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("test/{apiId}")]
        public async Task<ActionResult<bool>> TestApiConnection(string apiId)
        {
            try
            {
                var isConnected = await _searchService.TestApiConnectionAsync(apiId);
                return Ok(isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing API connection for {ApiId}", apiId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}