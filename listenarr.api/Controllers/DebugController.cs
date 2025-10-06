using Microsoft.AspNetCore.Mvc;
using Listenarr.Api.Services;
using System.Threading.Tasks;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly IAudibleMetadataService _audibleService;
        private readonly ILogger<DebugController> _logger;

        public DebugController(IAudibleMetadataService audibleService, ILogger<DebugController> logger)
        {
            _audibleService = audibleService;
            _logger = logger;
        }

        [HttpGet("audible-html/{asin}")]
        public async Task<IActionResult> GetAudibleHtml(string asin)
        {
            try
            {
                // This will use Playwright to get the rendered HTML
                var metadata = await _audibleService.ScrapeAudibleMetadataAsync(asin);
                
                return Ok(new
                {
                    asin,
                    metadata = new
                    {
                        metadata.Source,
                        metadata.Title,
                        metadata.Authors,
                        metadata.Description,
                        metadata.Publisher,
                        metadata.Language,
                        metadata.Narrators
                    },
                    note = "Check API logs for detailed HTML structure information"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Audible HTML for ASIN {Asin}", asin);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
