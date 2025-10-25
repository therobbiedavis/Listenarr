using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AntiforgeryController : ControllerBase
    {
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<AntiforgeryController> _logger;

        public AntiforgeryController(IAntiforgery antiforgery, ILogger<AntiforgeryController> logger)
        {
            _antiforgery = antiforgery;
            _logger = logger;
        }

        [HttpGet("token")]
        [AllowAnonymous]
        public IActionResult GetToken()
        {
            // Log minimal diagnostics about the principal we see when issuing a token.
            try
            {
                var user = HttpContext.User;
                var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
                string? nameMask = null;
                if (isAuthenticated)
                {
                    var name = user?.Identity?.Name ?? user?.FindFirst("sub")?.Value ?? user?.FindFirst("name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        nameMask = name.Length <= 8 ? name : name.Substring(0, 8);
                    }
                }

                _logger.LogInformation("Issuing antiforgery token. Authenticated={Authenticated}, NameMask={NameMask}, ClaimsCount={ClaimsCount}", isAuthenticated, nameMask, user?.Claims?.Count() ?? 0);
            }
            catch (Exception ex)
            {
                // Non-fatal diagnostic logging
                _logger.LogDebug(ex, "Failed to log antiforgery principal details");
            }

            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { token = tokens.RequestToken });
        }
    }
}
