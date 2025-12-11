using Listenarr.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/metadata")]
    public class MetadataController : ControllerBase
    {
        private readonly IAudiobookMetadataService _metadataService;
        private readonly ILogger<MetadataController> _logger;

        public MetadataController(
            IAudiobookMetadataService metadataService,
            ILogger<MetadataController> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        /// <summary>
        /// Get audiobook metadata from configured metadata sources by ASIN.
        /// </summary>
        [HttpGet("{asin}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetMetadata(
            string asin,
            [FromQuery] string region = "us",
            [FromQuery] bool cache = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(asin))
                {
                    return BadRequest("ASIN is required");
                }

                var result = await _metadataService.GetMetadataAsync(asin, region, cache);
                if (result == null)
                {
                    return NotFound($"No metadata found for ASIN: {asin}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metadata for ASIN: {Asin}", asin);
                return StatusCode(500, $"Error fetching metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Get audiobook metadata from audimeta.de by ASIN.
        /// </summary>
        [HttpGet("audimeta/{asin}")]
        [ProducesResponseType(typeof(AudimetaBookResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AudimetaBookResponse>> GetAudimetaMetadata(
            string asin,
            [FromQuery] string region = "us",
            [FromQuery] bool cache = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(asin))
                {
                    return BadRequest("ASIN parameter is required");
                }

                var result = await _metadataService.GetAudimetaMetadataAsync(asin, region, cache);
                if (result == null)
                {
                    return NotFound($"No metadata found for ASIN: {asin}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audimeta metadata for ASIN: {Asin}", asin);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
