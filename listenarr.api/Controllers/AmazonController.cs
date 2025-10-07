using Microsoft.AspNetCore.Mvc;
using Listenarr.Api.Services;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AmazonController : ControllerBase
    {
        private readonly IAmazonAsinService _amazonAsinService;
        private readonly ILogger<AmazonController> _logger;

        public AmazonController(IAmazonAsinService amazonAsinService, ILogger<AmazonController> logger)
        {
            _amazonAsinService = amazonAsinService;
            _logger = logger;
        }

        [HttpGet("asin-from-isbn/{isbn}")]
        public async Task<IActionResult> GetAsinFromIsbn(string isbn, CancellationToken ct)
        {
            var result = await _amazonAsinService.GetAsinFromIsbnAsync(isbn, ct);
            if (!result.Success)
            {
                return NotFound(new { success = false, error = result.Error ?? "ASIN not found" });
            }
            return Ok(new { success = true, asin = result.Asin });
        }
    }
}
